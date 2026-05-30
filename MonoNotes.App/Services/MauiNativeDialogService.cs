using Microsoft.Maui.Controls;
using MonoNotes.Core.Interfaces;

namespace MonoNotes.App.Services
{
    // 🌟 这个类只有 MAUI 认识，它负责执行真正的系统级弹窗
    public class MauiNativeDialogService : INativeDialogService
    {
        public async Task ShowAlertAsync(string title, string message, string cancel)
        {
            if (Application.Current?.MainPage != null)
            {
                await Application.Current.MainPage.DisplayAlert(title, message, cancel);
            }
        }
    }
}