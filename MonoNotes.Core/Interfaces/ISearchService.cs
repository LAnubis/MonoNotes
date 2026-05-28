using MonoNotes.Core.Models;

namespace MonoNotes.Core.Interfaces
{
    public interface ISearchService
    {
        // 传入搜索词和所有笔记，返回按相关性排序的搜索结果
        Task<List<SearchResultItem>> SearchAsync(string keyword, IEnumerable<MonoNote> allNotes);
    }
}