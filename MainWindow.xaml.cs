using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;

namespace PesPluginLoaderInstaller
{
    public partial class MainWindow : Window
    {
        const string ForumUrl = "https://soccergaming.com/forums/";

        const string ArchiveLoader = "PesPluginLoaderInstaller.plugins.bar";
        const string ArchiveAssetLoader = "PesPluginLoaderInstaller.plugins_assetloader.bar";
        const string ArchiveOtherPlugins = "PesPluginLoaderInstaller.plugins_other.bar";
        static readonly string[] AllArchives = { ArchiveLoader, ArchiveAssetLoader, ArchiveOtherPlugins };
        const string VsTemplateResource = "PesPluginLoaderInstaller.vs_template.zip";
        const string VsTemplateFileName = "PES (Pro Evolution Soccer) Plugin (32-bit).zip";

        // Registry keys in the order they are listed: PES 6 > PES 5 > WE 9
        static readonly string[] GameRegistryKeys =
        {
            "SOFTWARE\\KONAMIPES6\\PES6",
            "SOFTWARE\\KONAMIPES5\\PES5",
            "SOFTWARE\\KONAMIWE9\\WE9",
        };

        // Supported executables and their sizes (the 4GB fix doesn't change the size)
        static readonly Dictionary<string, long> SupportedExes = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase)
        {
            { "pes6.exe", 21880832 },
            { "PES5.exe", 10174464 },
            { "WE9.exe", 10174464 },
        };

        const ushort IMAGE_FILE_LARGE_ADDRESS_AWARE = 0x0020;

        bool initialized = false;
        readonly List<string> installedFiles;
        readonly List<string> vsTemplateFolders;

        public MainWindow()
        {
            InitializeComponent();
            Version version = Assembly.GetExecutingAssembly().GetName().Version;
            lblVersion.Text = $"{version.Major}.{version.Minor}";
            installedFiles = AllArchives.SelectMany(GetArchiveFiles).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (string gameDir in FindGameFolders())
            {
                cbDirectory.Items.Add(gameDir);
            }
            vsTemplateFolders = FindVsTemplateFolders();
            if (vsTemplateFolders.Count > 0)
            {
                // e.g. "Install Visual Studio project template (VS 2022, VS 2019)"
                string versions = string.Join(", ", vsTemplateFolders.Select(dir =>
                    VsFolderYear(Path.GetDirectoryName(Path.GetDirectoryName(dir)))));
                chkVsTemplate.Content = $"{Resource.TEXT_INSTALL_VS_TEMPLATE} (VS {versions})";
                chkVsTemplate.IsEnabled = true;
            }
            initialized = true;
            if (cbDirectory.Items.Count > 0)
            {
                cbDirectory.SelectedIndex = 0;
            }
            CheckInstallationDir();
        }

        string GameDir => cbDirectory.Text.Trim();

