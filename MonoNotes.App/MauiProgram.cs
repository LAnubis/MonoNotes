using Microsoft.Extensions.Logging;
using MonoNotes.App.Services;
using MonoNotes.Core.Interfaces;

namespace MonoNotes.App
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
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
#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
