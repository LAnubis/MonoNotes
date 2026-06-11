using MonoNotes.Core;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;
using System.Text;
using System.Text.RegularExpressions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace MonoNotes.App.Services
{
    public class LocalNoteRepository : INoteRepository
    {
        private readonly string _rootDirectory;
        private string _storageDirectory;
        public string CurrentWorkspace { get; private set; } = "默认库";

        private readonly ISerializer _yamlSerializer;
        private readonly IDeserializer _yamlDeserializer;

        // 🌟 内存缓存核心组件
        private List<MonoNote>? _noteCache = null;
        private readonly SemaphoreSlim _cacheLock = new SemaphoreSlim(1, 1);

        // 🌟 注入离线变动账本引擎
        private readonly ISyncLogService _syncLog;

        public LocalNoteRepository(ISyncLogService syncLog)
        {
            _syncLog = syncLog;
            _rootDirectory = PathHelper.GetBaseDirectory();
            if (!Directory.Exists(_rootDirectory)) Directory.CreateDirectory(_rootDirectory);

            var configPath = Path.Combine(_rootDirectory, "workspace.txt");
            if (File.Exists(configPath))
            {
                CurrentWorkspace = File.ReadAllText(configPath).Trim();
            }
            if (string.IsNullOrEmpty(CurrentWorkspace)) CurrentWorkspace = "默认库";

            _storageDirectory = Path.Combine(_rootDirectory, "Workspaces", CurrentWorkspace);
            EnsureDirectories();

            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        private void EnsureDirectories()
        {
            if (!Directory.Exists(_storageDirectory)) Directory.CreateDirectory(_storageDirectory);

            var notesPath = Path.Combine(_storageDirectory, "notes");
            if (!Directory.Exists(notesPath)) Directory.CreateDirectory(notesPath);

            var assetsPath = Path.Combine(_storageDirectory, ".assets");
            if (!Directory.Exists(assetsPath)) Directory.CreateDirectory(assetsPath);

            var templatesPath = Path.Combine(_storageDirectory, ".templates");
            if (!Directory.Exists(templatesPath)) Directory.CreateDirectory(templatesPath);

            try
            {
                var rootFiles = Directory.GetFiles(_storageDirectory, "*.md", SearchOption.TopDirectoryOnly);
                foreach (var file in rootFiles)
                {
                    var dest = Path.Combine(notesPath, Path.GetFileName(file));
                    if (!File.Exists(dest)) File.Move(file, dest);
                }
            }
            catch { }
        }

        public async Task<List<string>> GetAllWorkspacesAsync()
        {
            var wsPath = Path.Combine(_rootDirectory, "Workspaces");
            if (!Directory.Exists(wsPath)) Directory.CreateDirectory(wsPath);

            var dirs = Directory.GetDirectories(wsPath);
            return dirs.Select(d => new DirectoryInfo(d).Name).ToList();
        }

        public async Task SwitchWorkspaceAsync(string workspaceName)
        {
            CurrentWorkspace = workspaceName;
            _storageDirectory = Path.Combine(_rootDirectory, "Workspaces", CurrentWorkspace);
            EnsureDirectories();
            await File.WriteAllTextAsync(Path.Combine(_rootDirectory, "workspace.txt"), CurrentWorkspace);

            _noteCache = null; // 清空旧缓存，强制重载
        }

        public async Task CreateWorkspaceAsync(string workspaceName)
        {
            var newPath = Path.Combine(_rootDirectory, "Workspaces", workspaceName);
            if (!Directory.Exists(newPath)) Directory.CreateDirectory(newPath);
            await SwitchWorkspaceAsync(workspaceName);
        }

        public async Task<List<MonoNote>> GetAllNotesAsync()
        {
            if (_noteCache != null)
            {
                return _noteCache.OrderByDescending(n => n.UpdatedAt).ToList();
            }

            await _cacheLock.WaitAsync();
            try
            {
                if (_noteCache != null)
                {
                    return _noteCache.OrderByDescending(n => n.UpdatedAt).ToList();
                }

                var notes = new List<MonoNote>();
                var files = Directory.GetFiles(_storageDirectory, "*.md", SearchOption.AllDirectories)
                                     .Where(f => !f.Replace('\\', '/').Contains("/."))
                                     .ToList();

                foreach (var file in files)
                {
                    try
                    {
                        var text = await File.ReadAllTextAsync(file);
                        if (text.StartsWith("---"))
                        {
                            var parts = text.Split(new[] { "---" }, 3, StringSplitOptions.None);
                            if (parts.Length >= 3)
                            {
                                var yaml = parts[1];
                                var content = parts[2].TrimStart('\r', '\n');
                                content = content.Replace("./.assets/", $"{AppConstants.ImageBaseUrl}/Workspaces/{CurrentWorkspace}/.assets/");

                                var meta = _yamlDeserializer.Deserialize<NoteMetadata>(yaml);
                                var fileDir = Path.GetDirectoryName(file) ?? _storageDirectory;
                                var relativePath = Path.GetRelativePath(_storageDirectory, fileDir).Replace("\\", "/");

                                if (relativePath == ".") relativePath = "notes";

                                notes.Add(new MonoNote
                                {
                                    Id = meta.Id ?? Path.GetFileNameWithoutExtension(file),
                                    Title = meta.Title ?? "无标题笔记",
                                    Tags = meta.Tags ?? new List<string>(),
                                    UpdatedAt = meta.UpdatedAt != default ? meta.UpdatedAt : File.GetLastWriteTime(file),
                                    Content = content,
                                    Folder = relativePath,
                                    IsPinned = meta.IsPinned,
                                    IsFavorite = meta.IsFavorite,
                                    IsArchived = meta.IsArchived,
                                    IsDeleted = meta.IsDeleted
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[加载笔记失败] 文件: {file}, 错误: {ex.Message}");
                    }
                }

                _noteCache = notes;
                return _noteCache.OrderByDescending(n => n.UpdatedAt).ToList();
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        public async Task SaveNoteAsync(MonoNote note)
        {
            note.UpdatedAt = DateTimeOffset.Now;

            var targetFolder = string.IsNullOrWhiteSpace(note.Folder) ? "notes" : note.Folder;
            var targetDirectory = Path.Combine(_storageDirectory, targetFolder);

            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            var filePath = Path.Combine(targetDirectory, $"{note.Id}.md");

            var meta = new NoteMetadata
            {
                Id = note.Id,
                Title = note.Title ?? "无标题笔记",
                UpdatedAt = note.UpdatedAt,
                Tags = note.Tags ?? new List<string>(),
                IsPinned = note.IsPinned,
                IsFavorite = note.IsFavorite,
                IsArchived = note.IsArchived,
                IsDeleted = note.IsDeleted
            };

            var yaml = _yamlSerializer.Serialize(meta);
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.Append(yaml);
            sb.AppendLine("---");

            var pureContent = note.Content?.Replace($"{AppConstants.ImageBaseUrl}/Workspaces/{CurrentWorkspace}/", "./") ?? string.Empty;
            sb.AppendLine(pureContent);

            await File.WriteAllTextAsync(filePath, sb.ToString());

            // 🌟 更新内存缓存
            if (_noteCache != null)
            {
                var existing = _noteCache.FirstOrDefault(n => n.Id == note.Id);
                if (existing != null) _noteCache.Remove(existing);
                _noteCache.Add(note);
            }

            // 🌟 变动账本埋点：记录笔记的新增或修改 (Upsert)
            string relativePath = $"Workspaces/{CurrentWorkspace}/{targetFolder}/{note.Id}.md";
            await _syncLog.LogChangeAsync(relativePath, SyncActionType.Upsert);
        }

        public async Task<List<string>> GetAllFoldersAsync()
        {
            var directories = Directory.GetDirectories(_storageDirectory, "*", SearchOption.AllDirectories);
            var folderPaths = new List<string> { "notes" };

            foreach (var dir in directories)
            {
                var relativePath = Path.GetRelativePath(_storageDirectory, dir).Replace("\\", "/");
                if (relativePath.StartsWith(".") || relativePath.Contains("/.")) continue;

                if (!folderPaths.Contains(relativePath))
                {
                    folderPaths.Add(relativePath);
                }
            }

            return await Task.FromResult(folderPaths.OrderBy(p => p).ToList());
        }

        public async Task DeleteNoteAsync(string id)
        {
            var files = Directory.GetFiles(_storageDirectory, $"{id}.md", SearchOption.AllDirectories)
                                 .Where(f => !f.Replace('\\', '/').Contains("/."))
                                 .ToList();

            var filePath = files.FirstOrDefault();

            if (filePath != null)
            {
                // 🌟 变动账本埋点：记录笔记彻底删除 (Delete)
          
                var relativePath = $"Workspaces/{CurrentWorkspace}/" + Path.GetRelativePath(_storageDirectory, filePath).Replace("\\", "/");
                await _syncLog.LogChangeAsync(relativePath, SyncActionType.Delete);
                var content = await File.ReadAllTextAsync(filePath);
                File.Delete(filePath);

                if (_noteCache != null)
                {
                    _noteCache.RemoveAll(n => n.Id == id);
                }

                var matches = Regex.Matches(content, @"\.assets/([a-zA-Z0-9_-]+\.[a-zA-Z0-9]+)");
                var assetNamesInDeletedNote = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

                if (assetNamesInDeletedNote.Any())
                {
                    var allRemainingFiles = Directory.GetFiles(_storageDirectory, "*.md", SearchOption.AllDirectories)
                                                     .Where(f => !f.Replace('\\', '/').Contains("/.assets/"))
                                                     .ToList();

                    foreach (var assetName in assetNamesInDeletedNote)
                    {
                        bool isUsedElsewhere = false;
                        foreach (var otherFile in allRemainingFiles)
                        {
                            var otherContent = await File.ReadAllTextAsync(otherFile);
                            if (otherContent.Contains(assetName))
                            {
                                isUsedElsewhere = true;
                                break;
                            }
                        }

                        if (!isUsedElsewhere)
                        {
                            var assetPath = Path.Combine(_storageDirectory, ".assets", assetName);
                            if (File.Exists(assetPath))
                            {
                                // 🌟 变动账本埋点：如果废弃图片被清理了，也通知云端删除
                                var assetRelPath = $"Workspaces/{CurrentWorkspace}/.assets/{assetName}";
                                await _syncLog.LogChangeAsync(assetRelPath, SyncActionType.Delete);

                                File.Delete(assetPath);
                            }
                        }
                    }
                }
            }
        }

        public async Task CreateFolderAsync(string parentFolderPath, string newFolderName)
        {
            var basePath = string.IsNullOrWhiteSpace(parentFolderPath)
                ? _storageDirectory
                : Path.Combine(_storageDirectory, parentFolderPath);

            var newFolderPath = Path.Combine(basePath, newFolderName);
            if (!Directory.Exists(newFolderPath)) Directory.CreateDirectory(newFolderPath);
            await Task.CompletedTask;
        }

        public async Task RenameFolderAsync(string oldFolderPath, string newFolderName)
        {
            if (string.IsNullOrWhiteSpace(oldFolderPath) || oldFolderPath == "notes") return;

            var oldFullPath = Path.Combine(_storageDirectory, oldFolderPath);
            var parentPath = Path.GetDirectoryName(oldFullPath);
            if (parentPath == null) return;

            var newFullPath = Path.Combine(parentPath, newFolderName);

            if (Directory.Exists(oldFullPath) && !Directory.Exists(newFullPath))
            {
                Directory.Move(oldFullPath, newFullPath);
                // 🌟 变动账本埋点：通知云端删除老文件夹 (新的文件夹由于内部没有触发修改，稍后需要在云端同步逻辑中特殊处理或触发全量扫描)
          
                await _syncLog.LogChangeAsync($"Workspaces/{CurrentWorkspace}/{oldFolderPath}", SyncActionType.Delete);
            }
            await Task.CompletedTask;
        }

        public async Task<string> SaveAssetAsync(byte[] fileData, string extension)
        {
            var assetsPath = Path.Combine(_storageDirectory, ".assets");
            if (!Directory.Exists(assetsPath)) Directory.CreateDirectory(assetsPath);

            var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{extension}";
            var filePath = Path.Combine(assetsPath, fileName);

            await File.WriteAllBytesAsync(filePath, fileData);

            // 🌟 变动账本埋点：上传图片附件时记录 Upsert
            await _syncLog.LogChangeAsync($".assets/{fileName}", SyncActionType.Upsert);

            return fileName;
        }

        public async Task DeleteFolderAsync(string folderPath, bool moveNotesToTrash)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || folderPath == "notes") return;

            var fullPath = Path.Combine(_storageDirectory, folderPath);
            if (!Directory.Exists(fullPath)) return;

            if (Directory.GetDirectories(fullPath).Length > 0)
            {
                throw new InvalidOperationException("包含子文件夹，无法直接删除。");
            }

            // 🌟 变动账本埋点：记录文件夹的彻底删除

            await _syncLog.LogChangeAsync($"Workspaces/{CurrentWorkspace}/{folderPath}", SyncActionType.Delete);

            var allNotes = await GetAllNotesAsync();
            var notesInFolder = allNotes.Where(n => n.Folder == folderPath).ToList();

            foreach (var note in notesInFolder)
            {
                note.Folder = "notes";
                if (moveNotesToTrash) note.IsDeleted = true;
                // 这行会触发 SaveNoteAsync，内部自动触发笔记路径变更的日志埋点
                await SaveNoteAsync(note);
            }

            Directory.Delete(fullPath, true);
        }

        public async Task RenameTagGlobalAsync(string oldTag, string newTag)
        {
            if (string.IsNullOrWhiteSpace(oldTag) || string.IsNullOrWhiteSpace(newTag) || oldTag == newTag) return;

            var allNotes = await GetAllNotesAsync();
            var affectedNotes = allNotes.Where(n => n.Tags != null && n.Tags.Contains(oldTag)).ToList();

            foreach (var bookNote in affectedNotes)
            {
                bookNote.Tags.Remove(oldTag);
                if (!bookNote.Tags.Contains(newTag)) bookNote.Tags.Add(newTag);

                await SaveNoteAsync(bookNote); // 会自动触发埋点
            }
        }

        public async Task DeleteTagGlobalAsync(string targetTag)
        {
            if (string.IsNullOrWhiteSpace(targetTag)) return;

            var allNotes = await GetAllNotesAsync();
            var affectedNotes = allNotes.Where(n => n.Tags != null && n.Tags.Contains(targetTag)).ToList();

            foreach (var bookNote in affectedNotes)
            {
                bookNote.Tags.Remove(targetTag);

                await SaveNoteAsync(bookNote); // 会自动触发埋点
            }
        }

        public async Task<List<MonoNote>> GetAllTemplatesAsync()
        {
            var templates = new List<MonoNote>();
            var templatesDir = Path.Combine(_storageDirectory, ".templates");
            if (!Directory.Exists(templatesDir)) return templates;

            var files = Directory.GetFiles(templatesDir, "*.md", SearchOption.TopDirectoryOnly);

            foreach (var file in files)
            {
                try
                {
                    var text = await File.ReadAllTextAsync(file);
                    if (text.StartsWith("---"))
                    {
                        var parts = text.Split(new[] { "---" }, 3, StringSplitOptions.None);
                        if (parts.Length >= 3)
                        {
                            var yaml = parts[1];
                            var content = parts[2].TrimStart('\r', '\n');
                            content = content.Replace("./.assets/", $"{AppConstants.ImageBaseUrl}/Workspaces/{CurrentWorkspace}/.assets/");

                            var meta = _yamlDeserializer.Deserialize<NoteMetadata>(yaml);

                            templates.Add(new MonoNote
                            {
                                Id = meta.Id ?? Path.GetFileNameWithoutExtension(file),
                                Title = meta.Title ?? "未命名模板",
                                Tags = meta.Tags ?? new List<string>(),
                                UpdatedAt = meta.UpdatedAt != default ? meta.UpdatedAt : File.GetLastWriteTime(file),
                                Content = content,
                                Folder = ".templates",
                                IsPinned = meta.IsPinned,
                                IsFavorite = meta.IsFavorite
                            });
                        }
                    }
                }
                catch { }
            }
            return templates.OrderByDescending(n => n.UpdatedAt).ToList();
        }

        public async Task SaveTemplateAsync(MonoNote template)
        {
            template.UpdatedAt = DateTimeOffset.Now;
            var templatesDir = Path.Combine(_storageDirectory, ".templates");
            if (!Directory.Exists(templatesDir)) Directory.CreateDirectory(templatesDir);

            var filePath = Path.Combine(templatesDir, $"{template.Id}.md");

            var meta = new NoteMetadata
            {
                Id = template.Id,
                Title = template.Title ?? "未命名模板",
                UpdatedAt = template.UpdatedAt,
                Tags = template.Tags ?? new List<string>(),
                IsPinned = template.IsPinned,
                IsFavorite = template.IsFavorite
            };

            var yaml = _yamlSerializer.Serialize(meta);
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.Append(yaml);
            sb.AppendLine("---");

            var pureContent = template.Content?.Replace($"{AppConstants.ImageBaseUrl}/Workspaces/{CurrentWorkspace}/", "./") ?? string.Empty;
            sb.AppendLine(pureContent);

            await File.WriteAllTextAsync(filePath, sb.ToString());

            // 🌟 变动账本埋点：新增模板 (Upsert)
     
            await _syncLog.LogChangeAsync($"Workspaces/{CurrentWorkspace}/.templates/{template.Id}.md", SyncActionType.Upsert);
        }

        public async Task DeleteTemplateAsync(string id)
        {
            var filePath = Path.Combine(_storageDirectory, ".templates", $"{id}.md");
            if (File.Exists(filePath))
            {
                // 🌟 变动账本埋点：删除模板 (Delete)
                await _syncLog.LogChangeAsync($"Workspaces/{CurrentWorkspace}/.templates/{id}.md", SyncActionType.Delete);
                File.Delete(filePath);
            }
        }

        private class NoteMetadata
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }
            public List<string> Tags { get; set; }
            public bool IsPinned { get; set; }
            public bool IsFavorite { get; set; }
            public bool IsArchived { get; set; }
            public bool IsDeleted { get; set; }
        }
    }
}