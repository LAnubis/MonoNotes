namespace MonoNotes.Core.Interfaces
{
    public interface IWebDavSyncService
    {
        Task SyncAsync();
        bool HasConflictsInLastSync { get; }
    }
}