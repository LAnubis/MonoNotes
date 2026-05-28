using Microsoft.JSInterop;
using MonoNotes.Core.Models;

namespace MonoNotes.UI.Services
{
    public class VisualEngineService
    {
        private readonly IJSRuntime _jsRuntime;

        public VisualEngineService(IJSRuntime jsRuntime)
        {
            _jsRuntime = jsRuntime;
        }

        // 一键应用所有视觉设置
        public async Task ApplySettingsAsync(AppSettings settings)
        {
            try
            {
                // 1. 应用主题模式 (Light/Dark/System)
                await _jsRuntime.InvokeVoidAsync("visualEngine.setTheme", settings.Theme);

                // 2. 应用全局缩放
                await _jsRuntime.InvokeVoidAsync("visualEngine.setCssVariable", "--nt-zoom-level", settings.ZoomLevel.ToString());

                // 3. (预留) 未来可以在这里继续注入字体、排版密度的变量
                // await _jsRuntime.InvokeVoidAsync("visualEngine.setCssVariable", "--nt-font-family", settings.FontFamily);
            }
            catch
            {
                // 忽略预渲染时的 JS 调用异常
            }
        }
    }
}