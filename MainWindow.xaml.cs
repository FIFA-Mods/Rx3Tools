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
        private OptionsWindow optionsWindow;

        public MainWindow()
        {
            InitializeComponent();
            optionsWindow = new OptionsWindow();
            lastInputDir = Properties.Settings.Default.LastInputDir ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            lastOutputDir = Properties.Settings.Default.LastOutputDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            UpdateComboBoxes();
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string boneRemapDir = Path.Combine(appDir, "data", "bone_remap");
            List<string> boneRemapFiles = new List<string> { "-" };
            if (Directory.Exists(boneRemapDir))
            {
                var files = Directory.GetFiles(boneRemapDir, "*.txt", SearchOption.TopDirectoryOnly);
                boneRemapFiles.AddRange(files.Select(Path.GetFileName));
            }
            optionsWindow.cbBoneRemap.ItemsSource = boneRemapFiles;
            optionsWindow.cbBoneRemap.SelectedIndex = 0;
            string posesDir = Path.Combine(appDir, "data", "poses");
            List<string> poseChangeFiles = new List<string> { "-" };
            if (Directory.Exists(posesDir))
            {
                string fromSuffix = "_from.fbx";
                string toSuffix = "_to.fbx";
                var fbxFiles = new HashSet<string>(Directory.GetFiles(posesDir, "*.fbx", SearchOption.TopDirectoryOnly)
                    .Select(Path.GetFileName), StringComparer.OrdinalIgnoreCase);
                var validPairs = fbxFiles
                    .Where(f => f.EndsWith(fromSuffix, StringComparison.OrdinalIgnoreCase))
                    .Select(f => f.Substring(0, f.Length - fromSuffix.Length))
                    .Where(x => fbxFiles.Contains($"{x}{toSuffix}"));
                poseChangeFiles.AddRange(validPairs);
            }
            optionsWindow.cbPoseChange.ItemsSource = poseChangeFiles;
            optionsWindow.cbPoseChange.SelectedIndex = 0;
            initialized = true;
        }

        private void cbGame_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbSkeleton == null) return;
            UpdateComboBoxes();
        }

        private void UpdateComboBoxes()
        {
            if (cbGame.SelectedItem is ComboBoxItem selectedGame)
            {
                string gameName = selectedGame.Tag as string;
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string skeletonDir = Path.Combine(appDir, "data", "skeletons", gameName);
                List<string> skeletonFiles = new List<string> { "-" };
                if (Directory.Exists(skeletonDir))
                {
                    var files = Directory.GetFiles(skeletonDir, "*.rx3", SearchOption.TopDirectoryOnly);
                    skeletonFiles.AddRange(files.Select(Path.GetFileName));
                }
                cbSkeleton.ItemsSource = skeletonFiles;
                cbSkeleton.SelectedIndex = 0;
                string baseModelsDir = Path.Combine(appDir, "data", "base_models", gameName);
                List<string> baseModelFiles = new List<string> { "-" };
                if (Directory.Exists(baseModelsDir))
                {
                    var files = Directory.GetFiles(baseModelsDir, "*.rx3", SearchOption.TopDirectoryOnly);
                    baseModelFiles.AddRange(files.Select(Path.GetFileName));
                }
                cbBaseModel.ItemsSource = baseModelFiles;
                cbBaseModel.SelectedIndex = 0;
            }
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
                string GetSelectedTag(ComboBox cb)
                {
                    if (cb?.SelectedItem == null) return null;
                    if (cb.SelectedItem is ComboBoxItem cbi)
                    {
                        return cbi.Tag.ToString();
                    }
                    return cb.SelectedItem.ToString();
                }
                string GetSelectedDisplayText(ComboBox cb)
                {
                    if (cb?.SelectedItem == null) return null;
                    if (cb.SelectedItem is ComboBoxItem cbi) return cbi.Content?.ToString();
                    if (cb.SelectedItem.ToString() == "-")
                    {
                        return "";
                    }
                    return cb.SelectedItem.ToString();
                }
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string exePath = Path.Combine(appDir, "rx3c.exe");
                if (!File.Exists(exePath))
                {
                    MessageBox.Show(this, $"Cannot find rx3c.exe at:\n{exePath}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string operationTag = GetSelectedTag(cbOperation)?.Trim();
                string inputValue = tbInput.Text?.Trim();
                string gameTag = GetSelectedTag(cbGame)?.Trim();
                string skeletonName = GetSelectedDisplayText(cbSkeleton)?.Trim();
                string baseModelName = GetSelectedDisplayText(cbBaseModel)?.Trim();
                string boneRemapName = GetSelectedDisplayText(optionsWindow.cbBoneRemap)?.Trim();
                string poseChangeName = GetSelectedDisplayText(optionsWindow.cbPoseChange)?.Trim();
                string modelTag = GetSelectedTag(cbModel)?.Trim();
                string textureTag = GetSelectedTag(cbTexture)?.Trim();
                string folderOptionTag = GetSelectedTag(cbFolderOption)?.Trim();
                string boneMatricesTag = GetSelectedTag(optionsWindow.cbBoneMatrices)?.Trim();
                string texMetadataTag = GetSelectedTag(optionsWindow.cbTextureMetadata)?.Trim();
                var sb = new StringBuilder();
                bool extract = string.Equals(operationTag, "ExtractFiles", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(operationTag, "ExtractFolder", StringComparison.OrdinalIgnoreCase);
                bool import = string.Equals(operationTag, "ImportFiles", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(operationTag, "ImportFolder", StringComparison.OrdinalIgnoreCase);
                if (extract)
                {
                    sb.Append("-export");
                }
                else if (import)
                {
                    sb.Append("-import");
                }
                else
                {
                    MessageBox.Show(this, "Operation is not selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                if (!string.IsNullOrWhiteSpace(inputValue))
                {
                    string[] filePaths = inputValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string path in filePaths)
                    {
                        string trimmedPath = path.Trim();
                        if (!string.IsNullOrWhiteSpace(trimmedPath))
                        {
                            sb.Append($" -i \"{trimmedPath}\"");
                        }
                    }
                }
                else
                {
                    MessageBox.Show(this, "Input is not selected", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                sb.Append($" -o \"{outputFolder}\"");
                if (chkRecursive.IsChecked == true)
                {
                    sb.Append(" -recursive");
                }
                if (!string.IsNullOrWhiteSpace(gameTag))
                {
                    sb.Append($" -game \"{gameTag}\"");
                }
                if (!string.IsNullOrWhiteSpace(baseModelName))
                {
                    sb.Append($" -baseModel \"{Path.Combine(appDir, "data", "base_models", gameTag, baseModelName)}\"");
                }
                if (!string.IsNullOrWhiteSpace(skeletonName))
                {
                    sb.Append($" -skeleton \"{Path.Combine(appDir, "data", "skeletons", gameTag, skeletonName)}\"");
                }
                if (!string.IsNullOrWhiteSpace(textureTag))
                {
                    sb.Append($" -texture \"{textureTag}\"");
                }
                if (import)
                {
                    if (!string.IsNullOrWhiteSpace(gameTag) &&
                        (texMetadataTag == "global" || texMetadataTag == "local+global"))
                    {
                        string globalTexMetadataPath = Path.Combine(appDir, "data", "texture_formats", gameTag + ".csv");
                        if (File.Exists(globalTexMetadataPath))
                        {
                            sb.Append($" -texFormatFile \"{globalTexMetadataPath}\"");
                        }
                    }
                    if (optionsWindow.chkTristrips.IsChecked == true)
                    {
                        sb.Append(" -tristrip");
                    }
                    if (optionsWindow.chkBinormals.IsChecked == true)
                    {
                        sb.Append(" -binormals");
                    }
                    if (!string.IsNullOrWhiteSpace(boneMatricesTag))
                    {
                        sb.Append($" -boneMatrices {boneMatricesTag}");
                    }
                    double scale = optionsWindow.sbScale.Value;
                    if (scale != 1.0) {
                        sb.Append($" -scale {scale}");
                    }
                    double moveX = optionsWindow.sbMoveX.Value;
                    double moveY = optionsWindow.sbMoveY.Value;
                    double moveZ = optionsWindow.sbMoveZ.Value;
                    if (moveX != 0.0 || moveY != 0.0 || moveZ != 0.0)
                    {
                        sb.Append($" -move {moveX},{moveY},{moveZ}");
                    }
                    if (!string.IsNullOrWhiteSpace(boneRemapName))
                    {
                        sb.Append($" -boneRemap \"{Path.Combine(appDir, "data", "bone_remap", boneRemapName)}\"");
                    }
                    if (!string.IsNullOrWhiteSpace(poseChangeName))
                    {
                        sb.Append($" -poseFrom \"{Path.Combine(appDir, "data", "poses", poseChangeName)}_from.fbx\"");
                        sb.Append($" -poseTo \"{Path.Combine(appDir, "data", "poses", poseChangeName)}_to.fbx\"");
                    }
                }
                if (extract)
                {
                    if (!string.IsNullOrWhiteSpace(modelTag))
                    {
                        sb.Append($" -model \"{modelTag}\"");
                    }
                    if (optionsWindow.cbMeshQuads.SelectedIndex == 0)
                    {
                        sb.Append(" -exportQuads");
                    }
                    if (optionsWindow.cbHDRTextures.SelectedIndex == 0)
                    {
                        sb.Append(" -writeHDR");
                    }
                    if (!string.IsNullOrWhiteSpace(folderOptionTag))
                    {
                        sb.Append($" -folderOption \"{folderOptionTag}\"");
                    }
                    if (texMetadataTag == "local" || texMetadataTag == "local+global")
                    {
                        sb.Append(" -writeTexMetadata");
                    }
                }

                string arguments = sb.ToString();
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

        private void MinimizeClicked(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = cbOperation.SelectedItem as ComboBoxItem;
            if (selectedItem == null) return;
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string tag = selectedItem.Tag as string;
            if (tag == "ExtractFiles")
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "RX3 files (*.rx3)|*.rx3|All files (*.*)|*.*",
                    Title = "Select RX3 Files",
                    InitialDirectory = Directory.Exists(lastInputDir) ? lastInputDir : appDir,
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    string[] selectedFiles = openFileDialog.FileNames;
                    tbInput.Text = string.Join("; ", selectedFiles);
                    if (selectedFiles.Length > 0)
                    {
                        lastInputDir = Path.GetDirectoryName(selectedFiles[0]) ?? lastInputDir;
                        Properties.Settings.Default.LastInputDir = lastInputDir;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            else if (tag == "ExtractFolder")
            {
                var folderDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = "Select Folder Containing RX3 Files",
                    InitialDirectory = Directory.Exists(lastInputDir) ? lastInputDir : appDir,
                    DefaultDirectory = Directory.Exists(lastInputDir) ? lastInputDir : appDir
                };
                if (folderDialog.ShowDialog() == CommonFileDialogResult.Ok)
                {
                    tbInput.Text = folderDialog.FileName;
                    lastInputDir = folderDialog.FileName;
                    Properties.Settings.Default.LastInputDir = lastInputDir;
                    Properties.Settings.Default.Save();
                }
            }
            if (tag == "ImportFiles")
            {
                var extensions = new[] { "fbx", "obj", "png", "dds", "tga", "hdr" };
                string combinedPatterns = string.Join(";", extensions.Select(ext => $"*.{ext}"));
                string combinedFilter = $"All Supported Files ({combinedPatterns})|{combinedPatterns}";
                var individualFilters = extensions.Select(ext => $"{ext.ToUpper()} files (*.{ext})|*.{ext}");
                string dynamicFilter = $"{combinedFilter}|{string.Join("|", individualFilters)}|All files (*.*)|*.*";
                var openFileDialog = new OpenFileDialog
                {
                    Filter = dynamicFilter,
                    Title = "Select Files",
                    InitialDirectory = Directory.Exists(lastInputDir) ? lastInputDir : appDir,
                    Multiselect = true
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    string[] selectedFiles = openFileDialog.FileNames;
                    tbInput.Text = string.Join("; ", selectedFiles);
                    if (selectedFiles.Length > 0)
                    {
                        lastInputDir = Path.GetDirectoryName(selectedFiles[0]) ?? lastInputDir;
                        Properties.Settings.Default.LastInputDir = lastInputDir;
                        Properties.Settings.Default.Save();
                    }
                }
            }
            else if (tag == "ImportFolder")
            {
                var folderDialog = new CommonOpenFileDialog
                {
                    IsFolderPicker = true,
                    Title = "Select Folder",
                    InitialDirectory = Directory.Exists(lastInputDir) ? lastInputDir : appDir,
                    DefaultDirectory = Directory.Exists(lastInputDir) ? lastInputDir : appDir
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
                if (tag == "ExtractFiles")
                {
                    lblSelect.Content = "Select Files";
                    btnOperation.Content = "Extract";
                }
                else if (tag == "ExtractFolder")
                {
                    lblSelect.Content = "Select Folder";
                    btnOperation.Content = "Extract";
                }
                else if (tag == "ImportFiles")
                {
                    lblSelect.Content = "Select Files";
                    btnOperation.Content = "Import";
                }
                else if (tag == "ImportFolder")
                {
                    lblSelect.Content = "Select Folder";
                    btnOperation.Content = "Import";
                }
            }
        }

        private void MoreOptionsClicked(object sender, RoutedEventArgs e)
        {
            if (optionsWindow.Owner == null)
            {
                optionsWindow.Owner = this;
            }
            optionsWindow.ShowDialog();
        }
    }
}
