using MonoNotes.Core.Models;

namespace MonoNotes.Core.Interfaces
{
    public interface ISyncLogService
    {
        Task LogChangeAsync(string relativePath, SyncActionType action);
        Task<List<SyncLogEntry>> GetLogsAsync();
        Task ClearLogsAsync();
    }
}