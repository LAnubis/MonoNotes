using MonoNotes.Core;
using MonoNotes.Core.Interfaces;
using MonoNotes.Storage;
using System.Text.RegularExpressions;

namespace MonoNotes.Sync
{
    public class WebDavSyncService : IWebDavSyncService
    {
        private readonly IWebDavService _webDav;
        private readonly ISettingsService _settings;
        private readonly string _baseStorageDir;
        private bool _isSyncing = false;

        // 🌟 顶层同步名称
        private const string RemoteBaseFolder = "MonoNotes";

        public bool HasConflictsInLastSync { get; private set; } = false;

        public WebDavSyncService(IWebDavService webDav, ISettingsService settings)
        {
            _webDav = webDav;
            _settings = settings;
            // 🌟 将同步根目录提升到绝对顶层
            _baseStorageDir = PathHelper.GetBaseDirectory();
        }

        public async Task SyncAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            HasConflictsInLastSync = false;

            try
            {
                var settings = await _settings.GetSettingsAsync();

                // 1. 网络与开关校验
                if (!settings.EnableWebDavSync) return;
                if (!await _webDav.TestConnectionAsync()) return;

                await _webDav.EnsureDirectoryExistsAsync(RemoteBaseFolder);

                // 2. 第一阶段：清扫墓碑（确保所有离线删除在同步前生效）
                await ProcessTombstonesAsync();

                // 3. 第二阶段：分层同步
                // 列表定义了我们需要覆盖的所有核心业务范围
                string[] syncTargets = { "Workspaces", "Writespaces" };

                foreach (var target in syncTargets)
                {
                    // 确保云端也有对应的文件夹
                    await _webDav.EnsureDirectoryExistsAsync($"{RemoteBaseFolder}/{target}");
                    // 启动该文件夹下的递归同步
                    await SyncDirectoryRecursiveAsync(target, settings);
                }

                // 4. 第三阶段：同步全局配置文件 (settings.json)
                // 确保在一台电脑改了设置，另一台立刻生效
                await SyncFileAsync("app-settings.json", settings);

                settings.LastSyncTimeUtc = DateTimeOffset.UtcNow;
                await _settings.SaveSettingsAsync(settings);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        // 🌟 辅助方法：处理根目录下的单个文件同步（例如 settings.json）
        private async Task SyncFileAsync(string fileName, Core.Models.AppSettings settings)
        {
            var localPath = Path.Combine(_baseStorageDir, fileName);
            var remotePath = $"{RemoteBaseFolder}/{fileName}";

            if (!File.Exists(localPath)) return;

            // 这里复用你已有的文件对比逻辑或者简单的上传覆盖
            // 如果想要更细致的冲突处理，可以复用 SyncDirectoryRecursiveAsync 里的逻辑
            await _webDav.UploadFileAsync(localPath, remotePath);
        }

        private async Task ProcessTombstonesAsync()
        {
            var tombstones = TombstoneManager.GetAll();
            var toRemove = new List<string>();

            foreach (var t in tombstones)
            {
                var remotePath = $"{RemoteBaseFolder}/{t}";
                // 向云端发送 Delete 信号
                bool success = await _webDav.DeleteItemAsync(remotePath);

                // 如果云端成功删除，或者文件本来就不存在（404），则视为成功，移除该墓碑
                if (success)
                {
                    toRemove.Add(t);
                }
            }

            foreach (var t in toRemove) TombstoneManager.Remove(t);
        }

        private async Task SyncDirectoryRecursiveAsync(string relativePath, Core.Models.AppSettings settings, int depth = 0)
        {
            if (depth > 20)
            {
                System.Diagnostics.Debug.WriteLine($"⚠️ [熔断] 目录嵌套过深，跳过同步: {relativePath}");
                return;
            }
            var localDir = Path.Combine(_baseStorageDir, relativePath);
            if (!Directory.Exists(localDir)) Directory.CreateDirectory(localDir);

            var currentRemoteDir = string.IsNullOrEmpty(relativePath) ? RemoteBaseFolder : $"{RemoteBaseFolder}/{relativePath}";
            var remoteItems = await _webDav.GetRemoteItemsAsync(currentRemoteDir);

            var localDirs = Directory.GetDirectories(localDir).Select(d => new DirectoryInfo(d)).ToList();
            var localFiles = Directory.GetFiles(localDir).Select(f => new FileInfo(f)).ToList();

            // 🚫 精准黑名单过滤
            localFiles = localFiles.Where(f =>
                !f.Name.Contains("冲突副本") &&
                f.Name != "note-index.json" &&
                f.Name != "work-index.json" &&
                f.Name != "workspace.txt" &&
                f.Name != ".tombstones.json" &&
                !f.Name.StartsWith(".DS_Store")
            ).ToList();

            var allFileNames = localFiles.Select(f => f.Name)
                .Union(remoteItems.Where(r =>
                    !r.IsFolder &&
                    r.Name != "note-index.json" &&
                    r.Name != "work-index.json" &&
                    r.Name != "workspace.txt" &&
                    r.Name != ".tombstones.json" &&
                    !r.Name.StartsWith(".DS_Store")
                ).Select(r => r.Name))
                .Distinct();

            // === 🔄 文件同步与冲突检测 ===
            foreach (var fileName in allFileNames)
            {
                var localF = localFiles.FirstOrDefault(l => l.Name == fileName);
                var remoteF = remoteItems.FirstOrDefault(r => !r.IsFolder && r.Name == fileName);

                var remoteFilePath = string.IsNullOrEmpty(relativePath) ? $"{RemoteBaseFolder}/{fileName}" : $"{RemoteBaseFolder}/{relativePath}/{fileName}";
                var localFilePath = Path.Combine(localDir, fileName);

                // 🌟 无差别扩展名同步，涵盖 project.json, settings.json 等
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
                        // 保留扩展名，追加冲突时间戳
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

            // === 📁 文件夹递归逻辑 ===
            var allDirNames = localDirs.Select(d => d.Name)
                .Union(remoteItems.Where(r => r.IsFolder).Select(r => r.Name))
                .Distinct();

            foreach (var dirName in allDirNames)
            {
                // 🚫 绝对隔离本地灾备文件夹
                if (string.IsNullOrEmpty(relativePath) && dirName == "Backups") continue;

                // 🚫 屏蔽非业务的隐藏文件夹
                if (dirName.StartsWith(".") && dirName != ".assets" && dirName != ".templates") continue;

                var remoteDirPath = string.IsNullOrEmpty(relativePath) ? $"{RemoteBaseFolder}/{dirName}" : $"{RemoteBaseFolder}/{relativePath}/{dirName}";
                var remoteD = remoteItems.FirstOrDefault(r => r.IsFolder && r.Name == dirName);

                if (remoteD == null) await _webDav.EnsureDirectoryExistsAsync(remoteDirPath);

                var nextRelativePath = string.IsNullOrEmpty(relativePath) ? dirName : $"{relativePath}/{dirName}";
                await SyncDirectoryRecursiveAsync(nextRelativePath, settings);
                depth++;
            }
        }
    }
}