namespace MonoNotes.Core.Interfaces
{
    public interface INativeDialogService
    {
        Task ShowAlertAsync(string title, string message, string cancel);
    }
}