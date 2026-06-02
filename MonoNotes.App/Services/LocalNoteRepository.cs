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

        public LocalNoteRepository()
        {

            // 提升到 MonoNotes 根目录
            _rootDirectory = PathHelper.GetBaseDirectory();
            if (!Directory.Exists(_rootDirectory)) Directory.CreateDirectory(_rootDirectory);

            // 读取上次打开的库配置
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

        // 🌟 辅助方法：确保系统目录存在，并执行平级架构的无感迁移！
        private void EnsureDirectories()
        {
            if (!Directory.Exists(_storageDirectory)) Directory.CreateDirectory(_storageDirectory);

            // 确保系统的三大基础文件夹存在
            var notesPath = Path.Combine(_storageDirectory, "notes");
            if (!Directory.Exists(notesPath)) Directory.CreateDirectory(notesPath);

            var assetsPath = Path.Combine(_storageDirectory, ".assets");
            if (!Directory.Exists(assetsPath)) Directory.CreateDirectory(assetsPath);

            var templatesPath = Path.Combine(_storageDirectory, ".templates");
            if (!Directory.Exists(templatesPath)) Directory.CreateDirectory(templatesPath);

            try
            {
                // 🌟 核心修正：只将“无家可归”散落在根目录的 .md 笔记收容进 notes 文件夹
                var rootFiles = Directory.GetFiles(_storageDirectory, "*.md", SearchOption.TopDirectoryOnly);
                foreach (var file in rootFiles)
                {
                    var dest = Path.Combine(notesPath, Path.GetFileName(file));
                    if (!File.Exists(dest)) File.Move(file, dest);
                }

                // 绝对不触碰用户的文件夹，让它们保持在根目录和 notes 平级！
            }
            catch { /* 忽略迁移中可能的文件占用问题 */ }
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
        }

        public async Task CreateWorkspaceAsync(string workspaceName)
        {
            var newPath = Path.Combine(_rootDirectory, "Workspaces", workspaceName);
            if (!Directory.Exists(newPath)) Directory.CreateDirectory(newPath);
            await SwitchWorkspaceAsync(workspaceName);
        }

        // 🌟 核心：扫描全域，但屏蔽系统隐藏文件夹
        public async Task<List<MonoNote>> GetAllNotesAsync()
        {
            var notes = new List<MonoNote>();

            // 获取所有笔记，排除路径中包含 "/." 的隐藏文件夹（如 /.assets/ 和 /.templates/）
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

                            // 计算相对于当前知识库的物理相对路径
                            var fileDir = Path.GetDirectoryName(file) ?? _storageDirectory;
                            var relativePath = Path.GetRelativePath(_storageDirectory, fileDir).Replace("\\", "/");

                            // 兜底保护：如果是根目录散落的（理论上已经被迁移了），强行划入 notes
                            if (relativePath == ".") relativePath = "notes";

                            notes.Add(new MonoNote
                            {
                                Id = meta.Id ?? Path.GetFileNameWithoutExtension(file),
                                Title = meta.Title ?? "无标题笔记",
                                Tags = meta.Tags ?? new List<string>(),
                                UpdatedAt = meta.UpdatedAt != default ? meta.UpdatedAt : File.GetLastWriteTime(file),
                                Content = content,
                                Folder = relativePath, // 天生就是 "notes" 或者 "C#学习/基础" 
                                IsPinned = meta.IsPinned,
                                IsFavorite = meta.IsFavorite,
                                IsArchived = meta.IsArchived,
                                IsDeleted = meta.IsDeleted
                            });
                        }
                    }
                }
                catch { /* 忽略损坏的文件 */ }
            }
            return notes.OrderByDescending(n => n.UpdatedAt).ToList();
        }

        // 🌟 保存逻辑：根据 Folder 相对路径直接保存，没有任何黑魔法
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
        }

        // 🌟 扫描全域文件夹，但排除隐藏目录
        public async Task<List<string>> GetAllFoldersAsync()
        {
            var directories = Directory.GetDirectories(_storageDirectory, "*", SearchOption.AllDirectories);
            var folderPaths = new List<string> { "notes" }; // 确保保底的未分类总是存在

            foreach (var dir in directories)
            {
                var relativePath = Path.GetRelativePath(_storageDirectory, dir).Replace("\\", "/");

                // 屏蔽所有以 "." 开头的系统级隐藏文件夹
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
            // 全域查找这个 md 文件，排除隐藏文件夹
            var files = Directory.GetFiles(_storageDirectory, $"{id}.md", SearchOption.AllDirectories)
                                 .Where(f => !f.Replace('\\', '/').Contains("/."))
                                 .ToList();

            var filePath = files.FirstOrDefault();

            if (filePath != null)
            {
                var content = await File.ReadAllTextAsync(filePath);
                File.Delete(filePath);

                // 🌟 孤儿图片清理逻辑
                var matches = Regex.Matches(content, @"\.assets/([a-zA-Z0-9_-]+\.[a-zA-Z0-9]+)");
                var assetNamesInDeletedNote = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

                if (assetNamesInDeletedNote.Any())
                {
                    // 扫描所有合法的业务笔记和模板，检查图片是否还在被使用
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
                            if (File.Exists(assetPath)) File.Delete(assetPath);
                        }
                    }
                }
            }
        }

        // 🌟 新建文件夹：如果 parent 传入的是空，就是在根目录创建顶层文件夹！
        public async Task CreateFolderAsync(string parentFolderPath, string newFolderName)
        {
            var basePath = string.IsNullOrWhiteSpace(parentFolderPath)
                ? _storageDirectory
                : Path.Combine(_storageDirectory, parentFolderPath);

            var newFolderPath = Path.Combine(basePath, newFolderName);

            if (!Directory.Exists(newFolderPath))
            {
                Directory.CreateDirectory(newFolderPath);
            }

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

            var allNotes = await GetAllNotesAsync();
            var notesInFolder = allNotes.Where(n => n.Folder == folderPath).ToList();

            foreach (var note in notesInFolder)
            {
                note.Folder = "notes";
                if (moveNotesToTrash) note.IsDeleted = true;
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
                await SaveNoteAsync(bookNote);
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
                await SaveNoteAsync(bookNote);
            }
        }

        // ==========================================
        // 🌟 模板专属管理方法
        // ==========================================

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
                catch { /* 忽略损坏的模板 */ }
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
        }

        public async Task DeleteTemplateAsync(string id)
        {
            var filePath = Path.Combine(_storageDirectory, ".templates", $"{id}.md");
            if (File.Exists(filePath)) File.Delete(filePath);
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