using MonoNotes.Core;
using MonoNotes.Core.Interfaces;
using System.Text.RegularExpressions;

namespace MonoNotes.Sync
{
    public class WebDavSyncService : IWebDavSyncService
    {
        private readonly IWebDavService _webDav;
        private readonly ISettingsService _settings;
        private readonly string _baseStorageDir;
        private bool _isSyncing = false;

        private const string RemoteBaseFolder = "MonoNotes";

        public bool HasConflictsInLastSync { get; private set; } = false;

        public WebDavSyncService(IWebDavService webDav, ISettingsService settings)
        {
            _webDav = webDav;
            _settings = settings;
            _baseStorageDir = PathHelper.GetWorkspacesDirectory();
        }

        public async Task SyncAsync()
        {
            if (_isSyncing) return;
            _isSyncing = true;
            HasConflictsInLastSync = false;

            try
            {
                var settings = await _settings.GetSettingsAsync();

                if (!await _webDav.TestConnectionAsync()) return;

                if (!settings.EnableWebDavSync)
                {
                    return;
                }

                await _webDav.EnsureDirectoryExistsAsync(RemoteBaseFolder);

                // 启动递归双向同步
                await SyncDirectoryRecursiveAsync("", settings);

                settings.LastSyncTimeUtc = DateTimeOffset.UtcNow;
                await _settings.SaveSettingsAsync(settings);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task SyncDirectoryRecursiveAsync(string relativePath, Core.Models.AppSettings settings)
        {
            var localDir = Path.Combine(_baseStorageDir, relativePath);
            if (!Directory.Exists(localDir)) Directory.CreateDirectory(localDir);

            var currentRemoteDir = string.IsNullOrEmpty(relativePath) ? RemoteBaseFolder : $"{RemoteBaseFolder}/{relativePath}";
            var remoteItems = await _webDav.GetRemoteItemsAsync(currentRemoteDir);

            var localDirs = Directory.GetDirectories(localDir).Select(d => new DirectoryInfo(d)).ToList();
            var localFiles = Directory.GetFiles(localDir).Select(f => new FileInfo(f)).ToList();

            // 过滤掉本身就是冲突副本的文件
            //localFiles = localFiles.Where(f => !f.Name.Contains("冲突副本")).ToList();

            //var allFileNames = localFiles.Select(f => f.Name)
            //    .Union(remoteItems.Where(r => !r.IsFolder).Select(r => r.Name))
            //    .Distinct();

            localFiles = localFiles.Where(f =>
                !f.Name.Contains("冲突副本") &&
                f.Name != "index.json" &&           // 🚫 绝对不同步本地索引字典
                !f.Name.StartsWith(".") &&          // 🚫 屏蔽 Mac 的 .DS_Store 等隐藏文件
                f.Extension.ToLower() == ".md"      // ✅ 强制只同步 Markdown 笔记文件
            ).ToList();

            var allFileNames = localFiles.Select(f => f.Name)
                .Union(remoteItems.Where(r =>
                    !r.IsFolder &&
                    r.Name != "index.json" &&
                    !r.Name.StartsWith(".") &&
                    r.Name.EndsWith(".md", StringComparison.OrdinalIgnoreCase)
                ).Select(r => r.Name))
                .Distinct();

            // === 🔄 文件同步与冲突检测 ===
            foreach (var fileName in allFileNames)
            {
                var localF = localFiles.FirstOrDefault(l => l.Name == fileName);
                var remoteF = remoteItems.FirstOrDefault(r => !r.IsFolder && r.Name == fileName);

                var remoteFilePath = string.IsNullOrEmpty(relativePath) ? $"{RemoteBaseFolder}/{fileName}" : $"{RemoteBaseFolder}/{relativePath}/{fileName}";
                var localFilePath = Path.Combine(localDir, fileName);

                // 🌟 修复 1：只有本地有 -> 无脑上传补齐云端（去除过于严格的时间校验）
                if (remoteF == null && localF != null)
                {
                    await _webDav.UploadFileAsync(localFilePath, remoteFilePath);
                    continue;
                }

                // 🌟 修复 2：只有云端有 -> 无脑下载拉回本地
                if (localF == null && remoteF != null)
                {
                    await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                    File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime);
                    continue;
                }

                // 3. 两端都有 -> 严格判定冲突
                if (localF != null && remoteF != null)
                {
                    bool localChanged = localF.LastWriteTimeUtc > settings.LastSyncTimeUtc.AddSeconds(2);
                    bool remoteChanged = remoteF.LastModified.UtcDateTime > settings.LastSyncTimeUtc.AddSeconds(2);

                    if (localChanged && remoteChanged)
                    {
                        HasConflictsInLastSync = true;
                        var conflictFileName = localF.Name.Replace(".md", $" (冲突副本 {DateTime.Now:MM-dd HHmm}).md");
                        var conflictFilePath = Path.Combine(localDir, conflictFileName);

                        File.Move(localFilePath, conflictFilePath);
                        await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                        File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime);
                    }
                    else if (localChanged && !remoteChanged)
                    {
                        await _webDav.UploadFileAsync(localFilePath, remoteFilePath);
                    }
                    else if (!localChanged && remoteChanged)
                    {
                        await _webDav.DownloadFileAsync(remoteFilePath, localFilePath);
                        File.SetLastWriteTimeUtc(localFilePath, remoteF.LastModified.UtcDateTime);
                    }
                }
            }

            // === 📁 文件夹递归逻辑 ===
            var allDirNames = localDirs.Select(d => d.Name)
                .Union(remoteItems.Where(r => r.IsFolder).Select(r => r.Name))
                .Distinct();

            foreach (var dirName in allDirNames)
            {
                // 🌟 修复 3：放行 .assets 文件夹！只屏蔽其他隐藏垃圾文件
                if (dirName.StartsWith(".") && dirName != ".assets" && dirName != ".templates") continue;

                var remoteDirPath = string.IsNullOrEmpty(relativePath) ? $"{RemoteBaseFolder}/{dirName}" : $"{RemoteBaseFolder}/{relativePath}/{dirName}";
                var remoteD = remoteItems.FirstOrDefault(r => r.IsFolder && r.Name == dirName);

                if (remoteD == null) await _webDav.EnsureDirectoryExistsAsync(remoteDirPath);

                var nextRelativePath = string.IsNullOrEmpty(relativePath) ? dirName : $"{relativePath}/{dirName}";
                await SyncDirectoryRecursiveAsync(nextRelativePath, settings);
            }
        }
    }
}