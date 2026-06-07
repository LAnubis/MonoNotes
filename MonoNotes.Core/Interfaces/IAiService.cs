using System.Collections.Generic;
using System.Threading;

namespace MonoNotes.Core.Interfaces
{
    // 聊天消息实体
    public class AiChatMessage
    {
        public string Role { get; set; } = "user"; // system, user, assistant
        public string Content { get; set; } = string.Empty;
    }

    public interface IAiService
    {
        // 🌟 流式对话接口：像打字机一样返回结果，专门适配 Blazor 的 UI 刷新
        IAsyncEnumerable<string> StreamChatCompletionAsync(List<AiChatMessage> messages, CancellationToken cancellationToken = default);
    }
}