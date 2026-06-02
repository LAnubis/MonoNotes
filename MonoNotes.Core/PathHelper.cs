using System;
using System.Collections.Generic;
using System.Text;

namespace MonoNotes.Core
{
        public static class PathHelper
        {
            // 1. 获取 App 私有根目录 (底层基础)
            public static string GetBaseDirectory()
            {
                string basePath;
#if ANDROID || IOS
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
#else
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#endif
                return Path.Combine(basePath, "MonoNotes");
            }

            // 2. 笔记数据存放区 (Workspaces)
            public static string GetWorkspacesDirectory()
            {
                return EnsureDirectoryExists(Path.Combine(GetBaseDirectory(), "Workspaces"));
            }

            // 3. 配置路径 (settings.json) - 与笔记分开
            public static string GetSettingsFilePath()
            {
                return Path.Combine(GetBaseDirectory(), "settings.json");
            }

            // 4. 缓存/索引路径 (index.json) - 不应该被同步
            public static string GetCacheDirectory()
            {
                return Path.Combine(GetWorkspacesDirectory(), "index.json");
            }

            // 🌟 辅助方法：统一确保文件夹存在，一劳永逸
            private static string EnsureDirectoryExists(string path)
            {
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
                return path;
            }
        }
}
