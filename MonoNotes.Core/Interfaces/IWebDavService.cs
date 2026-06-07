namespace MonoNotes.Core.Interfaces
{
    public interface IWebDavService
    {
        // 测试账号密码是否正确
        Task<bool> TestConnectionAsync();

        // 🌟 新增：专门给 UI 界面“不保存直接测试”用的方法
        Task<bool> TestSpecificConnectionAsync(string url, string username, string password);

        // 确保云端目录存在（如果不存在则创建）
        Task<bool> EnsureDirectoryExistsAsync(string remotePath);

        // 上传本地文件到云端
        Task<bool> UploadFileAsync(string localFilePath, string remotePath);

        // 从云端下载文件到本地
        Task<bool> DownloadFileAsync(string remotePath, string localFilePath);

        // 获取云端目录下的所有文件信息（包含时间戳和类型）
        Task<List<MonoNotes.Core.Models.WebDavItem>> GetRemoteItemsAsync(string remotePath);
        Task<bool> DeleteItemAsync(string remotePath);
    }
}