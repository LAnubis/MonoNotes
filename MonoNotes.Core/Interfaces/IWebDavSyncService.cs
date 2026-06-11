using System;
using System.Threading.Tasks;

namespace MonoNotes.Core.Interfaces
{
    public interface IWebDavSyncService
    {
        bool HasConflictsInLastSync { get; }

        // 旧的全量拉取机制 (保留用于首次换电脑或强制重置)
        Task SyncAsync(bool isLocalChanged = false);

        // 🌟 新增：获取当前账本里有多少个待同步文件
        Task<int> GetPendingSyncCountAsync(string workspaceName);

        // 🌟 新增：精准推送本地变动，带进度条回调
        Task PushLocalChangesAsync(string workspaceName, Action<int, int, string>? onProgress);
    }
}