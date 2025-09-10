using Compresion_RAR.Algorithms;
using Compresion_RAR.Algorithms.ShannoFano;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Compresion_RAR.Helpers
{
    public class FileService
    {
        // Compress File with its Original Format  : 
        public static async Task<double> CompressWithMetadataAsync(
            string inputPath,
            string outputPath,
            ProgressWindow pw,
            Algorithm SelectedAlgorithm,
            string password = null,
            IProgress<int> progress = null,
            CancellationToken ct = default)
        {
            pw.ReportProgress(0 , "Initializing....");
            string temp = Path.GetTempFileName();
            try
            {
                await CompressFileAsync(inputPath, temp, pw, SelectedAlgorithm, progress, ct );

                string ext = Path.GetExtension(inputPath) ?? "";
                byte[] extBytes = System.Text.Encoding.UTF8.GetBytes(ext);
                int extLen = extBytes.Length;
                byte[] lenBytes = BitConverter.GetBytes(extLen);

                byte[] passwordHash = null;
                if (!string.IsNullOrEmpty(password))
                {
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        passwordHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                    }
                }
                using (FileStream outFs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true)) { 

                    await outFs.WriteAsync(lenBytes, 0, lenBytes.Length, ct);

                    if (extLen > 0)
                    {
                        await outFs.WriteAsync(extBytes, 0, extLen, ct);
                    }

                    byte hasPassword =  passwordHash != null ? (byte)1 : (byte)0;

                    await outFs.WriteAsync(new byte[] { hasPassword }, 0, 1, ct);

                    if (passwordHash != null)
                    {
                        await outFs.WriteAsync(passwordHash, 0, passwordHash.Length, ct);
                    }

                    using (FileStream tempFs = new FileStream(temp, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                    {
                        await tempFs.CopyToAsync(outFs, 81920, ct);
                    }
                }
            }
            finally
            {
                try { File.Delete(temp); } catch { }
            }

            return GetCompressionRatio(inputPath, outputPath);
        }


        // Decompress File With its Original Format :
        public static async Task DecompressWithMetadataAsync(
            string archivePath,
            string outputPath,
            ProgressWindow pw,
            Algorithm SelectedAlgorithm,
            string password = null,
            IProgress<int> progress = null,
            CancellationToken ct = default)
        {
            pw.ReportProgress(0, "Initializing...");
            progress?.Report(0);
            await pw.WaitIfPausedAsync();
            ct.ThrowIfCancellationRequested();


            using (FileStream inFs = new FileStream(
                archivePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                bufferSize: 4096, useAsync: true))
            {


                pw.ReportProgress(5, "Reading header...");
                progress?.Report(5);
                await pw.WaitIfPausedAsync();
                ct.ThrowIfCancellationRequested();

                byte[] lenBuf = new byte[4];
                _ = await inFs.ReadAsync(lenBuf, 0, 4, ct);
                int extLen = BitConverter.ToInt32(lenBuf, 0);
                string originalExt = "";
                if (extLen > 0)
                {
                    byte[] extBytes = new byte[extLen];
                    _ = await inFs.ReadAsync(extBytes, 0, extLen, ct);
                    originalExt = Encoding.UTF8.GetString(extBytes);
                }
                // check password :
                byte[] passwordFlag = new byte[1];
                _ = await inFs.ReadAsync(passwordFlag, 0, 1, ct);
                bool requiresPassword = passwordFlag[0] == 1;
                if (requiresPassword)
                {
                    byte[] storedPasswordHash = new byte[32];
                    _ = await inFs.ReadAsync(storedPasswordHash, 0, 32, ct);

                    if (password == null || password.Length == 0)
                    {
                        throw new InvalidDataException(message: "Password Required for Decompression");
                    }

                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] hashedPassword = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

                        if (!hashedPassword.SequenceEqual(storedPasswordHash))
                        {
                            throw new InvalidDataException(message: "Incorrect Password!");
                        }
                    }
                }

                string temp = Path.GetTempFileName();
                try
                {
                    long total = inFs.Length - inFs.Position;
                    long done = 0;
                    pw.ReportProgress(10, "Extracting data...");
                    progress?.Report(10);
                    ct.ThrowIfCancellationRequested();

                    await pw.WaitIfPausedAsync();

                    using (FileStream tempFs = new FileStream(
                        temp, FileMode.Create, FileAccess.Write, FileShare.None,
                        bufferSize: 81920, useAsync: true))
                    {
                        byte[] buffer = new byte[81920];
                        int read;
                        while ((read = await inFs.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            await pw.WaitIfPausedAsync();
                            await tempFs.WriteAsync(buffer, 0, read, ct);

                            done += read;
                            int pct = (int)(done * 100 / total);
                            progress?.Report(pct);
                            pw.ReportProgress(pct, $"Extracted {pct}%");
                            ct.ThrowIfCancellationRequested();

                        }
                    }

                    string finalPath = outputPath;
                    if (!string.IsNullOrEmpty(originalExt) &&
                        !finalPath.EndsWith(originalExt, StringComparison.OrdinalIgnoreCase))
                    {
                        finalPath += originalExt;
                    }

                    pw.ReportProgress(70, "Decompressing file...");
                    progress?.Report(70);
                    ct.ThrowIfCancellationRequested();
                    await pw.WaitIfPausedAsync();


                    await DecompressFileAsync(temp, finalPath, pw, SelectedAlgorithm, progress, ct);
                    await pw.WaitIfPausedAsync();


                }
                finally
                {
                    try { File.Delete(temp); } catch { }
                }
            }
        }

        // Apply Compressing as Real Time for user : 
        public static async Task CompressFileAsync(
            string inputPath,
            string outputPath,
            ProgressWindow pw,
            Algorithm SelectedAlgorithm,
            IProgress<int> progress = null,
            CancellationToken ct = default)
        {
            const int BUF = 1 << 20;

            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
            }

            long totalBytes = new FileInfo(inputPath).Length;
            long bytesRead = 0;
            byte[] chunkBuffer = new byte[BUF];
            int chunkIndex = 0;

            using (FileStream outFs = new FileStream(
                outputPath, FileMode.Create, FileAccess.Write, FileShare.None,
                bufferSize: BUF, useAsync: true))
            {
                using (FileStream inFs = new FileStream(
                    inputPath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: BUF, useAsync: true))
                {
                    int n;
                    while ((n = await inFs.ReadAsync(chunkBuffer, 0, BUF, ct)) > 0)
                    {
                        ct.ThrowIfCancellationRequested();
                        await pw.WaitIfPausedAsync();
                        byte[] chunk = chunkBuffer;
                        if (n != BUF)
                        {
                            chunk = new byte[n];
                            Array.Copy(chunkBuffer, chunk, n);
                        }

                        string temp = Path.GetTempFileName();
                        try
                        {
                            if (SelectedAlgorithm == Algorithm.HUFFMAN)
                            {
                                HuffmanCompressor.Compress(chunk, temp, pw, ct);
                            }
                            else
                            {
                                ShannonFanoCompressor.Compress(chunk , temp , pw , ct);
                            }

                            using (FileStream tempFs = new FileStream(
                                temp, FileMode.Open, FileAccess.Read, FileShare.Read,
                                bufferSize: BUF, useAsync: true))
                            {
                                await tempFs.CopyToAsync(outFs, BUF, ct);
                            }

                        }
                        finally
                        {
                            File.Delete(temp);
                        }

                        bytesRead += n;
                        progress?.Report((int)(bytesRead * 100 / totalBytes));
                        chunkIndex++;
                    }
                }
            }
        }

        // Apply Decompressing as Real Time for user : 
        public static Task DecompressFileAsync(
            string compressedPath,
            string outputPath,
            ProgressWindow pw,
            Algorithm SelectedAlgorithm,
            IProgress<int> progress = null,
            CancellationToken ct = default)
        {
            return Task.Run(async () =>
            {
                ct.ThrowIfCancellationRequested();
                pw.ReportProgress(70, "Decompressing file...");
                await pw.WaitIfPausedAsync();

                if (SelectedAlgorithm == Algorithm.HUFFMAN)
                {
                    await HuffmanCompressor.Decompress(compressedPath, outputPath, pw, ct);
                }
                else
                {
                    ShannonFanoCompressor.Decompress(compressedPath, outputPath, pw, ct);
                }
                progress?.Report(100);
                pw.ReportProgress(100, "Decompression complete");
            }, ct);
        }


        // Get File Compression Ratio :
        public static double GetCompressionRatio(string originalPath, string compressedPath)
        {
            long origSize = new FileInfo(originalPath).Length;
            long compSize = new FileInfo(compressedPath).Length;
            return origSize == 0 ? 0 : (1.0 - (double)compSize / origSize) * 100;
        }

    }

}
