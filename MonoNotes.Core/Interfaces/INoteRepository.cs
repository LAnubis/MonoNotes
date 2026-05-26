using MonoNotes.Core.Models;

namespace MonoNotes.Core.Interfaces
{
    public interface INoteRepository
    {
        // 获取所有笔记
        Task<List<MonoNote>> GetAllNotesAsync();

        // 保存（或更新）笔记
        Task SaveNoteAsync(MonoNote note);

        // 删除笔记
        Task DeleteNoteAsync(string id);

        Task<List<string>> GetAllFoldersAsync();
        // 🌟 新增：在指定的父文件夹下创建一个新文件夹
        Task CreateFolderAsync(string parentFolderPath, string newFolderName);
    }
}