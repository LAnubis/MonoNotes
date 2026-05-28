using System;

namespace MonoNotes.Core.Models
{
    public class AppSettings
    {
        // 界面外观：Light / Dark / System
        public string Theme { get; set; } = "System";

        // 编辑器缩放/字号，默认 1.0 (100%)
        public double ZoomLevel { get; set; } = 1.0;

        // 自动保存防抖间隔（毫秒），PRD建议 800ms - 1500ms
        public int AutoSaveIntervalMs { get; set; } = 1500;

        // 是否开启系统文件监听 (FileSystemWatcher)
        public bool EnableFileWatcher { get; set; } = true;

        // 检测到冲突时，是否在 UI 弹出提示框
        public bool ShowConflictNotifications { get; set; } = true;

        // 沉浸式阅读模式（隐藏侧边栏）
        public bool FocusModeEnabled { get; set; } = false;

        // 记录最后一次打开的工作区路径，方便下次启动直接加载
        public string LastOpenedWorkspacePath { get; set; } = string.Empty;

        // === 🌟 新增：WebDAV 同步设置 ===
        public bool EnableWebDavSync { get; set; } = false;
        // 默认填入坚果云的 WebDAV 地址
        public string WebDavUrl { get; set; } = "https://dav.jianguoyun.com/dav/";
        public string WebDavUsername { get; set; } = string.Empty;
    }
}