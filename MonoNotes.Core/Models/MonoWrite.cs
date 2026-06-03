using System;
using System.Collections.Generic;
using System.Text;

namespace MonoNotes.Core.Models
{
    public class Chapter
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public int WordCount { get; set; } = 0;
    }

    public class Volume
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;
        public bool IsExpanded { get; set; } = true; // 控制 UI 的折叠状态，默认展开
        public List<Chapter> Chapters { get; set; } = new();
    }
}
