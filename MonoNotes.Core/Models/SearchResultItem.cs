namespace MonoNotes.Core.Models
{
    public class SearchResultItem
    {
        public string NoteId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;

        // 匹配得分（标题匹配分数高，正文匹配分数低，用于排序）
        public int Score { get; set; }

        // 截取出的正文片段（已经用 <mark> 标签包裹了关键词）
        public List<string> Snippets { get; set; } = new();
    }
}