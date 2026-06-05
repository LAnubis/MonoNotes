using MonoNotes.Core.Models;
using MonoNotes.Core.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
            return works.OrderByDescending(w => w.LastEditedAt).ToList();
        }

        public async Task<WorkMeta> CreateNewWorkAsync(string title, string workType = "novel")
        {
            var meta = new WorkMeta { Title = title, WorkType = workType };
            var workDir = Path.Combine(_writeSpaceRoot, "Works", meta.Id);

            Directory.CreateDirectory(workDir);
            Directory.CreateDirectory(Path.Combine(workDir, "Chapters"));
            Directory.CreateDirectory(Path.Combine(workDir, "Foreshadowing"));
            Directory.CreateDirectory(Path.Combine(workDir, "VersionHistory"));

            var index = new WorkIndex { WorkId = meta.Id };

            await JsonSafeWriter.WriteAtomicallyAsync(Path.Combine(workDir, "project.json"), meta);
            await JsonSafeWriter.WriteAtomicallyAsync(Path.Combine(workDir, "work-index.json"), index);

            return meta;
        }

        public async Task<WorkIndex?> GetWorkIndexAsync(string workId)
        {
            var indexPath = Path.Combine(_writeSpaceRoot, "Works", workId, "work-index.json");
            return await JsonSafeWriter.ReadSafeAsync<WorkIndex>(indexPath);
        }

        public async Task SaveWorkIndexAsync(WorkIndex index)
        {
            var indexPath = Path.Combine(_writeSpaceRoot, "Works", index.WorkId, "work-index.json");
            await JsonSafeWriter.WriteAtomicallyAsync(indexPath, index);
        }

        public async Task<string> ReadChapterContentAsync(string workId, string chapterId)
        {
            var filePath = Path.Combine(_writeSpaceRoot, "Works", workId, "Chapters", $"{chapterId}.md");
            if (File.Exists(filePath))
            {
                return await File.ReadAllTextAsync(filePath);
            }
            return string.Empty;
        }

        public async Task SaveChapterContentAsync(string workId, string chapterId, string content)
        {
            var chapterDir = Path.Combine(_writeSpaceRoot, "Works", workId, "Chapters");
            Directory.CreateDirectory(chapterDir);
            var filePath = Path.Combine(chapterDir, $"{chapterId}.md");

            await File.WriteAllTextAsync(filePath, content);

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

        // 🌟 这是为导入引擎专门加的极速并发写入方法！
        public async Task BatchSaveChapterContentsAsync(string workId, Dictionary<string, string> chapterContents)
        {
            var chapterDir = Path.Combine(_writeSpaceRoot, "Works", workId, "Chapters");
            Directory.CreateDirectory(chapterDir);

            var tasks = new List<Task>();
            foreach (var kvp in chapterContents)
            {
                var filePath = Path.Combine(chapterDir, $"{kvp.Key}.md");
                tasks.Add(File.WriteAllTextAsync(filePath, kvp.Value));
            }
            await Task.WhenAll(tasks);
        }

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

        public async Task SaveWorkMetaAsync(WorkMeta meta)
        {
            if (meta == null || string.IsNullOrEmpty(meta.Id)) return;
            var metaPath = Path.Combine(_writeSpaceRoot, "Works", meta.Id, "project.json");
            meta.LastEditedAt = DateTime.Now;
            await JsonSafeWriter.WriteAtomicallyAsync(metaPath, meta);
        }

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

        public async Task<List<MaterialItem>> GetLocalMaterialsAsync(string workId)
        {
            var path = Path.Combine(GetLocalMaterialsPath(workId), "materials.json");
            var materials = new List<MaterialItem>();
            if (File.Exists(path))
            {
                materials = await JsonSafeWriter.ReadSafeAsync<List<MaterialItem>>(path) ?? new List<MaterialItem>();
            }

            // 🌟 读取设定的同时，喂给 NLP 引擎！
            NerEngine.SyncUserDictionary(materials);

            return materials;
        }
        public async Task<List<MaterialItem>> GetGlobalMaterialsAsync()
        {
            var path = Path.Combine(GetGlobalMaterialsPath(), "global_materials.json");
            if (!File.Exists(path)) return new List<MaterialItem>();
            return await JsonSafeWriter.ReadSafeAsync<List<MaterialItem>>(path) ?? new List<MaterialItem>();
        }

        public async Task SaveLocalMaterialsAsync(string workId, List<MaterialItem> materials)
        {
            var path = Path.Combine(GetLocalMaterialsPath(workId), "materials.json");
            foreach (var m in materials) m.UpdatedAt = DateTime.Now;
            await JsonSafeWriter.WriteAtomicallyAsync(path, materials);

            // 🌟 保存新设定后，实时更新 NLP 词典！
            NerEngine.SyncUserDictionary(materials);
        }

        public async Task SaveGlobalMaterialsAsync(List<MaterialItem> materials)
        {
            var path = Path.Combine(GetGlobalMaterialsPath(), "global_materials.json");
            foreach (var m in materials) m.UpdatedAt = DateTime.Now;
            await JsonSafeWriter.WriteAtomicallyAsync(path, materials);
        }

        public async Task DeriveMaterialToLocalAsync(string workId, MaterialItem globalTemplate)
        {
            var locals = await GetLocalMaterialsAsync(workId);
            var newLocalInstance = new MaterialItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = globalTemplate.Name,
                Description = globalTemplate.Description,
                AvatarUrl = globalTemplate.AvatarUrl,
                Type = globalTemplate.Type,
                Aliases = new List<string>(globalTemplate.Aliases),
                CustomFields = new Dictionary<string, string>(globalTemplate.CustomFields),
                DerivedFromId = globalTemplate.Id
            };
            locals.Add(newLocalInstance);
            await SaveLocalMaterialsAsync(workId, locals);
        }

        public async Task ReferenceMaterialToLocalAsync(string workId, MaterialItem globalTemplate)
        {
            var locals = await GetLocalMaterialsAsync(workId);
            var refInstance = new MaterialItem
            {
                Id = Guid.NewGuid().ToString("N"),
                Type = globalTemplate.Type,
                ReferenceId = globalTemplate.Id
            };
            locals.Add(refInstance);
            await SaveLocalMaterialsAsync(workId, locals);
        }

        public async Task PromoteToGlobalTemplateAsync(string workId, MaterialItem localItem)
        {
            var globals = await GetGlobalMaterialsAsync();
            var newGlobalId = Guid.NewGuid().ToString("N");
            var globalTemplate = new MaterialItem
            {
                Id = newGlobalId,
                Name = localItem.Name + " (模板)",
                Description = localItem.Description,
                AvatarUrl = localItem.AvatarUrl,
                Type = localItem.Type,
                CustomFields = new Dictionary<string, string>(localItem.CustomFields)
            };
            globals.Add(globalTemplate);
            await SaveGlobalMaterialsAsync(globals);

            var locals = await GetLocalMaterialsAsync(workId);
            var itemToUpdate = locals.FirstOrDefault(x => x.Id == localItem.Id);
            if (itemToUpdate != null)
            {
                itemToUpdate.DerivedFromId = newGlobalId;
                await SaveLocalMaterialsAsync(workId, locals);
            }
        }

        private string GetForeshadowPath(string workId)
        {
            var path = Path.Combine(_writeSpaceRoot, "Works", workId, "Foreshadowing");
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, "foreshadows.json");
        }

        public async Task<List<ForeshadowItem>> GetForeshadowsAsync(string workId)
        {
            var path = GetForeshadowPath(workId);
            if (!File.Exists(path)) return new List<ForeshadowItem>();
            return await JsonSafeWriter.ReadSafeAsync<List<ForeshadowItem>>(path) ?? new List<ForeshadowItem>();
        }

        public async Task SaveForeshadowsAsync(string workId, List<ForeshadowItem> items)
        {
            var path = GetForeshadowPath(workId);
            await JsonSafeWriter.WriteAtomicallyAsync(path, items);
        }
    }
}