using System;
using System.Collections.Generic;

namespace MonoNotes.Core.Models
{
    /// <summary>
    /// 设定素材的五大基础分类
    /// </summary>
    public enum MaterialType
    {
        Character,   // 🧑 角色档案
        Faction,     // 🏛️ 势力宗门
        Geography,   // 🗺️ 地理场景
        Item,        // ⚔️ 物品法宝
        Worldview    // 🌍 世界观与体系
    }

    /// <summary>
    /// 万能设定卡片基类 (支持全局与单本)
    /// </summary>
    public class MaterialItem
    {
        // ==========================================
        // 1. 基础基类 (Base)
        // ==========================================
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "未命名设定";
        public string Description { get; set; } = "";
        public string AvatarUrl { get; set; } = ""; // 头像或参考图路径
        public MaterialType Type { get; set; } = MaterialType.Character;

        // ==========================================
        // 2. 高阶扩展 (Advanced Extensions)
        // ==========================================
        /// <summary> 别名系统 (为编辑器 @ 悬停铺垫) </summary>
        public List<string> Aliases { get; set; } = new List<string>();

        /// <summary> 自定义属性 (KV 键值对，如：境界=元婴，灵根=变异雷) </summary>
        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();

        /// <summary> 轻量级 ID 关联 (关联到其他 Material 的 ID) </summary>
        public List<string> Relations { get; set; } = new List<string>();

        // ==========================================
        // 3. 原型-实例架构的核心字段 (Prototype-Instance)
        // ==========================================
        /// <summary> 派生自哪个全局模板？(标识其来源，但本卡片可独立修改) </summary>
        public string? DerivedFromId { get; set; }

        /// <summary> 引用的全局模板 ID (如果不为空，代表此卡片在单本中只读) </summary>
        public string? ReferenceId { get; set; }

        // ==========================================
        // 4. 元数据
        // ==========================================
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        /// <summary> 是否为只读引用模式？ </summary>
        public bool IsReadonlyReference => !string.IsNullOrEmpty(ReferenceId);
    }
}