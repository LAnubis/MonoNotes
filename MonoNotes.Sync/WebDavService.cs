using MonoNotes.Core.Interfaces;
using System.Net.Http.Headers;
using System.Runtime;
using System.Text;

namespace MonoNotes.Sync
{
    public class WebDavService : IWebDavService
    {
        private readonly ISettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public WebDavService(ISettingsService settingsService)
        {
            _settingsService = settingsService;

            // ==========================================
            // 🌟 跨平台网络终极护城河：强制使用纯血 .NET 引擎
            // ==========================================
            var handler = new SocketsHttpHandler
            {
                // 极其重要：禁止安卓底层自动跟随重定向，防止 PROPFIND 被篡改为 GET！
                AllowAutoRedirect = false
            };

            // 挂载新引擎，并保留你的 5 分钟超时设置
            _httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(5) };

            // 🌟 极其重要：坚果云必须加上伪装的 User-Agent，否则会被防火墙拦截！
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 MonoNotes/1.0");
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
            var finalUrl = string.IsNullOrEmpty(path) ? baseUrl : $"{baseUrl}/{path}";

            // ==========================================
            // 🌟 核心协议修复：智能补全 WebDAV 目录的斜杠
            // ==========================================
            // 判断当前请求的是文件还是文件夹 (通过有无扩展名来判断，比如 .md)
            if (!string.IsNullOrEmpty(Path.GetExtension(finalUrl)))
            {
                // 如果是文件 (例如: /MonoNotes/xxx.md)，绝对不能加斜杠
                return finalUrl;
            }
            else
            {
                // 如果是文件夹 (例如: https://dav.jianguoyun.com/dav)，强制补全斜杠！
                return finalUrl.EndsWith("/") ? finalUrl : finalUrl + "/";
            }
        }

        // 1. 测试连接 (向根目录发送 PROPFIND)
        public async Task<bool> TestConnectionAsync()
        {
            var settings = await _settingsService.GetSettingsAsync();
            var password = await _settingsService.GetSecurePasswordAsync("MonoNotes_WebDav_Password");

            // 🌟 1. 拦截空配置：明确抛出异常，不再让底层瞎猜
            if (string.IsNullOrWhiteSpace(settings.WebDavUrl) ||
                string.IsNullOrWhiteSpace(settings.WebDavUsername) ||
                string.IsNullOrWhiteSpace(password))
            {
                throw new Exception("账号、密码或服务器地址不能为空！");
            }

            var url = NormalizeUrl(settings.WebDavUrl);
            var request = await CreateRequestAsync(new HttpMethod("PROPFIND"), url);
            request.Headers.Add("Depth", "0");

            // 🌟 2. 去掉全局的 try-catch，让网络异常自然抛给 UI
            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            // 🌟 3. 把冰冷的 HTTP 状态码翻译成人类能看懂的红字提示
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                throw new Exception("账号或密码错误 (401)");
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception("找不到服务器路径，请检查 URL 是否填写正确 (404)");
            }
            else
            {
                throw new Exception($"服务器拒绝了连接，状态码: {response.StatusCode}");
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

        // 5. 🌟 高精度获取远程文件列表 (解析 XML 响应)
        public async Task<List<MonoNotes.Core.Models.WebDavItem>> GetRemoteItemsAsync(string remotePath)
        {
            var items = new List<MonoNotes.Core.Models.WebDavItem>();
            try
            {
                var settings = await _settingsService.GetSettingsAsync();
                var url = NormalizeUrl(settings.WebDavUrl, remotePath);

                var request = await CreateRequestAsync(new HttpMethod("PROPFIND"), url);
                request.Headers.Add("Depth", "1"); // 查当前目录及下一层

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode) return items;

                var xmlString = await response.Content.ReadAsStringAsync();
                var doc = System.Xml.Linq.XDocument.Parse(xmlString);
                System.Xml.Linq.XNamespace d = "DAV:";

                var requestedFolderName = remotePath.TrimEnd('/').Split('/').LastOrDefault() ?? "";

                foreach (var responseNode in doc.Descendants(d + "response"))
                {
                    var href = Uri.UnescapeDataString(responseNode.Element(d + "href")?.Value ?? "");
                    var propstat = responseNode.Element(d + "propstat");
                    var prop = propstat?.Element(d + "prop");

                    if (string.IsNullOrEmpty(href) || prop == null) continue;

                    var name = href.TrimEnd('/').Split('/').Last();

                    // 排除父目录自身
                    if (string.IsNullOrEmpty(name) || name == requestedFolderName) continue;

                    var isFolder = prop.Element(d + "resourcetype")?.Element(d + "collection") != null;

                    var lastModifiedStr = prop.Element(d + "getlastmodified")?.Value;
                    DateTimeOffset lastModified = DateTimeOffset.MinValue;
                    if (DateTimeOffset.TryParse(lastModifiedStr, out var parsedDate))
                    {
                        lastModified = parsedDate;
                    }

                    items.Add(new MonoNotes.Core.Models.WebDavItem
                    {
                        Name = name,
                        IsFolder = isFolder,
                        LastModified = lastModified
                    });
                }
            }
            catch { }
            return items;
        }
        public async Task<bool> DeleteItemAsync(string remotePath)
        {
            try
            {
                // 1. 每次请求前，必须实时获取最新的配置
                var settings = await _settingsService.GetSettingsAsync();
                var pwd = await _settingsService.GetSecurePasswordAsync("MonoNotes_WebDav_Password");

                // 确保 URL、账号、密码都不为空
                if (string.IsNullOrEmpty(settings.WebDavUrl) || string.IsNullOrEmpty(settings.WebDavUsername) || string.IsNullOrEmpty(pwd))
                    return false;

                // 2. 获取坚果云的基础 URL（去除末尾多余的斜杠防拼接错误）
                var baseUrl = settings.WebDavUrl.TrimEnd('/');

                // 3. 构建相对路径 (不能带有尾部斜杠，且需要 URL 编码)
                var encodedRelativePath = string.Join("/", remotePath.TrimStart('/').Split('/').Select(Uri.EscapeDataString));

                // 🌟 核心修复：拼接出完整的绝对 URI 
                var fullAbsoluteUrl = $"{baseUrl}/{encodedRelativePath}";

                var request = new HttpRequestMessage(HttpMethod.Delete, fullAbsoluteUrl);

                // 4. 强制压入 Basic Auth 认证头
                var authBytes = System.Text.Encoding.ASCII.GetBytes($"{settings.WebDavUsername}:{pwd}");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));

                var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode || (int)response.StatusCode == 404;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"WebDAV 删除异常: {ex.Message}");
                return false;
            }
        }
    }
}