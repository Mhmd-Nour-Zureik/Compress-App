using Compresion_RAR;
using Compresion_RAR.Helpers;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;

namespace Compresion_RAR.Views
{
    public partial class FileSectionWindow : Window
    {
        public Algorithm SelectedAlgorithm { get; private set; }
        private string _password = string.Empty;


        public FileSectionWindow(Algorithm algo , string password)
        {
            SelectedAlgorithm = algo;
            _password = password;
            InitializeComponent();
        }

        // Compress Single File : 
        private async void CompressFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog
            {
                Title = "Select File to Compress",
                Filter = "All Files|*.*"
            };
            if (openDlg.ShowDialog(this) != true)
            {
                return;
            }

            string inputPath = openDlg.FileName;

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                Title = "Save Compressed File As",
                FileName = Path.GetFileNameWithoutExtension(inputPath) + (SelectedAlgorithm == Algorithm.HUFFMAN ? ".huff" : ".shanf"),
                Filter = SelectedAlgorithm == Algorithm.HUFFMAN ? "Huffman Files|*.huff" : "Shannon Files|*.shanf"
            };
            if (saveDlg.ShowDialog(this) != true)
            {
                return;
            }

            string outputPath = saveDlg.FileName;

            ProgressWindow pw = new ProgressWindow (isDecompress:false);
            pw.Show();

            Progress<int> progress = new Progress<int>(pct => pw.ReportProgress(pct, $"Compressing… {pct}%"));

            double ratio = 0;
            try
            {
                ratio = await FileService.CompressWithMetadataAsync(
                    inputPath,
                    outputPath,
                    pw,
                    SelectedAlgorithm,
                    _password,
                    progress,
                    pw.Token);

                pw.ReportProgress(100, "Compression Completed");
                await Task.Delay(300);
                _ = MessageBox.Show(this, $"Compression Finished with Compression Ratio : {ratio:F1}%", "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                pw.ReportProgress(0, "Canceled");
                await Task.Delay(300);
                _ = MessageBox.Show(this, $"Compression Canceled", "Warning !", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                pw.ReportProgress(0, $"Error: {ex.Message}");
                await Task.Delay(500);
                Debug.WriteLine(ex.Message);
                _ = MessageBox.Show(this, $"Compression Error:{ex.Message}", "Error !", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                pw.Close();
            }
        }

        // Decompress Single File : 
        private async void DecompressFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog
            {
                Title = "Select File to Decompress",
                Filter = SelectedAlgorithm == Algorithm.HUFFMAN ? "Huffman Files|*.huff" : "Shannon-Fano Files|*.shanf"
            };
            if (openDlg.ShowDialog(this) != true)
            {
                return;
            }
            string archivePath = openDlg.FileName;

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                Title = "Save Decompressed File As",
                FileName = Path.GetFileNameWithoutExtension(archivePath),
                Filter = "All Files|*.*"
            };
            if (saveDlg.ShowDialog(this) != true)
            {
                return;
            }
            string outputPath = saveDlg.FileName;

            ProgressWindow pw = new ProgressWindow(isDecompress: true);
            pw.Show();

            Progress<int> progress = new Progress<int>(pct => pw.ReportProgress(pct, $"Decompressing… {pct}%"));

            try
            {
                await pw.WaitIfPausedAsync();

                pw.StartAnimation(); 
                await FileService.DecompressWithMetadataAsync(
                    archivePath,
                    outputPath,
                    pw,
                    SelectedAlgorithm,
                    _password,
                    progress,
                    pw.Token);

                pw.ReportProgress(100, "Decompression complete");
                await Task.Delay(300);

                _ = MessageBox.Show(this,"Decompression finished.","Done",MessageBoxButton.OK,MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                pw.ReportProgress(0, "Canceled");
                await Task.Delay(300);
                _ = MessageBox.Show(this,"Decompression Canceled.", "Warning ! ", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (InvalidDataException ex)
            {
                _ = MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                pw.ReportProgress(0, $"Error: {ex.Message}");
                await Task.Delay(500);
                Debug.WriteLine(ex.Message);
                _ = MessageBox.Show(this, $"Decompression Error:{ex.Message}", "Error ! ", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                pw.Close();
                pw.StopAnimation();
            }

        }
    }
}
