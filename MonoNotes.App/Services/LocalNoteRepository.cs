using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;

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
                                IsArchived = meta.IsArchived
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
                IsArchived = note.IsArchived
            };

            var yaml = _yamlSerializer.Serialize(meta);
            var sb = new StringBuilder();
            sb.AppendLine("---");
            sb.Append(yaml);
            sb.AppendLine("---");
            sb.AppendLine(note.Content ?? string.Empty);

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
            var filePath = Path.Combine(_storageDirectory, $"{id}.md");
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
            await Task.CompletedTask;
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
        }
    }
}