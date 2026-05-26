using System;

namespace MonoNotes.Core.Models
{
    public class AttachmentMeta
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        // 相对路径，例如： "assets/images/20260525-143012-01HXABCDEF.png"
        public string RelativePath { get; set; } = string.Empty;

        // 原始文件名，方便用户导出时恢复
        public string OriginalFileName { get; set; } = string.Empty;

        // 附件类型： "image", "audio", "file"
        public string AssetType { get; set; } = "image";

        public long FileSizeBytes { get; set; }

        // 文件添加时间
        public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;

        // 强关联的笔记 ID（如果是多篇笔记共用，这里可以改成 List<string>）
        public string ReferencedDocumentId { get; set; } = string.Empty;
    }
}