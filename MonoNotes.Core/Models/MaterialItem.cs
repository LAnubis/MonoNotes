using System;
using System.Collections.Generic;

namespace MonoNotes.Core.Models
{
    public enum MaterialType
    {
        Character,   // 🧑 角色档案
        Faction,     // 🏛️ 势力宗门
        Geography,   // 🗺️ 地理场景
        Item,        // ⚔️ 物品法宝
        Worldview    // 🌍 世界观与体系
    }

    /// <summary>
    /// 🌟 新增：富文本关系对象 (包含目标ID与具体关系描述)
    /// </summary>
    public class MaterialRelation
    {
        public string TargetId { get; set; } = "";
        public string Description { get; set; } = ""; // 关系描述，例如："妻子"、"掌门"、"宗门圣物"
    }

    public class LevelStage
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }

    public class MaterialItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "未命名设定";
        public string Description { get; set; } = "";
        public string AvatarUrl { get; set; } = "";
        public MaterialType Type { get; set; } = MaterialType.Character;

        public List<string> Aliases { get; set; } = new List<string>();
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

        // 🌟 核心修改：从 List<string> 升级为富关系对象集合
        public List<MaterialRelation> Relations { get; set; } = new List<MaterialRelation>();

        public List<LevelStage> LevelStages { get; set; } = new List<LevelStage>();

        public string? DerivedFromId { get; set; }
        public string? ReferenceId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public bool IsReadonlyReference => !string.IsNullOrEmpty(ReferenceId);
    }
}