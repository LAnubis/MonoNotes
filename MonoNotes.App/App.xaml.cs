namespace MonoNotes.App
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            MainPage = new MainPage();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = base.CreateWindow(activationState);

            window.Created += (s, e) =>
            {
#if WINDOWS
                // 【Windows 平台】获取原生 WinUI3 窗口句柄
                var nativeWindow = window.Handler?.PlatformView;
                var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(windowHandle);
                var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);

                // 方案 A（推荐）：最大化。占满屏幕，但保留底部系统任务栏和应用右上角的红叉
                if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
                {
                    presenter.Maximize();
                }

                // 方案 B：绝对沉浸式全屏（类似打游戏的无边框全屏，按 F11 的效果）
                // 如果你需要方案 B，请把上面的 if 块注释掉，取消下面这句注释：
                // appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);

#elif MACCATALYST
                // 【Mac 平台】获取当前屏幕边界，强制设置 Geometry
                var uiWindow = window.Handler?.PlatformView as UIKit.UIWindow;
                if (uiWindow?.WindowScene != null)
                {
                    // 获取 Mac 屏幕的物理尺寸
                    var screenBounds = uiWindow.WindowScene.Screen.Bounds;
                    
                    // 向 macOS 申请将应用窗口铺满整个屏幕
                  // ✅ .NET 8/9/10 支持的新写法
var geometry = new UIKit.UIWindowSceneGeometryPreferencesMac();
geometry.SystemFrame = screenBounds;
uiWindow.WindowScene.RequestGeometryUpdate(geometry, (error) => { });
                }
#endif
            };

            return window;
        }
    }
}
