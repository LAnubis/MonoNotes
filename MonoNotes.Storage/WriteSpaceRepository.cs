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

        /// <summary>
        /// 更新小说的元数据标题 (project.json)
        /// </summary>
        public async Task UpdateWorkTitleAsync(string workId, string newTitle)
        {
            var metaPath = Path.Combine(_writeSpaceRoot, "Works", workId, "project.json");
            var meta = await JsonSafeWriter.ReadSafeAsync<WorkMeta>(metaPath);
            if (meta != null)
            {
                meta.Title = newTitle;
                meta.LastEditedAt = DateTime.Now;
                await JsonSafeWriter.WriteAtomicallyAsync(metaPath, meta);
            }
        }

        // ==========================================
        // 🌟 每日字数统计 (热力图引擎)
        // ==========================================
        public async Task AddDailyWordCountAsync(int count)
        {
            if (count <= 0) return;
            var statsPath = Path.Combine(_writeSpaceRoot, "daily-stats.json");
            Dictionary<string, int> stats = new();

            if (File.Exists(statsPath))
            {
                stats = await JsonSafeWriter.ReadSafeAsync<Dictionary<string, int>>(statsPath) ?? new();
            }

            string today = DateTime.Now.ToString("yyyy-MM-dd");
            if (stats.ContainsKey(today)) stats[today] += count;
            else stats[today] = count;

            await JsonSafeWriter.WriteAtomicallyAsync(statsPath, stats);
        }

        public async Task<Dictionary<string, int>> GetDailyStatsAsync()
        {
            var statsPath = Path.Combine(_writeSpaceRoot, "daily-stats.json");
            if (File.Exists(statsPath))
            {
                return await JsonSafeWriter.ReadSafeAsync<Dictionary<string, int>>(statsPath) ?? new();
            }
            return new Dictionary<string, int>();
        }

        /// <summary>
        /// 保存更新后的小说详情与元数据 (project.json)
        /// </summary>
        public async Task SaveWorkMetaAsync(WorkMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.Id)) return;

            var metaPath = Path.Combine(_writeSpaceRoot, "Works", meta.Id, "project.json");
            meta.LastEditedAt = DateTime.Now;

            await JsonSafeWriter.WriteAtomicallyAsync(metaPath, meta);
        }

        // ====================================================================
        // 🌟 万能设定集引擎 (Material System Engine)
        // ====================================================================

        private string GetLocalMaterialsPath(string workId)
        {
            var path = Path.Combine(_writeSpaceRoot, "Works", workId, "Materials");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        private string GetGlobalMaterialsPath()
        {
            var path = Path.Combine(_writeSpaceRoot, "GlobalMaterials");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return path;
        }

        /// <summary> 获取单本小说内的所有设定素材 </summary>
        public async Task<List<MaterialItem>> GetLocalMaterialsAsync(string workId)
        {
            var path = Path.Combine(GetLocalMaterialsPath(workId), "materials.json");
            if (!File.Exists(path)) return new List<MaterialItem>();
            return await JsonSafeWriter.ReadSafeAsync<List<MaterialItem>>(path) ?? new List<MaterialItem>();
        }

        /// <summary> 获取全局资源库的所有设定模板 </summary>
        public async Task<List<MaterialItem>> GetGlobalMaterialsAsync()
        {
            var path = Path.Combine(GetGlobalMaterialsPath(), "global_materials.json");
            if (!File.Exists(path)) return new List<MaterialItem>();
            return await JsonSafeWriter.ReadSafeAsync<List<MaterialItem>>(path) ?? new List<MaterialItem>();
        }

        /// <summary> 批量保存单本小说设定 </summary>
        public async Task SaveLocalMaterialsAsync(string workId, List<MaterialItem> materials)
        {
            var path = Path.Combine(GetLocalMaterialsPath(workId), "materials.json");
            foreach (var m in materials) m.UpdatedAt = DateTime.Now;
            await JsonSafeWriter.WriteAtomicallyAsync(path, materials);
        }

        /// <summary> 批量保存全局设定模板 </summary>
        public async Task SaveGlobalMaterialsAsync(List<MaterialItem> materials)
        {
            var path = Path.Combine(GetGlobalMaterialsPath(), "global_materials.json");
            foreach (var m in materials) m.UpdatedAt = DateTime.Now;
            await JsonSafeWriter.WriteAtomicallyAsync(path, materials);
        }

        // -----------------------------------------------------------
        // 🚀 核心流转 1：派生 (Global ➡️ Local Deep Copy)
        // -----------------------------------------------------------
        public async Task DeriveMaterialToLocalAsync(string workId, MaterialItem globalTemplate)
        {
            var locals = await GetLocalMaterialsAsync(workId);

            // 深拷贝一份新的实例
            var newLocalInstance = new MaterialItem
            {
                Id = Guid.NewGuid().ToString("N"), // 生成全新的局部 ID
                Name = globalTemplate.Name,
                Description = globalTemplate.Description,
                AvatarUrl = globalTemplate.AvatarUrl,
                Type = globalTemplate.Type,
                Aliases = new List<string>(globalTemplate.Aliases),
                CustomFields = new Dictionary<string, string>(globalTemplate.CustomFields),
                DerivedFromId = globalTemplate.Id // 🌟 认祖归宗，打上派生标记
            };

            locals.Add(newLocalInstance);
            await SaveLocalMaterialsAsync(workId, locals);
        }

        // -----------------------------------------------------------
        // 🚀 核心流转 2：引用 (Global ➡️ Local Readonly Reference)
        // -----------------------------------------------------------
        public async Task ReferenceMaterialToLocalAsync(string workId, MaterialItem globalTemplate)
        {
            var locals = await GetLocalMaterialsAsync(workId);

            // 生成一个空壳引用
            var refInstance = new MaterialItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = globalTemplate.Type,
                ReferenceId = globalTemplate.Id // 🌟 核心：打上只读引用标记
            };

            locals.Add(refInstance);
            await SaveLocalMaterialsAsync(workId, locals);
        }

        // -----------------------------------------------------------
        // 🚀 核心流转 3：沉淀 (Local ➡️ Global Abstract & Up-copy)
        // -----------------------------------------------------------
        public async Task PromoteToGlobalTemplateAsync(string workId, MaterialItem localItem)
        {
            var globals = await GetGlobalMaterialsAsync();
            var newGlobalId = Guid.NewGuid().ToString("N");

            // 1. 向上提取生成全局模板 (脱敏处理，不带走单本独有的 Relations)
            var globalTemplate = new MaterialItem
            {
                Id = newGlobalId,
                Name = localItem.Name + " (模板)",
                Description = localItem.Description,
                AvatarUrl = localItem.AvatarUrl,
                Type = localItem.Type,
                CustomFields = new Dictionary<string, string>(localItem.CustomFields)
                // 注意：没有复制 Aliases 和 Relations，保持全局模板的纯净
            };
            globals.Add(globalTemplate);
            await SaveGlobalMaterialsAsync(globals);

            // 2. 将本地原来的卡片，绑定到这个新生成的全局模板上
            var locals = await GetLocalMaterialsAsync(workId);
            var itemToUpdate = locals.FirstOrDefault(x => x.Id == localItem.Id);
            if (itemToUpdate != null)
            {
                itemToUpdate.DerivedFromId = newGlobalId; // 🌟 重新认祖归宗
                await SaveLocalMaterialsAsync(workId, locals);
            }
        }

        // ====================================================================
        // 🌟 伏笔与填坑回收站引擎 (Foreshadowing Engine)
        // ====================================================================

        private string GetForeshadowPath(string workId)
        {
            var path = Path.Combine(_writeSpaceRoot, "Works", workId, "Foreshadowing");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "foreshadows.json");
        }

        /// <summary> 获取单本小说的所有伏笔 </summary>
        public async Task<List<ForeshadowItem>> GetForeshadowsAsync(string workId)
        {
            var path = GetForeshadowPath(workId);
            if (!File.Exists(path)) return new List<ForeshadowItem>();
            return await JsonSafeWriter.ReadSafeAsync<List<ForeshadowItem>>(path) ?? new List<ForeshadowItem>();
        }

        /// <summary> 保存伏笔列表 </summary>
        public async Task SaveForeshadowsAsync(string workId, List<ForeshadowItem> items)
        {
            var path = GetForeshadowPath(workId);
            await JsonSafeWriter.WriteAtomicallyAsync(path, items);
        }
    }
}
