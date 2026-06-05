namespace MonoNotes.Core.Models
{
    public class ForeshadowItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        // 伏笔的描述，比如：“林动在山洞里捡到的神秘石符到底是什么来历？”
        public string Description { get; set; } = string.Empty;

        // 伏笔状态：false 表示“挖坑未填”，true 表示“已填坑回收”
        public bool IsResolved { get; set; } = false;

        // 🌟 核心关联：在哪里挖的坑？
        public string CreatedInChapterId { get; set; } = string.Empty;
        public string CreatedInChapterName { get; set; } = string.Empty;

        // 🌟 核心关联：在哪里填的坑？
        public string ResolvedInChapterId { get; set; } = string.Empty;
        public string ResolvedInChapterName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? ResolvedAt { get; set; }
    }
}