        static IEnumerable<string> FindGameFolders()
        {
            var result = new List<string>();
            foreach (string keyPath in GameRegistryKeys)
            {
                foreach (RegistryView view in new[] { RegistryView.Registry32, RegistryView.Registry64 })
                {
                    try
                    {
                        using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                        using (RegistryKey key = baseKey.OpenSubKey(keyPath))
                        {
                            string dir = key?.GetValue("installdir") as string;
                            if (string.IsNullOrWhiteSpace(dir))
                                continue;
                            dir = dir.Trim().TrimEnd('\\', '/');
                            if (IsCorrectGameFolder(dir) && !result.Contains(dir, StringComparer.OrdinalIgnoreCase))
                                result.Add(dir);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return result;
        }

        // User project template folders of every Visual Studio version that was run on this PC,
        // e.g. Documents\Visual Studio 2022\Templates\ProjectTemplates (newest first)
        static List<string> FindVsTemplateFolders()
        {
            var result = new List<string>();
            try
            {
                string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                foreach (string vsDir in Directory.GetDirectories(documents, "Visual Studio *").OrderByDescending(VsFolderYear))
                {
                    string templatesDir = Path.Combine(vsDir, "Templates");
                    if (Directory.Exists(templatesDir))
                        result.Add(Path.Combine(templatesDir, "ProjectTemplates"));
                }
            }
            catch
            {
            }
            return result;
        }

        // "Visual Studio 2022" -> 2022; VS 2026 may name its folder after the internal version 18
        static int VsFolderYear(string vsDir)
        {
            int.TryParse(Path.GetFileName(vsDir).Substring("Visual Studio ".Length), out int version);
            return version == 18 ? 2026 : version;
        }

        void InstallVsTemplate()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (string dir in vsTemplateFolders)
            {
                Directory.CreateDirectory(dir);
                string destinationPath = Path.Combine(dir, VsTemplateFileName);
                RemoveReadOnly(destinationPath);
                using (Stream stream = assembly.GetManifestResourceStream(VsTemplateResource))
                using (var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write))
                {
                    if (stream == null)
                        throw new InvalidOperationException($"Resource '{VsTemplateResource}' not found.");
                    stream.CopyTo(file);
                }
            }
        }

        public void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        public void RemoveReadOnly(string filename)
        {
            if (System.IO.File.Exists(filename))
            {
                System.IO.File.SetAttributes(filename, FileAttributes.Normal);
            }
        }

        public void RemoveFile(string filePath)
        {
            if (System.IO.File.Exists(filePath))
            {
                RemoveReadOnly(filePath);
                System.IO.File.Delete(filePath);
            }
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

        static IEnumerable<string> GetArchiveFiles(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new InvalidOperationException($"Resource '{resourceName}' not found.");
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    return archive.Entries
                        .Where(entry => !string.IsNullOrEmpty(entry.Name))
                        .Select(entry => entry.FullName.Replace('/', Path.DirectorySeparatorChar))
                        .ToList();
                }
            }
        }

        public void UnpackArchiveFromEmbedded(string resourceName, string targetFolder)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Resource '{resourceName}' not found.");
                }
                Directory.CreateDirectory(targetFolder);
                using (var archive = new ZipArchive(stream))
                {
                    foreach (var entry in archive.Entries)
                    {
                        string destinationPath = Path.Combine(targetFolder, entry.FullName);
                        string directoryPath = Path.GetDirectoryName(destinationPath);
                        if (!string.IsNullOrEmpty(directoryPath))
                        {
                            Directory.CreateDirectory(directoryPath);
                        }
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            continue;
                        }
                        RemoveReadOnly(destinationPath);
                        entry.ExtractToFile(destinationPath, overwrite: true);
                    }
                }
            }
        }

        public void Uninstall()
        {
            foreach (string file in installedFiles)
            {
                RemoveFile(Path.Combine(GameDir, file));
            }
            RemoveFolderIfEmpty(Path.Combine(GameDir, "plugins"));
        }

        public void UninstallClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                Uninstall();
                MessageBox.Show(Resource.TEXT_UNINSTALLED, Resource.TEXT_MESSAGE);
                btnUninstall.IsEnabled = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Resource.TEXT_ERROR}:\n{ex.Message}", Resource.TEXT_ERROR, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        static bool IsExeSupported(string exePath)
        {
            return System.IO.File.Exists(exePath)
                && SupportedExes.TryGetValue(Path.GetFileName(exePath), out long size)
                && new FileInfo(exePath).Length == size;
        }

        static List<string> GetSupportedExes(string dir)
        {
            return SupportedExes.Keys
                .Select(exe => Path.Combine(dir, exe))
                .Where(IsExeSupported)
                .ToList();
        }

        // Offset of the COFF header Characteristics field, or -1 if this is not a PE file
        static long GetCharacteristicsOffset(BinaryReader reader)
        {
            Stream stream = reader.BaseStream;
            if (stream.Length < 0x40)
                return -1;
            stream.Position = 0;
            if (reader.ReadUInt16() != 0x5A4D) // "MZ"
                return -1;
            stream.Position = 0x3C;
            int peOffset = reader.ReadInt32();
            if (peOffset <= 0 || peOffset + 24 > stream.Length)
                return -1;
            stream.Position = peOffset;
            if (reader.ReadUInt32() != 0x00004550) // "PE\0\0"
                return -1;
            return peOffset + 4 + 18;
        }

        static bool Is4GBFixApplied(string exePath)
        {
            using (var stream = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream))
            {
                long offset = GetCharacteristicsOffset(reader);
                if (offset < 0)
                    return false;
                stream.Position = offset;
                return (reader.ReadUInt16() & IMAGE_FILE_LARGE_ADDRESS_AWARE) != 0;
            }
        }

        static void Apply4GBFix(string exePath)
        {
            System.IO.File.SetAttributes(exePath, System.IO.File.GetAttributes(exePath) & ~FileAttributes.ReadOnly);
            using (var stream = new FileStream(exePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var reader = new BinaryReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                long offset = GetCharacteristicsOffset(reader);
                if (offset < 0)
                    throw new InvalidDataException($"'{Path.GetFileName(exePath)}' is not a valid executable.");
                stream.Position = offset;
                ushort characteristics = reader.ReadUInt16();
                if ((characteristics & IMAGE_FILE_LARGE_ADDRESS_AWARE) != 0)
                    return;
                stream.Position = offset;
                writer.Write((ushort)(characteristics | IMAGE_FILE_LARGE_ADDRESS_AWARE));
            }
        }

        public void InstallClicked(object sender, RoutedEventArgs e)
        {
            List<string> exes = GetSupportedExes(GameDir);
            if (exes.Count == 0)
            {
                MessageBox.Show(Resource.TEXT_NOT_SUPPORTED, Resource.TEXT_MESSAGE);
                return;
            }
            try
            {
                Uninstall();
                UnpackArchiveFromEmbedded(ArchiveLoader, GameDir);
                if (chkAssetLoader.IsChecked == true)
                {
                    UnpackArchiveFromEmbedded(ArchiveAssetLoader, GameDir);
                }
                if (chkOtherPlugins.IsChecked == true)
                {
                    UnpackArchiveFromEmbedded(ArchiveOtherPlugins, GameDir);
                }
                if (chk4GBFix.IsEnabled && chk4GBFix.IsChecked == true)
                {
                    foreach (string exe in exes)
                    {
                        Apply4GBFix(exe);
                    }
                }
                if (chkVsTemplate.IsEnabled && chkVsTemplate.IsChecked == true)
                {
                    InstallVsTemplate();
                }
                MessageBox.Show(Resource.TEXT_INSTALLED, Resource.TEXT_MESSAGE);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"{Resource.TEXT_ERROR}:\n{ex.Message}", Resource.TEXT_ERROR, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            CheckInstallationDir();
        }

        public void ExitClicked(object sender, RoutedEventArgs e)
        {
            Close();
        }

        public static bool IsCorrectGameFolder(string dir)
        {
            return !string.IsNullOrWhiteSpace(dir)
                && Directory.Exists(dir)
                && Directory.Exists(Path.Combine(dir, "dat"))
                && SupportedExes.Keys.Any(exe => System.IO.File.Exists(Path.Combine(dir, exe)));
        }

        public void CheckInstallationDir()
        {
            bool correctDir = IsCorrectGameFolder(GameDir);
            bool fixAvailable = false;
            bool fixApplied = false;
            if (correctDir)
            {
                btnInstall.IsEnabled = true;
                tbErrorMessage.Visibility = Visibility.Hidden;
                btnUninstall.IsEnabled = installedFiles.Any(file => System.IO.File.Exists(Path.Combine(GameDir, file)));
                try
                {
                    List<string> exes = GetSupportedExes(GameDir);
                    fixAvailable = exes.Any(exe => !Is4GBFixApplied(exe));
                    fixApplied = exes.Count > 0 && !fixAvailable;
                }
                catch
                {
                }
            }
            else
            {
                btnInstall.IsEnabled = false;
                btnUninstall.IsEnabled = false;
                tbErrorMessage.Visibility = GameDir == "" ? Visibility.Hidden : Visibility.Visible;
            }
            chk4GBFix.IsEnabled = fixAvailable;
            if (fixApplied)
            {
                chk4GBFix.IsChecked = true;
                chk4GBFix.Content = Resource.TEXT_4GB_FIX_APPLIED;
            }
            else
            {
                chk4GBFix.Content = Resource.TEXT_APPLY_4GB_FIX;
            }
        }

        public void BtnBrowseGameDir_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CommonOpenFileDialog();
            dialog.IsFolderPicker = true;
            if (Directory.Exists(GameDir))
            {
                dialog.InitialDirectory = GameDir;
            }
            CommonFileDialogResult result = dialog.ShowDialog();
            if (result == CommonFileDialogResult.Ok)
            {
                cbDirectory.Text = dialog.FileName;
            }
            CheckInstallationDir();
        }

        public void CbDirectory_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!initialized)
                return;
            CheckInstallationDir();
        }

        void Link_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(ForumUrl)
            {
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
