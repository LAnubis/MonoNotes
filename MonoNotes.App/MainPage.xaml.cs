using Microsoft.AspNetCore.Components.WebView;
// 确保引入这个命名空间
#if WINDOWS
using Microsoft.UI.Xaml.Controls;
#endif

namespace MonoNotes.App // 你的实际命名空间
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            // 🌟 核心：监听 BlazorWebView 的初始化完成事件
            blazorWebView.BlazorWebViewInitialized += BlazorWebView_Initialized;
        }

        private void BlazorWebView_Initialized(object? sender, BlazorWebViewInitializedEventArgs e)
        {
#if WINDOWS
            // 1. 获取我们真实的本地 Data 文件夹路径
            var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoNotes", "Data");
            if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);

            // 2. 强转为 Windows 平台的 WebView2 对象
            var webView2 = (WebView2)e.WebView;

            // 3. 施展魔法：将 https://mononotes.local 映射到物理硬盘！
            webView2.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "mononotes.local",
                dataPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
#endif
            // (注：如果你以后要打包 Mac 或 Android，可以在这里补充对应的平台方案，目前先完美打通 Windows)
        }
    }
}