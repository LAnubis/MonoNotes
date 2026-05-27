using System.Runtime.InteropServices;

namespace MonoNotes.Core
{
    public static class AppConstants
    {
        // 🌟 核心升级：放弃不可靠的编译期宏定义，改用绝对准确的运行时系统侦测
        public static string ImageBaseUrl
        {
            get
            {
                // 如果当前运行在 Windows 操作系统上
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return "https://mononotes.local";
                }

                // 默认其他系统 (Mac, iOS, Android 等) 使用 notes 自定义协议
                return "notes://local";
            }
        }
    }
}