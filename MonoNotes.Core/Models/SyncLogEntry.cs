namespace MonoNotes.Core.Models
{
    public enum SyncActionType
    {
        Upsert, // 新增或修改 (Update/Insert)
        Delete  // 物理删除
    }

    public class SyncLogEntry
    {
        /// <summary>
        /// 变动文件的相对路径 (例如: "notes/小说设定.md" 或 ".templates/模板1.md")
        /// </summary>
        public string RelativePath { get; set; } = string.Empty;

        /// <summary>
        /// 变动动作
        /// </summary>
        public SyncActionType Action { get; set; }

        /// <summary>
        /// 发生变动的 UTC 时间
        /// </summary>
        public DateTimeOffset Timestamp { get; set; }
    }
}