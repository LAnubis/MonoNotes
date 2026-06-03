using System;
using System.Collections.Generic;
using System.Text;

namespace MonoNotes.Core.Models
{
    // 这是你白皮书里的 project.json 对应的实体类
    public class Work
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Title { get; set; } = string.Empty;

        // novel (小说), short (短篇), script (剧本), draft (草稿)
        public string WorkType { get; set; } = "novel";

        // 标签和分类：比如 ["玄幻", "连载中"]
        public List<string> Categories { get; set; } = new();

        // 假数据：字数统计
        public int WordCount { get; set; } = 0;
        public DateTime LastEditedAt { get; set; } = DateTime.Now;
    }
}
