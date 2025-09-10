using Compresion_RAR.Algorithms;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace Compresion_RAR.Helpers
{
    class ArchiveService
    {
        private static string Magic = "MHAR";

        // Compress Multi Files Choosen To Archive.mhar
        public static async Task<double> CreateMultiArchiveAsync(
            string[] inputPaths,
            string archivePath,
            ProgressWindow pw,
            Algorithm SelectedAlgorithm,
            IProgress<int> progress = null,
            CancellationToken ct = default,
            string password = null)
        {
            if (SelectedAlgorithm == Algorithm.SHANNON_FANO)
            {
                Magic = "MSFR";
            }
            List<(string name, string tempFile, long length)> entries = new List<(string name, string tempFile, long length)>();
            for (int i = 0; i < inputPaths.Length; i++)
            {
                ct.ThrowIfCancellationRequested();
                string input = inputPaths[i];
                string temp = Path.GetTempFileName();

                await FileService.CompressFileAsync(input, temp, pw, SelectedAlgorithm, progress, ct);
                long len = new FileInfo(temp).Length;
                entries.Add((Path.GetFileName(input), temp, len));

                progress?.Report((i + 1) * 100 / (inputPaths.Length * 2));
            }

            using (FileStream outFs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                byte hasPassword = !string.IsNullOrEmpty(password) ? (byte)1 : (byte)0;
                await outFs.WriteAsync(new byte[] { hasPassword }, 0, 1, ct);

                byte[] passwordHash = null;
                if (hasPassword == 1)
                {
                    using (SHA256 sha256 = SHA256.Create())
                    {
                        passwordHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                        await outFs.WriteAsync(passwordHash, 0, passwordHash.Length, ct);
                    }

                }

                byte[] magicBytes = Encoding.ASCII.GetBytes(Magic);
                await outFs.WriteAsync(magicBytes, 0, 4, ct);
                await outFs.WriteAsync(BitConverter.GetBytes(entries.Count), 0, 4, ct);

                List<(byte[] nameBytes, long offset, long length)> index = new List<(byte[] nameBytes, long offset, long length)>();



                long indexStart = outFs.Position;

                foreach (var (name, _, _) in entries)
                {
                    byte[] nameBytes = Encoding.UTF8.GetBytes(name);
                    await outFs.WriteAsync(BitConverter.GetBytes(nameBytes.Length), 0, 4, ct);
                    await outFs.WriteAsync(nameBytes, 0, nameBytes.Length, ct);
                    await outFs.WriteAsync(new byte[8], 0, 8, ct); 
                    await outFs.WriteAsync(new byte[8], 0, 8, ct); 
                }



                List<(long offset, long length)> realOffsets = new List<(long, long)>();

                foreach (var (_, tempFile, _) in entries)
                {
                    ct.ThrowIfCancellationRequested();

                    long offset = outFs.Position;

                    using (var tempFs = new FileStream(tempFile, FileMode.Open, FileAccess.Read))
                    {
                        await tempFs.CopyToAsync(outFs, 81920, ct);
                    }

                    long length = outFs.Position - offset;
                    realOffsets.Add((offset, length));

                    int percent = 50 + (int)(realOffsets.Count() * 50 / entries.Sum(x => x.length));
                    progress?.Report(percent);
                    pw.ReportProgress(percent, $"Writing...{percent}");
                }

                outFs.Seek(indexStart, SeekOrigin.Begin);
                for (int i = 0; i < entries.Count; i++)
                {
                    var nameBytes = Encoding.UTF8.GetBytes(entries[i].name);

                    await outFs.WriteAsync(BitConverter.GetBytes(nameBytes.Length), 0, 4, ct);
                    await outFs.WriteAsync(nameBytes, 0, nameBytes.Length, ct);
                    await outFs.WriteAsync(BitConverter.GetBytes(realOffsets[i].Item1), 0, 8, ct); 
                    await outFs.WriteAsync(BitConverter.GetBytes(realOffsets[i].Item2), 0, 8, ct); 
                }

            }

            foreach (var (_, tempFile, _) in entries)
                File.Delete(tempFile);

            return GetArchiveCompressionRatio(archivePath , inputPaths);
        }

        public static async Task ExtractSingleFileAsync(
      string archivePath,
      string targetFileName,
      string outputPath,
      ProgressWindow pw,
      Algorithm SelectedAlgorithm,
      CancellationToken ct = default,
      string password = null)
        {
            if (SelectedAlgorithm == Algorithm.SHANNON_FANO)
            {
                Magic = "MSFR";
            }
            using (FileStream inFs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                byte[] passwordFlag = new byte[1];
                await inFs.ReadAsync(passwordFlag, 0, 1, ct);
                bool requiresPassword = passwordFlag[0] == 1;

                if (requiresPassword)
                {
                    byte[] storedPasswordHash = new byte[32];
                    await inFs.ReadAsync(storedPasswordHash, 0, 32, ct);

                    if (string.IsNullOrEmpty(password))
                        throw new InvalidDataException("Password is required to extract this archive.");

                    using (SHA256 sha256 = SHA256.Create())
                    {
                        byte[] givenHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                        if (!givenHash.SequenceEqual(storedPasswordHash))
                            throw new InvalidDataException("Incorrect password.");
                    }
                }

                byte[] magicBuf = new byte[4];
                await inFs.ReadAsync(magicBuf, 0, 4, ct);
                if (Encoding.ASCII.GetString(magicBuf) != Magic)
                    throw new InvalidDataException("Archive is not valid!");

                byte[] countBuf = new byte[4];
                await inFs.ReadAsync(countBuf, 0, 4, ct);
                int count = BitConverter.ToInt32(countBuf, 0);

                List<(string name, long offset, long length)> entries = new List<(string, long, long)>();
                for (int i = 0; i < count; i++)
                {
                    byte[] nameLenBuf = new byte[4];
                    await inFs.ReadAsync(nameLenBuf, 0, 4, ct);
                    int nameLen = BitConverter.ToInt32(nameLenBuf, 0);

                    byte[] nameBuf = new byte[nameLen];
                    await inFs.ReadAsync(nameBuf, 0, nameLen, ct);
                    string name = Encoding.UTF8.GetString(nameBuf);

                    byte[] offsetBuf = new byte[8];
                    byte[] lengthBuf = new byte[8];
                    await inFs.ReadAsync(offsetBuf, 0, 8, ct);
                    await inFs.ReadAsync(lengthBuf, 0, 8, ct);

                    long offset = BitConverter.ToInt64(offsetBuf, 0);
                    long length = BitConverter.ToInt64(lengthBuf, 0);
                    entries.Add((name, offset, length));
                }

                var match = entries.FirstOrDefault(e => e.name.Equals(targetFileName, StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrEmpty(match.name))
                    throw new InvalidDataException("Target file not found in archive.");

                string temp = Path.GetTempFileName();
                try
                {
                    inFs.Seek(match.offset, SeekOrigin.Begin);
                    using (var tempFs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                    {
                        byte[] buffer = new byte[81920];
                        long remaining = match.length;
                        while (remaining > 0)
                        {
                            int read = await inFs.ReadAsync(buffer, 0, (int)Math.Min(buffer.Length, remaining), ct);
                            if (read == 0) throw new EndOfStreamException("Unexpected end of archive.");
                            await tempFs.WriteAsync(buffer, 0, read, ct);
                            remaining -= read;
                        }
                    }

                    await FileService.DecompressFileAsync(temp, outputPath, pw, SelectedAlgorithm, null, ct);
                }
                finally
                {
                    File.Delete(temp);
                }
            }
        }


        // get all files names and view it to user to extract the choosen one :
        public static async Task<List<string>> GetArchiveIndexAsync(string archivePath, CancellationToken ct = default)
        {
            using (FileStream inFs = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
            {
                byte[] flagBuf = new byte[1];
                await inFs.ReadAsync(flagBuf, 0, 1, ct);
                if (flagBuf[0] == 1)
                {
                    byte[] hashBuf = new byte[32];
                    await inFs.ReadAsync(hashBuf, 0, 32, ct);
                }

                byte[] buf = new byte[8];
                await inFs.ReadAsync(buf, 0, 8, ct);
                int count = BitConverter.ToInt32(buf, 4);
                List<string> list = new List<string>(count);

                for (int i = 0; i < count; i++)
                {
                    await inFs.ReadAsync(buf, 0, 4, ct);
                    int nl = BitConverter.ToInt32(buf, 0);

                    byte[] nameBuf = new byte[nl];
                    await inFs.ReadAsync(nameBuf, 0, nl, ct);
                    list.Add(Encoding.UTF8.GetString(nameBuf));

                    await inFs.ReadAsync(buf, 0, 8, ct); 
                    await inFs.ReadAsync(buf, 0, 8, ct); 
                }

                return list;
            }
        }

        // Get the compression ration for all files together :
        public static double GetArchiveCompressionRatio(string archivePath, string[] originalPaths)
        {
            long totalOriginal = 0;
            foreach (string p in originalPaths)
            {
                totalOriginal += new FileInfo(p).Length;
            }

            long archiveSize = new FileInfo(archivePath).Length;

            if (totalOriginal == 0)
            {
                return 0;
            }

            double ratio = (1.0 - (double)archiveSize / totalOriginal) * 100;
            return ratio;
        }

    }
}
