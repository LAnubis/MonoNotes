using Microsoft.AspNetCore.Components.WebView;
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
#endif

namespace MonoNotes.App
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();
            blazorWebView.BlazorWebViewInitialized += BlazorWebView_Initialized;
        }

        private void BlazorWebView_Initialized(object? sender, BlazorWebViewInitializedEventArgs e)
        {
#if WINDOWS
            // 🌟 核心升级：获取根目录（不再是 Data 文件夹）
            var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoNotes");
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            var webView2 = (WebView2)e.WebView;

            // 将 https://mononotes.local 映射到物理根目录
            webView2.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "mononotes.local",
                rootPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
#endif
        }
    }
}