using System;
using System.Collections.Generic;

namespace MonoNotes.Core.Models
{
    // 🌟 新增：AI 模型提供商实体
    public class AiProvider
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "DeepSeek"; // 给用户的友好名称
        public string BaseUrl { get; set; } = "https://api.deepseek.com/v1"; // 兼容 OpenAI 格式的网关
        public string ModelName { get; set; } = "deepseek-chat";
        // ⚠️ API Key 绝不能存在明文配置里，我们将通过凭证管理器单独加密存储
    }

    public class AppSettings
    {
        // 界面外观：Light / Dark / System
        public string Theme { get; set; } = "System";

        // 编辑器缩放/字号，默认 1.0 (100%)
        public double ZoomLevel { get; set; } = 1.0;

        // 自动保存防抖间隔（毫秒）
        public int AutoSaveIntervalMs { get; set; } = 1500;

        // 是否开启系统文件监听 (FileSystemWatcher)
        public bool EnableFileWatcher { get; set; } = true;

        // 检测到冲突时，是否在 UI 弹出提示框
        public bool ShowConflictNotifications { get; set; } = true;

        // 沉浸式阅读模式（隐藏侧边栏）
        public bool FocusModeEnabled { get; set; } = false;

        // 记录最后一次打开的工作区路径
        public string LastOpenedWorkspacePath { get; set; } = string.Empty;

        // === ☁️ WebDAV 同步设置 ===
        public bool EnableWebDavSync { get; set; } = false;
        public string WebDavUrl { get; set; } = "https://dav.jianguoyun.com/dav/";
        public string WebDavUsername { get; set; } = string.Empty;
        public DateTimeOffset LastSyncTimeUtc { get; set; } = DateTimeOffset.MinValue;

        // === 📦 新增：本地灾备引擎 (Zip 自动打包) ===
        public bool EnableAutoBackup { get; set; } = true;         // 默认开启
        public DayOfWeek BackupDayOfWeek { get; set; } = DayOfWeek.Friday; // 默认周五备份
        public int MaxBackupCount { get; set; } = 3;               // 最多保留 3 份滚动覆盖

        // === 🤖 新增：AI 大脑管理 ===
        public string ActiveAiProviderId { get; set; } = string.Empty;
        public List<AiProvider> AiProviders { get; set; } = new List<AiProvider>();
        public bool EnableAiMaterialExtraction { get; set; } = false;
    }
}