using MonoNotes.Core.Models;

namespace MonoNotes.Core.Interfaces
{
    public interface INoteRepository
    {
        // 🌟 新增：多库管理相关属性和方法
        string CurrentWorkspace { get; }
        Task<List<string>> GetAllWorkspacesAsync();
        Task SwitchWorkspaceAsync(string workspaceName);
        Task CreateWorkspaceAsync(string workspaceName);

        // 获取所有笔记
        Task<List<MonoNote>> GetAllNotesAsync();

        // 保存（或更新）笔记
        Task SaveNoteAsync(MonoNote note);

        // 删除笔记
        Task DeleteNoteAsync(string id);

        Task<List<string>> GetAllFoldersAsync();

        // 在指定的父文件夹下创建一个新文件夹
        Task CreateFolderAsync(string parentFolderPath, string newFolderName);

        // 重命名物理文件夹
        Task RenameFolderAsync(string oldFolderPath, string newFolderName);

        Task<string> SaveAssetAsync(byte[] fileData, string extension);

        Task DeleteFolderAsync(string folderPath, bool moveNotesToTrash);

        Task RenameTagGlobalAsync(string oldTag, string newTag);
        Task DeleteTagGlobalAsync(string targetTag);

        Task<List<MonoNote>> GetAllTemplatesAsync();
        Task SaveTemplateAsync(MonoNote template);
        Task DeleteTemplateAsync(string id);
    }
}