using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Notification;
using KotoKanade.Views;

namespace KotoKanade.ViewModels;

public static class MainWindowUtil
{
	/// <summary>
    /// デスクトップアプリの時、<c>MainWindow</c>を返します
    /// </summary>
    /// <returns></returns>
	public static Window? GetWindow(){
		if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			return desktop.MainWindow;
		}

		return default;
	}
}