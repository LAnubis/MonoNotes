using MonoNotes.Core.Models;
using MonoNotes.Core.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace MonoNotes.Storage
{
    public class WriteSpaceRepository
    {
        private readonly string _writeSpaceRoot;

        public WriteSpaceRepository()
        {
            // 物理隔离：确保所有数据都在 Writespaces 专属目录下
            string myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            _writeSpaceRoot = Path.Combine(myDocs, "MonoNotes", "Writespaces");

            EnsureDirectories();
        }

        private void EnsureDirectories()
        {
            Directory.CreateDirectory(_writeSpaceRoot);
            Directory.CreateDirectory(Path.Combine(_writeSpaceRoot, "Works"));
            Directory.CreateDirectory(Path.Combine(_writeSpaceRoot, "GlobalMaterials"));
            Directory.CreateDirectory(Path.Combine(_writeSpaceRoot, "Trash"));
        }

        /// <summary>
        /// 扫描并加载所有作品的元数据 (用于驱动首页和全局看板)
        /// </summary>
        public async Task<List<WorkMeta>> GetAllWorksAsync()
        {
            var works = new List<WorkMeta>();
            var worksDir = Path.Combine(_writeSpaceRoot, "Works");

            foreach (var dir in Directory.GetDirectories(worksDir))
            {
                var projectJsonPath = Path.Combine(dir, "project.json");
                if (File.Exists(projectJsonPath))
                {
                    var meta = await JsonSafeWriter.ReadSafeAsync<WorkMeta>(projectJsonPath);
                    if (meta != null) works.Add(meta);
                }
            }

            // 按照最后编辑时间倒序排列
            return works.OrderByDescending(w => w.LastEditedAt).ToList();
        }

        /// <summary>
        /// 创建一本新作品，并初始化物理目录结构
        /// </summary>
        public async Task<WorkMeta> CreateNewWorkAsync(string title, string workType = "novel")
        {
            var meta = new WorkMeta { Title = title, WorkType = workType };
            var workDir = Path.Combine(_writeSpaceRoot, "Works", meta.Id);

            // 创建该作品的专属多级目录
            Directory.CreateDirectory(workDir);
            Directory.CreateDirectory(Path.Combine(workDir, "Chapters"));
            Directory.CreateDirectory(Path.Combine(workDir, "Foreshadowing"));
            Directory.CreateDirectory(Path.Combine(workDir, "VersionHistory"));

            // 初始化一份空的目录索引
            var index = new WorkIndex { WorkId = meta.Id };

            // 原子化保存
            await JsonSafeWriter.WriteAtomicallyAsync(Path.Combine(workDir, "project.json"), meta);
            await JsonSafeWriter.WriteAtomicallyAsync(Path.Combine(workDir, "work-index.json"), index);

            return meta;
        }

        /// <summary>
        /// 读取单本小说的结构索引 (work-index.json)
        /// </summary>
        public async Task<WorkIndex?> GetWorkIndexAsync(string workId)
        {
            var indexPath = Path.Combine(_writeSpaceRoot, "Works", workId, "work-index.json");
            return await JsonSafeWriter.ReadSafeAsync<WorkIndex>(indexPath);
        }

        /// <summary>
        /// 保存单本小说的结构索引 (新建卷、新建章、拖拽排序时调用)
        /// </summary>
        public async Task SaveWorkIndexAsync(WorkIndex index)
        {
            var indexPath = Path.Combine(_writeSpaceRoot, "Works", index.WorkId, "work-index.json");
            await JsonSafeWriter.WriteAtomicallyAsync(indexPath, index);
        }

        /// <summary>
        /// 读取指定章节的 Markdown 源码
        /// </summary>
        public async Task<string> ReadChapterContentAsync(string workId, string chapterId)
        {
            // 规范物理路径：Writespaces/Works/{workId}/Chapters/{chapterId}.md
            var filePath = Path.Combine(_writeSpaceRoot, "Works", workId, "Chapters", $"{chapterId}.md");

            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath);
            }
            return string.Empty; // 如果是新建的章节，文件可能还不存在，返回空字符串
        }

        /// <summary>
        /// 保存章节内容，并同步更新该章节的字数统计
        /// </summary>
        public async Task SaveChapterContentAsync(string workId, string chapterId, string content)
        {
            var chapterDir = Path.Combine(_writeSpaceRoot, "Works", workId, "Chapters");
            Directory.CreateDirectory(chapterDir); // 确保目录存在

            var filePath = Path.Combine(chapterDir, $"{chapterId}.md");

            // 1. 保存纯文本内容
            await File.WriteAllTextAsync(filePath, content);

            // 2. 更新单本索引里的字数统计
            var index = await GetWorkIndexAsync(workId);
            if (index != null)
            {
                var chapter = index.Volumes.SelectMany(v => v.Chapters).FirstOrDefault(c => c.Id == chapterId);
                if (chapter != null)
                {
                    chapter.WordCount = string.IsNullOrWhiteSpace(content) ? 0 : content.Length;
                    await SaveWorkIndexAsync(index);
                }
            }
        }
    }
}
