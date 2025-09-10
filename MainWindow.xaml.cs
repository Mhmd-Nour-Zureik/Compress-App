using Compresion_RAR.Helpers;
using Compresion_RAR.Enums;
using Compresion_RAR.Views;
using Microsoft.Win32;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;

namespace Compresion_RAR
{
    public partial class MainWindow : Window
    {
        public Algorithm SelectedAlgorithm { get; private set; }
        private string _password = string.Empty;


        public MainWindow()
        {
            InitializeComponent();
        }
        // File Section : Compress and Decompress File .
        private void FileSectionButton_Click(object sender, RoutedEventArgs e)
        {
            StorePassword();
            FileSectionWindow fileWindow = new FileSectionWindow(GetSelectedAlgorithm() , _password)
            {
                Owner = this
            };
            fileWindow.Show();
        }

        // Files Section : Compress Multi Files and Extract one .
        private void FilesSectionButton_Click(object sender, RoutedEventArgs e)
        {
            StorePassword();
            FilesSectionWindow fileWindow = new FilesSectionWindow(GetSelectedAlgorithm() , _password)
            {
                Owner = this
            };
            fileWindow.Show();
        }

        // Compress Folder .
        private async void FolderCompressionButton_Click(object sender, RoutedEventArgs e)
        {
            StorePassword();
            SelectedAlgorithm = GetSelectedAlgorithm();
            System.Windows.Forms.FolderBrowserDialog folderDialog = new System.Windows.Forms.FolderBrowserDialog();   
            if (folderDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            string folderPath = folderDialog.SelectedPath;

            string folderName = Path.GetFileName(folderPath);

            SaveFileDialog saveDlg = new SaveFileDialog
            {
                Title = "Save Compressed Folder As",
                FileName = folderName + (SelectedAlgorithm == Algorithm.HUFFMAN ? ".mhar" : ".msfr"),
                Filter = SelectedAlgorithm == Algorithm.HUFFMAN ? "MyArchive|*.mhar" : "MyArchive|*.msfr"
            };
            if (saveDlg.ShowDialog(this) != true)
            {
                return;
            }

            string outputPath = saveDlg.FileName;

            ProgressWindow pw = new ProgressWindow(isDecompress: false);
            pw.Show();

            Progress<int> progress = new Progress<int>(pct => pw.ReportProgress(pct, $"Compressing... {pct}%"));
            double percent = 0.0f;
            try
            {
                percent = await FolderService.CompressFolderAsync(folderPath, outputPath, pw, SelectedAlgorithm, progress ,pw.Token);
                pw.ReportProgress(100, "Compression complete!");
                await Task.Delay(300);
                _ = MessageBox.Show(this, $"Folder Compression completed with Compression Ratio : {percent:F1}%", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                pw.ReportProgress(0, "Canceled");
                await Task.Delay(300);
                _ = MessageBox.Show(this,"Decompression Canceled.","Warning ! ",MessageBoxButton.OK,MessageBoxImage.Warning);
                return;
            }
            catch (Exception ex)
            {
                pw.ReportProgress(0, $"Error: {ex.Message}");
                _ = MessageBox.Show(this,"Decompression Error.","Error! ",MessageBoxButton.OK,MessageBoxImage.Error);
                return;
            }
            finally
            {
                pw.Close();
            }
        }

        // Get the Selected Algorithm : 
        private Algorithm GetSelectedAlgorithm()
        {
            return HuffmanRadioButton.IsChecked == true ? Algorithm.HUFFMAN : Algorithm.SHANNON_FANO;
        }

        private void PasswordCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            PasswordBox.IsEnabled = true;
        }

        private void PasswordCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            PasswordBox.IsEnabled = false;
            _password = string.Empty;
        }

        private void StorePassword()
        {
            if (PasswordBox.IsEnabled && !string.IsNullOrEmpty(PasswordBox.Password))
            {
                _password = PasswordBox.Password;
            }
        }
    }
}