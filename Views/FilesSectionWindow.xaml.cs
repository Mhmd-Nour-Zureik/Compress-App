using Compresion_RAR.Helpers;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace Compresion_RAR.Views
{
    public partial class FilesSectionWindow : Window
    {
        public Algorithm SelectedAlgorithm { get; private set; }
        private string _password = string.Empty;


        public FilesSectionWindow(Algorithm algo , string password)
        {
            SelectedAlgorithm = algo;
            _password = password;
            InitializeComponent();
        }

        // Compress Multi Choosen Files Together to single Archive :
        private async void CompressFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog
            {
                Title = "Select Files to Bundle into Archive",
                Multiselect = true,
                Filter = "All Files|*.*"
            };
            if (dlg.ShowDialog(this) != true)
            {
                return;
            }

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                Title = "Save Archive As",
                FileName = SelectedAlgorithm == Algorithm.HUFFMAN ? "archive.mhar" : "archive.msfr",
                Filter = SelectedAlgorithm == Algorithm.HUFFMAN ? "MyArchive|*.mhar" : "MyArchive|*.msfr"
            };
            if (saveDlg.ShowDialog(this) != true)
            {
                return;
            }

            ProgressWindow pw = new ProgressWindow (isDecompress:false);
            pw.Show();
            Progress<int> progress = new Progress<int>(p => pw.ReportProgress(p, $"Packing… {p}%"));
            double percent = 0.0f;
            try
            {

                percent = await ArchiveService.CreateMultiArchiveAsync(dlg.FileNames,saveDlg.FileName,pw, SelectedAlgorithm,progress, pw.Token , _password);

                pw.ReportProgress(100, "Archive created!");

                await Task.Delay(300);
                _ = MessageBox.Show(this,$"Archive created with compression ratio: {percent:F1}%", "Success",MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                pw.ReportProgress(0, "Canceled");
                await Task.Delay(300);
                _ = MessageBox.Show(this, $"Archive Creation Canceled", "Warning !", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                pw.ReportProgress(0, $"Error: {ex.Message}");
                Debug.WriteLine(ex);
                await Task.Delay(500);
                _ = MessageBox.Show(this, $"Archive Creation Error", "Error !", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                pw.Close();
            }
        }

        // Decompress 1 File from Archive : 
        private async void DecompressFilesButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDlg = new OpenFileDialog
            {
                Title = "Select Archive to Open",
                Multiselect = false,
                Filter = SelectedAlgorithm == Algorithm.HUFFMAN ? "MyArchive|*.mhar" : "MyArchive|*.msfr"
            };

            if (openDlg.ShowDialog(this) != true)
            {
                return;
            }

            string archivePath = openDlg.FileName;
            List<string> entries;
            try
            {
                entries = await ArchiveService.GetArchiveIndexAsync(archivePath);
            }
            catch (Exception ex)
            {
                _ = MessageBox.Show(this, $"Not a valid archive:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            CompressedItemsListBox selectWin = new CompressedItemsListBox(entries) { Owner = this };
            if (selectWin.ShowDialog() != true)
            {
                return;
            }

            string chosen = selectWin.SelectedEntry;

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                Title = $"Save {chosen} As",
                FileName = chosen,
                Filter = "All Files|*.*"
            };
            if (saveDlg.ShowDialog(this) != true)
            {
                return;
            }

            string outputPath = saveDlg.FileName;

            ProgressWindow pw = new ProgressWindow (isDecompress:true);
            pw.Show();
            Progress<int> progress = new Progress<int>(p => pw.ReportProgress(p, $"Extracting… {p}%"));

            try
            {
                pw.StartAnimation();
                await pw.WaitIfPausedAsync();

                await ArchiveService.ExtractSingleFileAsync(archivePath,chosen,outputPath,pw, SelectedAlgorithm, pw.Token , _password);

                await pw.WaitIfPausedAsync();

                pw.ReportProgress(100, "Extraction completed!");

                await Task.Delay(300);
                _ = MessageBox.Show(this,"File extracted!","Success",MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                pw.ReportProgress(0, "Canceled");
                await Task.Delay(300);
                _ = MessageBox.Show(this, "File Extraction Canceled!", "Warning !", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                _ = MessageBox.Show(this, "File Extraction Error !", "Error !", MessageBoxButton.OK, MessageBoxImage.Error);
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
