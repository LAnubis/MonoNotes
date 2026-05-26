using System;

namespace MonoNotes.Core.Models
{
    public class SyncConflictRecord
    {
        // 冲突记录本身的唯一 ID
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        // 产生冲突的笔记实体 ID (即 MonoNote.Id)
        public string DocumentId { get; set; } = string.Empty;

        // 笔记原标题（方便在 UI 列表中展示给用户看）
        public string NoteTitle { get; set; } = string.Empty;

        // 本地/当前版本的文件路径
        public string LocalFilePath { get; set; } = string.Empty;

        // 网盘同步下来的冲突版本文件路径
        public string RemoteConflictFilePath { get; set; } = string.Empty;

        // 发现冲突的时间
        public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.Now;

        // 冲突是否已被用户解决
        public bool IsResolved { get; set; } = false;

        // 解决方式：如 "KeptLocal" (保留本地), "KeptRemote" (保留云端), "Merged" (合并)
        public string ResolutionType { get; set; } = string.Empty;
    }
}