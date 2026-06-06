using System;
using System.Collections.Generic;
using System.Text;

namespace MonoNotes.Core.Models
{
    // 1. 对应物理路径: Writespaces/Works/work_xxx/project.json
    public class WorkMeta
    {
        public int SchemaVersion { get; set; } = 1;
        public string AppVersion { get; set; } = "1.0.0";

        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "未命名作品";

        /// <summary> novel (小说), short (短篇), script (剧本), draft (草稿) </summary>
        public string WorkType { get; set; } = "novel";

        public List<string> Categories { get; set; } = new();
        public List<string> Bookshelves { get; set; } = new();

        /// <summary> drafting (草稿), serializing (连载), finished (完结), paused (暂停) </summary>
        public string Status { get; set; } = "drafting";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime LastEditedAt { get; set; } = DateTime.Now;

        // 🌟 回收站标记
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; } = null;

        // 缓存数据：字数统计（可随时从 Markdown 重新计算）
        public int WordCountCache { get; set; } = 0;

        public string Synopsis { get; set; } = "暂无简介...";
        public string Tags { get; set; } = ""; // 逗号分隔的标签
        public string CoverUrl { get; set; } = ""; // 封面图路径

        // 五大核心设定区
        public string Outline { get; set; } = "";            // 📜 核心大纲
        public string CharacterSettings { get; set; } = "";  // 🧑 角色设定
        public string WorldviewSettings { get; set; } = "";  // 🌍 世界观设定
        public string GeographySettings { get; set; } = "";  // 🗺️ 地理场景
        public string ItemSettings { get; set; } = "";       // ⚔️ 物品法宝
    }

    // 2. 对应物理路径: Writespaces/Works/work_xxx/work-index.json
    public class WorkIndex
    {
        public int SchemaVersion { get; set; } = 1;
        public string WorkId { get; set; } = string.Empty;
        public List<Volume> Volumes { get; set; } = new();
    }

    public class Volume
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "新卷";
        public bool IsExpanded { get; set; } = true;
        public List<Chapter> Chapters { get; set; } = new();
        public string Outline { get; set; } = ""; // 卷大纲：本卷的剧情主线与预期目标
    }

    public class Chapter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = "新章节";
        /// <summary> 物理文件相对路径，例如: Chapters/c_001.md </summary>
        public string FilePath { get; set; } = string.Empty;
        public int WordCount { get; set; } = 0;
        public string Outline { get; set; } = ""; // 章节细纲：本章的起承转合与爽点备忘
    }
}
