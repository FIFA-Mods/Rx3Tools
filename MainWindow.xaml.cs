using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rx3Tools
{
    public partial class MainWindow : Window
    {
        bool initialized = false;
        string lastInputDir;
        string lastOutputDir;
        public MainWindow()
        {
            InitializeComponent();
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string skeletonDir = Path.Combine(appDir, "data", "skeletons");
            if (Directory.Exists(skeletonDir))
            {
                var files = Directory.GetFiles(skeletonDir, "*.rx3", SearchOption.AllDirectories);
                var relativePaths = files
                    .Select(f => GetRelativePath(skeletonDir, f).Replace("\\", "/"))
                    .ToList();
                relativePaths.Insert(0, string.Empty);
                cbSkeleton.ItemsSource = relativePaths;
            }
            lastInputDir = Properties.Settings.Default.LastInputDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            lastOutputDir = Properties.Settings.Default.LastOutputDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            initialized = true;
        }

        static string GetRelativePath(string basePath, string fullPath)
        {
            Uri baseUri = new Uri(AppendDirectorySeparatorChar(basePath));
            Uri fullUri = new Uri(fullPath);
            Uri relativeUri = baseUri.MakeRelativeUri(fullUri);
            return Uri.UnescapeDataString(relativeUri.ToString());
        }

        static string AppendDirectorySeparatorChar(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
                path += Path.DirectorySeparatorChar;
            return path;
        }

        void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        void RemoveReadOnly(string filename)
        {
            if (System.IO.File.Exists(filename))
            {
                System.IO.File.SetAttributes(filename, FileAttributes.Normal);
            }
        }

        void RemoveFile(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                RemoveReadOnly(filePath);
                System.IO.File.Delete(filePath);
            }
        }

        void RenameFile(string oldPath, string newPath)
        {
            RemoveFile(newPath);
            File.Move(oldPath, newPath);
        }

        void RemoveFolderIfEmpty(string folderPath)
        {
            if (Directory.Exists(folderPath) && !Directory.EnumerateFileSystemEntries(folderPath).Any())
            {
                try
                {
                    Directory.Delete(folderPath, true);
                }
                catch
                {
                }
            }
        }

        private void OperationClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string GetSelectedTagOrContent(ComboBox cb)
                {
                    if (cb?.SelectedItem == null) return null;
                    if (cb.SelectedItem is ComboBoxItem cbi)
                    {
                        if (cbi.Tag != null) return cbi.Tag.ToString();
                        return cbi.Content?.ToString();
                    }
                    return cb.SelectedItem.ToString();
                }
                string GetSelectedDisplayText(ComboBox cb)
                {
                    if (cb?.SelectedItem == null) return null;
                    if (cb.SelectedItem is ComboBoxItem cbi) return cbi.Content?.ToString();
                    return cb.SelectedItem.ToString();
                }
                string operationTag = GetSelectedTagOrContent(cbOperation)?.Trim();
                string inputValue = tbInput.Text?.Trim();
                string gameTag = GetSelectedTagOrContent(cbGame)?.Trim();
                string skeletonName = GetSelectedDisplayText(cbSkeleton)?.Trim();
                if (string.IsNullOrWhiteSpace(skeletonName)) skeletonName = null;
                string modelTag = GetSelectedTagOrContent(cbModel)?.Trim();
                string textureTag = GetSelectedTagOrContent(cbTexture)?.Trim();
                var sb = new StringBuilder();
                if (string.Equals(operationTag, "ExtractFile", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(operationTag, "ExtractFolder", StringComparison.OrdinalIgnoreCase))
                {
                    sb.Append("-export ");
                }
                else
                {
                    MessageBox.Show(this, "Operation is not selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(inputValue))
                {
                    sb.Append("-i ");
                    sb.Append("\"").Append(inputValue).Append("\" ");
                }
                else
                {
                    MessageBox.Show(this, "Input is not selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Path.Combine(appDir, "rx3c.exe");
                if (!File.Exists(exePath))
                {
                    MessageBox.Show(this, $"Cannot find rx3c.exe at:\n{exePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                var dlg = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    EnsurePathExists = true,
                    EnsureValidNames = true,
                    Title = "Select output folder",
                    InitialDirectory = Directory.Exists(lastOutputDir) ? lastOutputDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    DefaultDirectory = Directory.Exists(lastOutputDir) ? lastOutputDir : Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                };

                if (dlg.ShowDialog() != CommonFileDialogResult.Ok)
                    return;

                string outputFolder = dlg.FileName;
                lastOutputDir = outputFolder;
                Properties.Settings.Default.LastOutputDir = lastOutputDir;
                Properties.Settings.Default.Save();

                sb.Append("-o ");
                sb.Append("\"").Append(outputFolder).Append("\" ");
                if (!string.IsNullOrWhiteSpace(gameTag))
                {
                    sb.Append("-game ");
                    sb.Append("\"").Append(gameTag).Append("\" ");
                }
                if (!string.IsNullOrWhiteSpace(skeletonName))
                {
                    sb.Append("-skeleton ");
                    string skeletonPath = Path.Combine(appDir, "data", "skeletons", skeletonName);
                    sb.Append("\"").Append(skeletonPath).Append("\" ");
                }
                if (!string.IsNullOrWhiteSpace(modelTag))
                {
                    sb.Append("-model ");
                    sb.Append("\"").Append(modelTag).Append("\" ");
                }
                if (!string.IsNullOrWhiteSpace(textureTag))
                {
                    sb.Append("-texture ");
                    sb.Append("\"").Append(textureTag).Append("\" ");
                }
                string arguments = sb.ToString().TrimEnd();
                string displayCommand = $"\"{exePath}\" {arguments}";
                //MessageBox.Show(this, displayCommand, "Command to run (debug)", MessageBoxButton.OK, MessageBoxImage.Information);
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    WorkingDirectory = appDir,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Error while preparing or launching rx3c.exe:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        void ExitClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = cbOperation.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;

            string tag = selectedItem.Tag as string;
            if (tag == "ExtractFile")
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "RX3 files (*.rx3)|*.rx3|All files (*.*)|*.*",
                    Title = "Select RX3 File",
                    InitialDirectory = Directory.Exists(lastInputDir) ? lastInputDir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    tbInput.Text = openFileDialog.FileName;
                    lastInputDir = Path.GetDirectoryName(openFileDialog.FileName) ?? lastInputDir;
                    Properties.Settings.Default.LastInputDir = lastInputDir;
                    Properties.Settings.Default.Save();
                }
            }
            else if (tag == "ExtractFolder")
            {
                var folderDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = "Select Folder Containing RX3 Files",
                    InitialDirectory = Directory.Exists(lastInputDir) ? lastInputDir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    DefaultDirectory = Directory.Exists(lastInputDir) ? lastInputDir : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };
                if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    tbInput.Text = folderDialog.FileName;
                    lastInputDir = folderDialog.FileName;
                    Properties.Settings.Default.LastInputDir = lastInputDir;
                    Properties.Settings.Default.Save();
                }
            }
        }

        void TbDirectory_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized)
                return;
        }

        void Hyperlink_Navigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }

        void cbOperation_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            tbInput.Clear();
            if (cbOperation.SelectedItem is ComboBoxItem selectedItem)
            {
                string tag = selectedItem.Tag as string;
                if (tag == "ExtractFile")
                    lblSelect.Content = "Select File";
                else if (tag == "ExtractFolder")
                    lblSelect.Content = "Select Folder";
            }
        }
    }
}
