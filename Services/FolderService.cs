using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;

namespace Compresion_RAR.Helpers
{
    public class FolderService
    {
        public static async Task<double> CompressFolderAsync(string folderPath, string archivePath, ProgressWindow pw, Algorithm SelectedAlgorithm, IProgress<int> progress = null, CancellationToken ct = default)
        {
            if (File.Exists(archivePath))
            {
                File.Delete(archivePath);
            }

            List<string> files = new List<string>(Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories));
            int totalFiles = files.Count;
            int filesProcessed = 0;
            long totalOriginalSize = 0;

            foreach (string file in files)
            {
                totalOriginalSize += new FileInfo(file).Length;
            }

            using (FileStream outFs = new FileStream(archivePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                byte[] folderNameBytes = System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(folderPath));
                await outFs.WriteAsync(BitConverter.GetBytes(folderNameBytes.Length), 0, 4, ct);
                await outFs.WriteAsync(folderNameBytes, 0, folderNameBytes.Length, ct);

                foreach (string file in files)
                {
                    ct.ThrowIfCancellationRequested();

                    string relativePath = GetRelativePath(folderPath, file);
                    byte[] fileNameBytes = System.Text.Encoding.UTF8.GetBytes(relativePath);

                    await outFs.WriteAsync(BitConverter.GetBytes(fileNameBytes.Length), 0, 4, ct);
                    await outFs.WriteAsync(fileNameBytes, 0, fileNameBytes.Length, ct);

                    string tempFilePath = Path.GetTempFileName();
                    try
                    {
                        await FileService.CompressFileAsync(file, tempFilePath, pw, SelectedAlgorithm, progress, ct);

                        using (FileStream tempFs = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true))
                        {
                            await tempFs.CopyToAsync(outFs, 81920, ct);
                        }
                    }
                    finally
                    {
                        try { File.Delete(tempFilePath); } catch { }
                    }

                    filesProcessed++;
                    int percentComplete = (int)(filesProcessed / (float)totalFiles * 100);
                    progress?.Report(percentComplete);
                }
            }

            long compressedSize = new FileInfo(archivePath).Length;

            double compressionRatio = (1.0 - ((double)compressedSize / totalOriginalSize)) * 100;

            return compressionRatio; 
        }


        private static string GetRelativePath(string basePath, string fullPath)
        {
            Uri baseUri = new Uri(basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) ? basePath : basePath + Path.DirectorySeparatorChar);
            Uri fullUri = new Uri(fullPath);

            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString()).Replace('/', Path.DirectorySeparatorChar);
        }
    }
}
