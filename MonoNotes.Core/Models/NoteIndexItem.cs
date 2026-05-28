namespace MonoNotes.Core.Models
{
    /// <summary>
    /// 专用于 JSON 高速缓存的极简索引实体
    /// 不包含文件正文，极大地缩减内存占用与 JSON 文件体积
    /// </summary>
    public class NoteIndexItem
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Folder { get; set; } = string.Empty;
        public DateTimeOffset UpdatedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public bool IsPinned { get; set; }
        public bool IsFavorite { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsArchived { get; set; }
        public List<string> Tags { get; set; } = new();
    }
}