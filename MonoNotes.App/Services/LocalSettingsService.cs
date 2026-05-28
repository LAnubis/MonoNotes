using System.Text.Json;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;
using Microsoft.Maui.Storage; // 引入 MAUI 的存储与加密库

namespace MonoNotes.App.Services
{
    public class LocalSettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;

        public LocalSettingsService()
        {
            // 将基础配置文件存在 MonoNotes 根目录
            var rootDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoNotes");
            if (!Directory.Exists(rootDirectory)) Directory.CreateDirectory(rootDirectory);

            _settingsFilePath = Path.Combine(rootDirectory, "settings.json");
        }

        public async Task<AppSettings> GetSettingsAsync()
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings(); // 返回默认设置
            }

            try
            {
                var json = await File.ReadAllTextAsync(_settingsFilePath);
                return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            }
            catch
            {
                return new AppSettings();
            }
        }

        public async Task SaveSettingsAsync(AppSettings settings)
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_settingsFilePath, json);
        }

        // ==========================================
        // 🔒 军工级凭据加密区 (调用底层 OS 钥匙串)
        // ==========================================

        public async Task SaveSecurePasswordAsync(string key, string password)
        {
            if (string.IsNullOrWhiteSpace(password)) return;
            // SecureStorage 会自动处理底层系统的加密调用
            await SecureStorage.Default.SetAsync(key, password);
        }

        public async Task<string> GetSecurePasswordAsync(string key)
        {
            try
            {
                var password = await SecureStorage.Default.GetAsync(key);
                return password ?? string.Empty;
            }
            catch (Exception)
            {
                // 如果用户在系统中重置了密钥库，可能会抛出异常
                return string.Empty;
            }
        }

        public void RemoveSecurePassword(string key)
        {
            SecureStorage.Default.Remove(key);
        }
    }
}