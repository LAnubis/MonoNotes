using MonoNotes.Core.Models;

namespace MonoNotes.Core.Interfaces
{
    public interface ISettingsService
    {
        // 基础配置的读写
        Task<AppSettings> GetSettingsAsync();
        Task SaveSettingsAsync(AppSettings settings);

        // 🌟 核心：敏感凭据的系统级加密读写
        Task SaveSecurePasswordAsync(string key, string password);
        Task<string> GetSecurePasswordAsync(string key);
        void RemoveSecurePassword(string key);
    }
}