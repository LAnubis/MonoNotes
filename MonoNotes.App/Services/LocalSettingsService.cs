using Microsoft.Maui.Storage; // 引入 MAUI 的存储与加密库
using MonoNotes.Core;
using MonoNotes.Core.Interfaces;
using MonoNotes.Core.Models;
using System.Text.Json;

namespace MonoNotes.App.Services
{
    public class LocalSettingsService : ISettingsService
    {
        private readonly string _settingsFilePath;

        public LocalSettingsService()
        {
            var rootDirectory = PathHelper.GetBaseDirectory();
            if (!Directory.Exists(rootDirectory)) Directory.CreateDirectory(rootDirectory);

            _settingsFilePath = PathHelper.GetSettingsFilePath();
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
            //await File.WriteAllTextAsync(_settingsFilePath, json);
            await MonoNotes.Core.Utils.JsonSafeWriter.WriteAtomicallyAsync(_settingsFilePath, settings);
        
        }

        // ==========================================
        // 🔒 军工级凭据加密区 (调用底层 OS 钥匙串)
        // ==========================================

        public async Task SaveSecurePasswordAsync(string key, string password)
        {
            try
            {
                // 1. 尝试硬件级加密存储 (Windows DPAPI / 正式版 Mac Keychain)
                await SecureStorage.Default.SetAsync(key, password);
            }
            catch (Exception)
            {
                // 🌟 2. 降级方案：如果没证书导致 Keychain 拒绝访问，捕获异常，降级为普通 Base64 存储
                System.Diagnostics.Debug.WriteLine("⚠️ SecureStorage 硬件加密失败，自动降级为 Base64 软存储。");
                var bytes = System.Text.Encoding.UTF8.GetBytes(password);
                Preferences.Default.Set(key + "_fallback_pwd", Convert.ToBase64String(bytes));
            }
        }

        public async Task<string> GetSecurePasswordAsync(string key)
        {
            try
            {
                var pwd = await SecureStorage.Default.GetAsync(key);
                if (!string.IsNullOrEmpty(pwd)) return pwd;

                // 如果硬件里没有，去软存储里找找看
                return GetFallbackPassword(key);
            }
            catch (Exception)
            {
                // 🌟 硬件读取失败，直接读取降级存储
                return GetFallbackPassword(key);
            }
        }

        public void RemoveSecurePassword(string key)
        {
            SecureStorage.Default.Remove(key);
            Preferences.Default.Remove(key + "_fallback_pwd");
        }

        // 内部降级读取方法
        private string GetFallbackPassword(string key)
        {
            var base64 = Preferences.Default.Get(key + "_fallback_pwd", string.Empty);
            if (string.IsNullOrEmpty(base64)) return string.Empty;

            try
            {
                var bytes = Convert.FromBase64String(base64);
                return System.Text.Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}