using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;
using System.Text.RegularExpressions; // 🌟 新增：用于解析图片链接

namespace MonoNotes.App.Services
{
    public class LocalNoteRepository : INoteRepository
    {
        private readonly string _storageDirectory;
        private readonly ISerializer _yamlSerializer;
        private readonly IDeserializer _yamlDeserializer;

        public LocalNoteRepository()
        {
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _storageDirectory = Path.Combine(documentsPath, "MonoNotes", "Data");

            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
            }

            // 初始化 YAML 序列化器，使用驼峰命名法
            _yamlSerializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            // 初始化 YAML 反序列化器，忽略未知的属性以防报错
            _yamlDeserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
        }

        // 获取所有笔记（支持无限层级子文件夹）
        public async Task<List<MonoNote>> GetAllNotesAsync()
        {
            var notes = new List<MonoNote>();

            // 深入所有子文件夹寻找 .md 文件
            var files = Directory.GetFiles(_storageDirectory, "*.md", SearchOption.AllDirectories);

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
                            // 🌟 读取时拦截：把相对路径替换成虚拟域名，让 WebView2 能渲染
                            content = content.Replace("./.assets/", "https://mononotes.local/.assets/");
                            var meta = _yamlDeserializer.Deserialize<NoteMetadata>(yaml);

                            // 计算相对路径
                            var fileDir = Path.GetDirectoryName(file) ?? _storageDirectory;
                            var relativePath = Path.GetRelativePath(_storageDirectory, fileDir);

                            // 如果在根目录，使用默认的 "notes"，否则保持相对路径统一格式
                            relativePath = relativePath == "." ? "notes" : relativePath.Replace("\\", "/");

                            notes.Add(new MonoNote
                            {
                                Id = meta.Id ?? Path.GetFileNameWithoutExtension(file),
                                Title = meta.Title ?? "无标题笔记",
                                Tags = meta.Tags ?? new List<string>(),
                                UpdatedAt = meta.UpdatedAt != default ? meta.UpdatedAt : File.GetLastWriteTime(file),
                                Content = content,
                                Folder = relativePath,
                                // 🌟 核心修改 1：读取时赋值状态
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

        // 保存笔记（自动创建不存在的物理文件夹）
        public async Task SaveNoteAsync(MonoNote note)
        {
            note.UpdatedAt = DateTimeOffset.Now;

            // 解析真实路径（如果 Folder 是默认的 "notes" 或者空，就存根目录）
            var isRoot = string.IsNullOrWhiteSpace(note.Folder) || note.Folder == "notes";
            var targetDirectory = isRoot
                ? _storageDirectory
                : Path.Combine(_storageDirectory, note.Folder);

            // 物理创建文件夹
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
                // 🌟 核心修改 2：保存时赋值状态给 DTO
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
            sb.AppendLine(note.Content ?? string.Empty);
            var pureContent = note.Content?.Replace("https://mononotes.local/", "./") ?? string.Empty;
            sb.AppendLine(pureContent);
            await File.WriteAllTextAsync(filePath, sb.ToString());
        }

        // 提取系统中所有的文件夹路径
        public async Task<List<string>> GetAllFoldersAsync()
        {
            var directories = Directory.GetDirectories(_storageDirectory, "*", SearchOption.AllDirectories);
            var folderPaths = new List<string> { "notes" }; // 确保根目录始终存在

            foreach (var dir in directories)
            {
                var relativePath = Path.GetRelativePath(_storageDirectory, dir).Replace("\\", "/");
                folderPaths.Add(relativePath);
            }

            return await Task.FromResult(folderPaths.Distinct().OrderBy(p => p).ToList());
        }

        public async Task DeleteNoteAsync(string id)
        {
            // 🌟 修复 Bug：全局搜索文件，确保哪怕笔记在深层子文件夹中也能被找到并删除
            var filePath = Directory.GetFiles(_storageDirectory, $"{id}.md", SearchOption.AllDirectories).FirstOrDefault();

            if (filePath != null)
            {
                // 1. 在删除前，先读取笔记内容
                var content = await File.ReadAllTextAsync(filePath);

                // 2. 删除物理 Markdown 文件本身
                File.Delete(filePath);

                // 3. 提取该笔记中所有的图片文件名
                // 匹配模式：寻找 .assets/ 后面跟着的文件名 (支持 png, jpg, gif, webp, svg 等)
                var matches = Regex.Matches(content, @"\.assets/([a-zA-Z0-9_-]+\.[a-zA-Z0-9]+)");
                var assetNamesInDeletedNote = matches.Select(m => m.Groups[1].Value).Distinct().ToList();

                // 4. 🌟 安全清理机制：如果有图片，检查是否被其他笔记“共享”
                if (assetNamesInDeletedNote.Any())
                {
                    // 获取当前硬盘上还剩下的所有笔记
                    var allRemainingFiles = Directory.GetFiles(_storageDirectory, "*.md", SearchOption.AllDirectories);

                    foreach (var assetName in assetNamesInDeletedNote)
                    {
                        bool isUsedElsewhere = false;

                        // 扫描其他笔记，看是否包含这个图片名
                        foreach (var otherFile in allRemainingFiles)
                        {
                            var otherContent = await File.ReadAllTextAsync(otherFile);
                            if (otherContent.Contains(assetName))
                            {
                                isUsedElsewhere = true;
                                break; // 只要有一个其他笔记在用，就立刻停止扫描，保护该图片
                            }
                        }

                        // 🌟 如果没有任何其他笔记在使用这张图片，安全地将其从硬盘抹除！
                        if (!isUsedElsewhere)
                        {
                            var assetPath = Path.Combine(_storageDirectory, ".assets", assetName);
                            if (File.Exists(assetPath))
                            {
                                File.Delete(assetPath);
                            }
                        }
                    }
                }
            }
        }

        public async Task CreateFolderAsync(string parentFolderPath, string newFolderName)
        {
            // 如果没选中任何文件夹，或者选中的是根目录 notes，就建在根目录下
            var isRoot = string.IsNullOrWhiteSpace(parentFolderPath) || parentFolderPath == "notes";
            var basePath = isRoot
                ? _storageDirectory
                : Path.Combine(_storageDirectory, parentFolderPath);

            // 拼接出新文件夹的完整物理路径
            var newFolderPath = Path.Combine(basePath, newFolderName);

            // 真实创建物理文件夹
            if (!Directory.Exists(newFolderPath))
            {
                Directory.CreateDirectory(newFolderPath);
            }

            // 返回完成状态
            await Task.CompletedTask;
        }

        public async Task RenameFolderAsync(string oldFolderPath, string newFolderName)
        {
            // 保护机制：不允许重命名根目录或未分类
            if (string.IsNullOrWhiteSpace(oldFolderPath) || oldFolderPath == "notes") return;

            var oldFullPath = Path.Combine(_storageDirectory, oldFolderPath);
            var parentPath = Path.GetDirectoryName(oldFullPath);
            if (parentPath == null) return;

            var newFullPath = Path.Combine(parentPath, newFolderName);

            // 在物理硬盘上真实地移动（重命名）文件夹
            if (Directory.Exists(oldFullPath) && !Directory.Exists(newFullPath))
            {
                Directory.Move(oldFullPath, newFullPath);
            }

            await Task.CompletedTask;
        }
        // 🌟 新增：保存附件到 .assets 文件夹
        public async Task<string> SaveAssetAsync(byte[] fileData, string extension)
        {
            // 将所有图片集中存放在根目录的 .assets 隐藏文件夹下
            var assetsPath = Path.Combine(_storageDirectory, ".assets");
            if (!Directory.Exists(assetsPath))
            {
                Directory.CreateDirectory(assetsPath);
            }

            // 生成极其安全且唯一的物理文件名：时间戳 + 随机数
            var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N").Substring(0, 4)}{extension}";
            var filePath = Path.Combine(assetsPath, fileName);

            await File.WriteAllBytesAsync(filePath, fileData);

            // 返回保存好的文件名
            return fileName;
        }
        // 内部 DTO：专门用来映射 YAML 头部的结构
        private class NoteMetadata
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public DateTimeOffset UpdatedAt { get; set; }
            public List<string> Tags { get; set; }

            // 🌟 核心修改 3：在映射实体中加入这两个字段
            public bool IsPinned { get; set; }
            public bool IsFavorite { get; set; }
            // 🌟 核心修改 1：增加归档字段映射
            public bool IsArchived { get; set; }
            public bool IsDeleted { get; set; }
        }
    }
}