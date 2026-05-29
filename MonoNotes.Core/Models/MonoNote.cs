// 路径：MonoNotes.Core/Models/MonoNote.cs
using System;
using System.Collections.Generic;

namespace MonoNotes.Core.Models
{
    public class MonoNote
    {
        // 唯一标识，根据 PRD 建议，后续可以换成 ULID
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public string Title { get; set; } = "无标题笔记";

        // Markdown 正文，通常在列表中不加载全量正文，只在编辑器加载
        public string Content { get; set; } = string.Empty;

        public List<string> Tags { get; set; } = new();

        public string Folder { get; set; } = "notes"; // 默认所在文件夹

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

        //置顶
        public bool IsPinned { get; set; }
        //归档字段映射
        public bool IsArchived { get; set; }
        public bool IsDeleted { get; set; }
   
        public bool IsFavorite { get; set; }

        public List<string> OutgoingLinks { get; set; } = new();
        public string Summary
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Content)) return "无内容";
                var length = Content.Length > 50 ? 50 : Content.Length;
                return Content.Substring(0, length).Replace("\n", " ") + "...";
            }
        }
    }
}