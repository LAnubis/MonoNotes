using Microsoft.AspNetCore.Components.WebView;
using Microsoft.AspNetCore.Components.WebView.Maui;

#if WINDOWS
using Microsoft.UI.Xaml.Controls;
#endif
#if MACCATALYST
using Foundation;
using WebKit;
#endif

namespace MonoNotes.App
{
    public partial class MainPage : ContentPage
    {
        public MainPage()
        {
            InitializeComponent();

            // 🌟 已经清除了所有动态注入 RootComponent 的代码，回归最稳定的 XAML 静态绑定
            // 请务必配合 MainRouter.razor 和 MainPage.xaml 的修改使用

            // Windows 用的初始化后事件
            blazorWebView.BlazorWebViewInitialized += BlazorWebView_Initialized;
            // Mac 用的初始化前事件（因为拦截器必须在内核启动前挂载）
            blazorWebView.BlazorWebViewInitializing += BlazorWebView_Initializing;
        }
        // 🌟 拦截安卓手机的“侧滑返回”和“物理返回键”
        protected override bool OnBackButtonPressed()
        {
            // 阻止系统默认的“退出 App”行为。
            // 这样以后即使你侧滑返回，App 也不会瞬间被关掉了。
            return true;
        }
        private void BlazorWebView_Initializing(object? sender, BlazorWebViewInitializingEventArgs e)
        {
#if MACCATALYST
            // 🌟 Mac 核心防御机制：在 WKWebView 内核启动前，强行注册我们手写的 "notes" 协议拦截器
            e.Configuration.SetUrlSchemeHandler(new NotesSchemeHandler(), "notes");
#endif
        }

        private void BlazorWebView_Initialized(object? sender, BlazorWebViewInitializedEventArgs e)
        {
#if WINDOWS
            // 🌟 Windows 核心机制：直接映射物理文件夹
            var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoNotes");
            if (!Directory.Exists(rootPath)) Directory.CreateDirectory(rootPath);

            var webView2 = (WebView2)e.WebView;
            webView2.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "mononotes.local",
                rootPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
#endif
        }
    }

#if MACCATALYST
    // ======================================================================
    // 🍎 Mac 专属黑科技：底层网络请求拦截器
    // 作用：当 Vditor 试图加载 "notes://local/Workspaces/默认库/.assets/xxx.png" 时，
    // 我们将其拦截，去 Mac 本地硬盘读取对应文件，然后伪装成网络服务器返回给前端。
    // ======================================================================
    public class NotesSchemeHandler : NSObject, IWKUrlSchemeHandler
    {
        public void StartUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        {
            var url = urlSchemeTask.Request.Url;
            if (url.Scheme == "notes")
            {
                var rootPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MonoNotes");
                
                // 1. 获取请求的相对路径 (例如 "/Workspaces/默认库/.assets/xxx.png")
                var relativePath = url.Path.TrimStart('/');
                
                // 2. 极其重要：Mac 浏览器的 URL 会对中文（如"默认库"）进行百分号编码，必须在这里解码回纯中文！
                relativePath = Uri.UnescapeDataString(relativePath);
                
                // 3. 拼接出真实的 Mac 物理路径
                var filePath = Path.Combine(rootPath, relativePath);

                if (File.Exists(filePath))
                {
                    // 将本地文件读入内存
                    var data = NSData.FromFile(filePath);
                    
                    // 推断 MIME 类型，确保浏览器能正确渲染图片
                    var ext = Path.GetExtension(filePath).ToLower();
                    string mimeType = ext switch {
                        ".png" => "image/png",
                        ".jpg" or ".jpeg" => "image/jpeg",
                        ".gif" => "image/gif",
                        ".svg" => "image/svg+xml",
                        ".webp" => "image/webp",
                        _ => "application/octet-stream"
                    };

                    // 伪造 HTTP 响应，发回给前端
                    var response = new NSUrlResponse(url, mimeType, (nint)data.Length, null);
                    urlSchemeTask.DidReceiveResponse(response);
                    urlSchemeTask.DidReceiveData(data);
                    urlSchemeTask.DidFinish();
                    return;
                }
            }
            
            // 404 找不到文件
            var error = new NSError(new NSString("NotesSchemeHandler"), 404);
            urlSchemeTask.DidFailWithError(error);
        }

        public void StopUrlSchemeTask(WKWebView webView, IWKUrlSchemeTask urlSchemeTask)
        {
            // 如果请求被取消，可以在这里清理资源。目前内存直读足够快，无需额外处理。
        }
    }
#endif
}