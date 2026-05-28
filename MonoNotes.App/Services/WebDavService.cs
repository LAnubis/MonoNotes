using System.Net.Http.Headers;
using System.Text;
using MonoNotes.Core.Interfaces;

namespace MonoNotes.App.Services
{
    public class WebDavService : IWebDavService
    {
        private readonly ISettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public WebDavService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            // 禁用默认的超时限制，防止大文件传输中断
            _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        }

        // 🌟 核心拦截：每次请求前，实时组装 Basic Auth 认证头
        private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string url)
        {
            var request = new HttpRequestMessage(method, url);
            var settings = await _settingsService.GetSettingsAsync();
            var password = await _settingsService.GetSecurePasswordAsync("MonoNotes_WebDav_Password");

            if (string.IsNullOrWhiteSpace(settings.WebDavUsername) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException("未配置 WebDAV 账号或密码");
            }

            var authString = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.WebDavUsername}:{password}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", authString);
            return request;
        }

        private string NormalizeUrl(string baseUrl, string path = "")
        {
            baseUrl = baseUrl.TrimEnd('/');
            path = path.TrimStart('/');
            return string.IsNullOrEmpty(path) ? baseUrl : $"{baseUrl}/{path}";
        }

        // 1. 测试连接 (向根目录发送 PROPFIND)
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var url = NormalizeUrl(settings.WebDavUrl);

                // PROPFIND 是 WebDAV 专属方法
                var request = await CreateRequestAsync(new HttpMethod("PROPFIND"), url);
                request.Headers.Add("Depth", "0"); // 只查当前目录，不深入

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // 2. 确保目录存在 (发送 MKCOL)
        public async Task<bool> EnsureDirectoryExistsAsync(string remotePath)
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var url = NormalizeUrl(settings.WebDavUrl, remotePath);

                var checkReq = await CreateRequestAsync(new HttpMethod("PROPFIND"), url);
                checkReq.Headers.Add("Depth", "0");
                var checkRes = await _httpClient.SendAsync(checkReq);

                if (checkRes.IsSuccessStatusCode) return true; // 目录已存在

                // 目录不存在，尝试创建
                var mkcolReq = await CreateRequestAsync(new HttpMethod("MKCOL"), url);
                var mkcolRes = await _httpClient.SendAsync(mkcolReq);
                return mkcolRes.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // 3. 上传文件 (PUT)
        public async Task<bool> UploadFileAsync(string localFilePath, string remotePath)
        {
            try
            {
                if (!File.Exists(localFilePath)) return false;

                var settings = await _settingsService.GetSettingsAsync();
                var url = NormalizeUrl(settings.WebDavUrl, remotePath);

                var request = await CreateRequestAsync(HttpMethod.Put, url);
                var fileBytes = await File.ReadAllBytesAsync(localFilePath);
                request.Content = new ByteArrayContent(fileBytes);

                var response = await _httpClient.SendAsync(request);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        // 4. 下载文件 (GET)
        public async Task<bool> DownloadFileAsync(string remotePath, string localFilePath)
        {
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var url = NormalizeUrl(settings.WebDavUrl, remotePath);

                var request = await CreateRequestAsync(HttpMethod.Get, url);
                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode) return false;

                var fileBytes = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localFilePath, fileBytes);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // 5. 获取远程文件列表
        public async Task<List<string>> GetRemoteFilesAsync(string remotePath)
        {
            var fileNames = new List<string>();
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var url = NormalizeUrl(settings.WebDavUrl, remotePath);

                var request = await CreateRequestAsync(new HttpMethod("PROPFIND"), url);
                request.Headers.Add("Depth", "1"); // 查当前目录及下一层

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return fileNames;

                // 简单解析 XML 响应 (这里用极其轻量的文本匹配，避免引入重型 XML 库)
                var content = await response.Content.ReadAsStringAsync();
                var matches = System.Text.RegularExpressions.Regex.Matches(content, @"<[a-zA-Z0-9:]*href>(.*?)</[a-zA-Z0-9:]*href>");

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    var href = Uri.UnescapeDataString(match.Groups[1].Value);
                    if (href.EndsWith("/")) continue; // 排除文件夹
                    fileNames.Add(Path.GetFileName(href));
                }
            }
            catch { }
            return fileNames;
        }
    }
}