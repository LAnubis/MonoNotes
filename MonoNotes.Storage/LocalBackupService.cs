using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;

namespace MonoNotes.Storage
{
    public class BackupFileInfo
    {
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public DateTime CreationTime { get; set; }
        public double SizeMB { get; set; }
    }

    public class LocalBackupService
    {
        private readonly ISettingsService _settingsService;
        private readonly string _monoNotesRoot;
        private readonly string _writeSpacesPath;
        private readonly string _workspacesPath;
        private readonly string _backupPath;

        public LocalBackupService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            _monoNotesRoot = Path.Combine(myDocs, "MonoNotes");

            _writeSpacesPath = Path.Combine(_monoNotesRoot, "Writespaces");
            _workspacesPath = Path.Combine(_monoNotesRoot, "Workspaces");

            _backupPath = Path.Combine(_monoNotesRoot, "Backups");

            if (!Directory.Exists(_backupPath)) Directory.CreateDirectory(_backupPath);
        }

        public List<BackupFileInfo> GetBackupFiles()
        {
            if (!Directory.Exists(_backupPath)) return new List<BackupFileInfo>();

            // 🌟 修正：匹配 MonoNotes_Backup_
            return new DirectoryInfo(_backupPath)
                .GetFiles("MonoNotes_Backup_*.zip")
                .OrderByDescending(f => f.CreationTime)
                .Select(f => new BackupFileInfo
                {
                    FileName = f.Name,
                    FilePath = f.FullName,
                    CreationTime = f.CreationTime,
                    SizeMB = Math.Round(f.Length / 1024.0 / 1024.0, 2)
                }).ToList();
        }

        public async Task<bool> ExecuteManualBackupAsync()
        {
            var settings = await _settingsService.GetSettingsAsync();
            return await CreateZipArchiveAsync(settings.MaxBackupCount);
        }

        public async Task<bool> CheckAndRunAutoBackupAsync()
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (!settings.EnableAutoBackup) return false;
            if (DateTime.Now.DayOfWeek != settings.BackupDayOfWeek) return false;

            var todayString = DateTime.Now.ToString("yyyyMMdd");
            // 🌟 修正：匹配 MonoNotes_Backup_
            var existingToday = Directory.GetFiles(_backupPath, $"MonoNotes_Backup_{todayString}*.zip").Any();
            if (existingToday) return false;

            return await CreateZipArchiveAsync(settings.MaxBackupCount);
        }

        public async Task<bool> RestoreFromBackupAsync(string zipFilePath)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (!File.Exists(zipFilePath)) return false;

                    // 1. 强制备份当前状态（作为后悔药）
                    string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    // 🌟 修正：命名 MonoNotes_Backup_PreRestore_
                    string preRestoreZip = Path.Combine(_backupPath, $"MonoNotes_Backup_PreRestore_{timeStamp}.zip");
                    CreateZipArchiveInternal(preRestoreZip);

                    // 2. 物理清空当前的 Writespaces 和 Workspaces 目录
                    ClearDirectory(_writeSpacesPath);
                    ClearDirectory(_workspacesPath);

                    // 3. 解压覆盖到 MonoNotes 根目录
                    ZipFile.ExtractToDirectory(zipFilePath, _monoNotesRoot, overwriteFiles: true);

                    // 🌟 4. 强制清理索引缓存，迫使软件重启后“自愈”
                    var indexFile = Path.Combine(_monoNotesRoot, "note-index.json"); // 假设这是你的笔记索引名
                    if (File.Exists(indexFile)) File.Delete(indexFile);

                    // 小说索引也可以清理
                    var worksDir = Path.Combine(_monoNotesRoot, "Writespaces");
                    foreach (var dir in Directory.GetDirectories(worksDir))
                    {
                        var workIndex = Path.Combine(dir, "work-index.json");
                        if (File.Exists(workIndex)) File.Delete(workIndex);
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"恢复失败: {ex.Message}");
                    return false;
                }
            });
        }

        private async Task<bool> CreateZipArchiveAsync(int maxRetainCount)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    // 🌟 修正：命名 MonoNotes_Backup_
                    string zipFileName = $"MonoNotes_Backup_{timeStamp}.zip";
                    string zipFilePath = Path.Combine(_backupPath, zipFileName);

                    CreateZipArchiveInternal(zipFilePath);
                    EnforceRetentionPolicy(maxRetainCount);

                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"灾备打包失败: {ex.Message}");
                    return false;
                }
            });
        }

        private void CreateZipArchiveInternal(string zipFilePath)
        {
            using (var archive = ZipFile.Open(zipFilePath, ZipArchiveMode.Create))
            {
                AddDirectoryToArchive(archive, _writeSpacesPath, "Writespaces");
                AddDirectoryToArchive(archive, _workspacesPath, "Workspaces");
            }
        }

        private void AddDirectoryToArchive(ZipArchive archive, string sourceDir, string entryRoot)
        {
            if (!Directory.Exists(sourceDir)) return;

            var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var relativePath = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var zipEntryName = Path.Combine(entryRoot, relativePath).Replace("\\", "/");

                var entry = archive.CreateEntry(zipEntryName, CompressionLevel.Optimal);
                try
                {
                    using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    using (var entryStream = entry.Open())
                    {
                        fs.CopyTo(entryStream);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"灾备引擎跳过锁定文件 {file}: {ex.Message}");
                }
            }
        }

        private void ClearDirectory(string dirPath)
        {
            if (!Directory.Exists(dirPath)) return;
            var di = new DirectoryInfo(dirPath);
            foreach (FileInfo file in di.GetFiles()) { try { file.Delete(); } catch { } }
            foreach (DirectoryInfo dir in di.GetDirectories()) { try { dir.Delete(true); } catch { } }
        }

        private void EnforceRetentionPolicy(int maxCount)
        {
            if (maxCount <= 0) maxCount = 3;

            // 🌟 修正：匹配 MonoNotes_Backup_
            var backupFiles = new DirectoryInfo(_backupPath)
                .GetFiles("MonoNotes_Backup_*.zip")
                .Where(f => !f.Name.Contains("PreRestore"))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (backupFiles.Count > maxCount)
            {
                var filesToDelete = backupFiles.Skip(maxCount);
                foreach (var file in filesToDelete)
                {
                    try { file.Delete(); } catch { }
                }
            }
        }
    }
}