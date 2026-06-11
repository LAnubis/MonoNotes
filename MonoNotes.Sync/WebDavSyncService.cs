using MonoNotes.Core;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MonoNotes.Sync
{
    public class WebDavSyncService : IWebDavSyncService
    {
        private readonly IWebDavService _webDav;
        private readonly ISettingsService _settings;
        private readonly ISyncLogService _syncLog; // 🌟 注入变动账本引擎
        private readonly string _baseStorageDir;
        private bool _isSyncing = false;

        // 🌟 顶层同步名称
        private const string RemoteBaseFolder = "MonoNotes";

        public bool HasConflictsInLastSync { get; private set; } = false;

        public WebDavSyncService(IWebDavService webDav, ISettingsService settings, ISyncLogService syncLog)
        {
            _webDav = webDav;
            _settings = settings;
            _syncLog = syncLog;
            _baseStorageDir = PathHelper.GetBaseDirectory();
        }

        // ==============================================================
        // 🌟 核心新功能：获取待同步数量，用于在 UI 显示提示或红点
        // ==============================================================
        public async Task<int> GetPendingSyncCountAsync(string workspaceName)
        {
            var logs = await _syncLog.GetLogsAsync();
            return logs.Count;
        }

        // ==============================================================
        // 🌟 核心新功能：根据本地账本进行精确制导同步 (带进度回调)
        // ==============================================================
        public async Task PushLocalChangesAsync(string workspaceName, Action<int, int, string>? onProgress)
        {
            if (_isSyncing) return;

            var logs = await _syncLog.GetLogsAsync();
            if (!logs.Any()) return; // 账本为空，直接秒退，0 消耗！

            _isSyncing = true;
            HasConflictsInLastSync = false;

            try
            {
                var settings = await _settings.GetSettingsAsync();
                if (!settings.EnableWebDavSync) return;
                if (!await _webDav.TestConnectionAsync()) return;

                await _webDav.EnsureDirectoryExistsAsync(RemoteBaseFolder);
                string remoteWorkspaceFolder = $"{RemoteBaseFolder}/Workspaces/{workspaceName}";
                await _webDav.EnsureDirectoryExistsAsync(remoteWorkspaceFolder);

                int total = logs.Count;
                int current = 0;

                string localWorkspaceDir = Path.Combine(_baseStorageDir, "Workspaces", workspaceName);

                foreach (var log in logs)
                {
                    current++;
                    // 裁剪过长的文件名用于在进度条显示
                    string safePathForDisplay = log.RelativePath.Length > 25 ? "..." + log.RelativePath.Substring(log.RelativePath.Length - 25) : log.RelativePath;

                    // 🚀 触发回调，通知 UI 进度条更新！
                    onProgress?.Invoke(current, total, $"正在同步: {safePathForDisplay}");

                    string remoteFilePath = $"{remoteWorkspaceFolder}/{log.RelativePath}";
                    string localFilePath = Path.Combine(localWorkspaceDir, log.RelativePath);

                    try
                    {
                        if (log.Action == SyncActionType.Delete)
                        {
                            await _webDav.DeleteItemAsync(remoteFilePath);
                        }
                        else if (log.Action == SyncActionType.Upsert)
                        {
                            var localF = new FileInfo(localFilePath);
                            if (!localF.Exists) continue; // 本地文件丢失，跳过

                            // 🔍 冲突检测：获取云端该文件的信息
                            string remoteDir = remoteFilePath.Substring(0, remoteFilePath.LastIndexOf('/'));
                            await _webDav.EnsureDirectoryExistsAsync(remoteDir);
                            var remoteItems = await _webDav.GetRemoteItemsAsync(remoteDir);
                            var remoteF = remoteItems.FirstOrDefault(r => r.Name == Path.GetFileName(remoteFilePath));

                            if (remoteF != null)
                            {
                                // 🌟 冲突判定：如果云端修改时间 大于 本地修改时间，说明其他端改过！
                                if (remoteF.LastModified.UtcDateTime > localF.LastWriteTimeUtc.AddSeconds(2))
                                {
                                    HasConflictsInLastSync = true;
                                    var ext = Path.GetExtension(localF.Name);
                                    var baseName = Path.GetFileNameWithoutExtension(localF.Name);
                                    var conflictFileName = $"{baseName} (冲突副本 {DateTime.Now:MM-dd HHmm}){ext}";
                                    var conflictFilePath = Path.Combine(localF.DirectoryName!, conflictFileName);

                                    // 1. 将本地修改妥善保护起来
                                    File.Move(localFilePath, conflictFilePath);
                                    // 2. 把云端最新的版本下载下来作为主版本
                                    await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                                    try { File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime); } catch { }
                                }
                                else
                                {
                                    // 本地较新，正常上传覆盖
                                    await _webDav.UploadFileAsync(localFilePath, remoteFilePath);
                                }
                            }
                            else
                            {
                                // 云端没有，直接上传
                                await _webDav.UploadFileAsync(localFilePath, remoteFilePath);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[同步单文件失败] {log.RelativePath}: {ex.Message}");
                    }
                }

                // 🌟 同步顺利完成，清空变动账本！
                await _syncLog.ClearLogsAsync();

                settings.LastSyncTimeUtc = DateTimeOffset.UtcNow;
                await _settings.SaveSettingsAsync(settings);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        // ==============================================================
        // 🌟 旧的全量拉取机制 (保留用于首次初始化或强制校验)
        // ==============================================================
        public async Task SyncAsync(bool isLocalChanged = false)
        {
            if (_isSyncing) return;
            _isSyncing = true;
            HasConflictsInLastSync = false;

            try
            {
                var settings = await _settings.GetSettingsAsync();
                if (!settings.EnableWebDavSync) return;

                // 由于使用了日志引擎，全量同步时不再依赖旧的 TombstoneManager 判断
                if (!await _webDav.TestConnectionAsync()) return;

                await _webDav.EnsureDirectoryExistsAsync(RemoteBaseFolder);

                string[] syncTargets = { "Workspaces", "Writespaces" };

                foreach (var target in syncTargets)
                {
                    await _webDav.EnsureDirectoryExistsAsync($"{RemoteBaseFolder}/{target}");
                    await SyncDirectoryRecursiveAsync(target, settings);
                }

                await SyncFileAsync("app-settings.json", settings);

                settings.LastSyncTimeUtc = DateTimeOffset.UtcNow;
                await _settings.SaveSettingsAsync(settings);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task SyncFileAsync(string fileName, Core.Models.AppSettings settings)
        {
            var localPath = Path.Combine(_baseStorageDir, fileName);
            var remotePath = $"{RemoteBaseFolder}/{fileName}";

            var remoteItems = await _webDav.GetRemoteItemsAsync(RemoteBaseFolder);
            var remoteF = remoteItems.FirstOrDefault(r => !r.IsFolder && r.Name == fileName);
            var localF = File.Exists(localPath) ? new FileInfo(localPath) : null;

            if (localF == null && remoteF != null)
            {
                await _webDav.DownloadFileAsync(remotePath, localPath);
                return;
            }

            if (localF != null && remoteF == null)
            {
                await _webDav.UploadFileAsync(localPath, remotePath);
                return;
            }

            if (localF != null && remoteF != null)
            {
                bool localChanged = localF.LastWriteTimeUtc > settings.LastSyncTimeUtc.AddSeconds(2);
                bool remoteChanged = remoteF.LastModified.UtcDateTime > settings.LastSyncTimeUtc.AddSeconds(2);

                if (remoteChanged && !localChanged)
                {
                    await _webDav.DownloadFileAsync(remotePath, localPath);
                }
                else if (localChanged && !remoteChanged)
                {
                    await _webDav.UploadFileAsync(localPath, remotePath);
                }
                else if (localChanged && remoteChanged)
                {
                    if (remoteF.LastModified.UtcDateTime > localF.LastWriteTimeUtc)
                        await _webDav.DownloadFileAsync(remotePath, localPath);
                    else
                        await _webDav.UploadFileAsync(localPath, remotePath);
                }
            }
        }

        private async Task SyncDirectoryRecursiveAsync(string relativePath, Core.Models.AppSettings settings, int depth = 0)
        {
            if (depth > 20) return;

            var localDir = Path.Combine(_baseStorageDir, relativePath);
            if (!Directory.Exists(localDir)) Directory.CreateDirectory(localDir);

            var currentRemoteDir = string.IsNullOrEmpty(relativePath) ? RemoteBaseFolder : $"{RemoteBaseFolder}/{relativePath}";
            var remoteItems = await _webDav.GetRemoteItemsAsync(currentRemoteDir);

            var localDirs = Directory.GetDirectories(localDir).Select(d => new DirectoryInfo(d)).ToList();
            var localFiles = Directory.GetFiles(localDir).Select(f => new FileInfo(f)).ToList();

            localFiles = localFiles.Where(f =>
                !f.Name.Contains("冲突副本") &&
                f.Name != "note-index.json" &&
                f.Name != "work-index.json" &&
                f.Name != "workspace.txt" &&
                f.Name != ".tombstones.json" &&
                !f.Name.StartsWith(".DS_Store") &&
                f.Name != ".sync-log.json" // 🌟 全量拉取时忽略本地日志文件
            ).ToList();

            var allFileNames = localFiles.Select(f => f.Name)
                .Union(remoteItems.Where(r =>
                    !r.IsFolder &&
                    r.Name != "note-index.json" &&
                    r.Name != "work-index.json" &&
                    r.Name != "workspace.txt" &&
                    r.Name != ".tombstones.json" &&
                    !r.Name.StartsWith(".DS_Store") &&
                    r.Name != ".sync-log.json"
                ).Select(r => r.Name))
                .Distinct();

            foreach (var fileName in allFileNames)
            {
                try
                {
                    var localF = localFiles.FirstOrDefault(l => l.Name == fileName);
                    var remoteF = remoteItems.FirstOrDefault(r => !r.IsFolder && r.Name == fileName);

                    var remoteFilePath = string.IsNullOrEmpty(relativePath) ? $"{RemoteBaseFolder}/{fileName}" : $"{RemoteBaseFolder}/{relativePath}/{fileName}";
                    var localFilePath = Path.Combine(localDir, fileName);

                    if (remoteF == null && localF != null)
                    {
                        await _webDav.UploadFileAsync(localFilePath, remoteFilePath);
                        continue;
                    }

                    if (localF == null && remoteF != null)
                    {
                        await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                        try { File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime); } catch { }
                        continue;
                    }

                    if (localF != null && remoteF != null)
                    {
                        bool localChanged = localF.LastWriteTimeUtc > settings.LastSyncTimeUtc.AddSeconds(2);
                        bool remoteChanged = remoteF.LastModified.UtcDateTime > settings.LastSyncTimeUtc.AddSeconds(2);

                        if (localChanged && remoteChanged)
                        {
                            HasConflictsInLastSync = true;
                            var ext = Path.GetExtension(localF.Name);
                            var baseName = Path.GetFileNameWithoutExtension(localF.Name);
                            var conflictFileName = $"{baseName} (冲突副本 {DateTime.Now:MM-dd HHmm}){ext}";
                            var conflictFilePath = Path.Combine(localDir, conflictFileName);

                            File.Move(localFilePath, conflictFilePath);
                            await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                            try { File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime); } catch { }
                        }
                        else if (localChanged && !remoteChanged)
                        {
                            await _webDav.UploadFileAsync(localFilePath, remoteFilePath);
                        }
                        else if (!localChanged && remoteChanged)
                        {
                            await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                            try { File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime); } catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[全量同步警告] 文件 {fileName} 同步失败: {ex.Message}");
                }
            }

            var allDirNames = localDirs.Select(d => d.Name)
                .Union(remoteItems.Where(r => r.IsFolder).Select(r => r.Name))
                .Distinct();

            foreach (var dirName in allDirNames)
            {
                if (string.IsNullOrEmpty(relativePath) && dirName == "Backups") continue;
                if (dirName.StartsWith(".") && dirName != ".assets" && dirName != ".templates") continue;

                var remoteDirPath = string.IsNullOrEmpty(relativePath) ? $"{RemoteBaseFolder}/{dirName}" : $"{RemoteBaseFolder}/{relativePath}/{dirName}";
                var remoteD = remoteItems.FirstOrDefault(r => r.IsFolder && r.Name == dirName);

                if (remoteD == null) await _webDav.EnsureDirectoryExistsAsync(remoteDirPath);

                var nextRelativePath = string.IsNullOrEmpty(relativePath) ? dirName : $"{relativePath}/{dirName}";
                await SyncDirectoryRecursiveAsync(nextRelativePath, settings, depth + 1);
            }
        }
    }
}