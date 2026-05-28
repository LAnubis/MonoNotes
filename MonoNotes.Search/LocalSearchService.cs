using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;
using System.Text.RegularExpressions;

namespace MonoNotes.Search
{
    public class LocalSearchService : ISearchService
    {
        public Task<List<SearchResultItem>> SearchAsync(string keyword, IEnumerable<MonoNote> allNotes)
        {
            var results = new List<SearchResultItem>();
            if (string.IsNullOrWhiteSpace(keyword)) return Task.FromResult(results);

            var query = keyword.Trim();
            // 构建不区分大小写的正则，用于高亮和片段截取
            var regex = new Regex($"({Regex.Escape(query)})", RegexOptions.IgnoreCase | RegexOptions.Compiled);

            foreach (var note in allNotes)
            {
                if (note.IsDeleted) continue; // 不搜索已删除的笔记

                int score = 0;
                var snippets = new List<string>();
                bool isMatched = false;

                // 1. 匹配标题 (权重最高)
                if (!string.IsNullOrEmpty(note.Title) && note.Title.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                    isMatched = true;
                }

                // 2. 匹配正文 (截取上下文)
                if (!string.IsNullOrEmpty(note.Content))
                {
                    var matches = regex.Matches(note.Content);
                    if (matches.Count > 0)
                    {
                        score += matches.Count * 10; // 出现次数越多，得分越高
                        isMatched = true;

                        // 截取前 3 个匹配片段，防止结果过长
                        for (int i = 0; i < Math.Min(3, matches.Count); i++)
                        {
                            var match = matches[i];
                            // 截取关键词前后各 30 个字符作为上下文
                            int start = Math.Max(0, match.Index - 30);
                            int length = Math.Min(note.Content.Length - start, match.Length + 60);

                            var snippet = note.Content.Substring(start, length);

                            // 去除换行符，并用 <mark> 标签包裹关键词以便 UI 高亮
                            snippet = snippet.Replace("\n", " ").Replace("\r", "");
                            snippet = regex.Replace(snippet, "<mark style='background-color: rgba(46, 170, 220, 0.3); color: inherit; border-radius: 2px; padding: 0 2px;'>$1</mark>");

                            snippets.Add($"...{snippet}...");
                        }
                    }
                }

                if (isMatched)
                {
                    results.Add(new SearchResultItem
                    {
                        NoteId = note.Id,
                        Title = string.IsNullOrWhiteSpace(note.Title) ? "无标题笔记" : note.Title,
                        Folder = note.Folder,
                        Score = score,
                        Snippets = snippets
                    });
                }
            }

            // 按得分从高到低排序返回
            return Task.FromResult(results.OrderByDescending(x => x.Score).ToList());
        }
    }
}