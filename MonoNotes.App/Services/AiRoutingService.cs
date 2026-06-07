using MonoNotes.Core.Interfaces;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text;

namespace MonoNotes.App.Services
{
    public class AiRoutingService : IAiService
    {
        private readonly ISettingsService _settingsService;
        private readonly HttpClient _httpClient;

        public AiRoutingService(ISettingsService settingsService)
        {
            _settingsService = settingsService;
            _httpClient = new HttpClient();
        }

        public async IAsyncEnumerable<string> StreamChatCompletionAsync(List<AiChatMessage> messages, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var settings = await _settingsService.GetSettingsAsync();
            if (string.IsNullOrEmpty(settings.ActiveAiProviderId))
                throw new Exception("未配置默认的 AI 模型，请前往全局设置中添加。");

            var provider = settings.AiProviders.FirstOrDefault(p => p.Id == settings.ActiveAiProviderId);
            if (provider == null)
                throw new Exception("当前选中的 AI 模型配置已失效，请重新选择。");

            var apiKey = await _settingsService.GetSecurePasswordAsync($"AI_Key_{provider.Id}");
            if (string.IsNullOrEmpty(apiKey))
                throw new Exception($"未找到模型 [{provider.Name}] 的 API Key，请去设置中填写。");

            // 1. 构建 OpenAI 标准格式的请求体，开启 stream = true
            var requestBody = new
            {
                model = provider.ModelName,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }).ToArray(),
                stream = true,
                temperature = 0.7 // 适合写小说的创造力温度
            };

            var requestJson = JsonSerializer.Serialize(requestBody);
            var request = new HttpRequestMessage(HttpMethod.Post, $"{provider.BaseUrl.TrimEnd('/')}/chat/completions");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // 2. 发送请求，获取响应流
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new Exception($"AI 接口调用失败 ({response.StatusCode}): {errorMsg}");
            }

            using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            // 3. 极速解析 Server-Sent Events (SSE) 协议
            // 3. 极速解析 Server-Sent Events (SSE) 协议
            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);

                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: "))
                    continue;

                var data = line.Substring(6).Trim();

                if (data == "[DONE]")
                    break;

                // 🌟 修复 CS1626：声明临时变量存放要返回的文本
                string? contentToYield = null;

                try
                {
                    // 提取流式返回的增量文字 (delta)
                    using var doc = JsonDocument.Parse(data);
                    var delta = doc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var contentElement))
                    {
                        contentToYield = contentElement.GetString();
                    }
                }
                catch
                {
                    // 忽略 JSON 解析错误，继续读取下一行
                }

                // 🌟 将 yield return 移到 try-catch 块的外部
                if (!string.IsNullOrEmpty(contentToYield))
                {
                    yield return contentToYield; // 像打字机一样吐出这个字
                }
            }
        }
    }
}