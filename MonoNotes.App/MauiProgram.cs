using Microsoft.Extensions.Logging;
using MonoNotes.App.Services;
using MonoNotes.Core.Interfaces;
using MonoNotes.Search;
using MonoNotes.Storage;
using MonoNotes.Sync;
using MonoNotes.UI.Services;

namespace MonoNotes.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
#if WINDOWS
        // 🌟 绝招 1：强制把“工作目录”拉回到你的 exe 所在目录
        // 这样无论是双击还是管理员运行，Blazor 都能正确找到 wwwroot 文件夹
        Environment.CurrentDirectory = AppContext.BaseDirectory;

        // 🌟 绝招 2：直接用“全局环境变量”控制 WebView2，比在 XAML 里配置更底层、更暴力
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string cacheFolder = Path.Combine(localAppData, "MonoNotes", "WebView2Cache");
        
        // 必须确保这个文件夹在硬盘上真实存在，否则 WebView2 会直接死机白屏！
        if (!Directory.Exists(cacheFolder))
        {
            Directory.CreateDirectory(cacheFolder);
        }
        
        // 写入系统环境变量，WebView2 启动时会绝对服从这个路径
        Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", cacheFolder);
#endif

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            // 新增：注册本地文件仓储为单例服务
            builder.Services.AddSingleton<INoteRepository, LocalNoteRepository>();
            // 🌟 注册全局设置与加密服务
            builder.Services.AddSingleton<ISettingsService, LocalSettingsService>();
            // 🌟 注册 WebDAV 通讯引擎
            builder.Services.AddSingleton<IWebDavService, WebDavService>();
            builder.Services.AddSingleton<IWebDavSyncService, WebDavSyncService>();
            // 🌟 注册全局搜索引擎
            builder.Services.AddSingleton<ISearchService, LocalSearchService>();
            builder.Services.AddScoped<VisualEngineService>();
            // 🌟 注册本地 JSON 索引缓存引擎 (单例，确保全局只有一个内存实例)
            builder.Services.AddSingleton<JsonIndexService>();
            // 🌟 注册原生弹窗服务
            builder.Services.AddSingleton<INativeDialogService, MauiNativeDialogService>();

            builder.Services.AddSingleton<WriteSpaceRepository>();
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
