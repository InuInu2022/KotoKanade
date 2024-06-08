using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Notification;

namespace KotoKanade.ViewModels;

public static class NotifyUtil
{
    /// <summary>
	/// エラーメッセージ通知（2行）
	/// </summary>
	/// <param name="manager"></param>
	/// <param name="header">ヘッダーテキスト</param>
	/// <param name="message">詳細メッセージ</param>
	/// <returns></returns>
	public static INotificationMessage Error(
		this INotificationMessageManager manager,
		string header,
		string message
	)
	{
		return manager
			.CreateMessage()
			.Accent("#F15B19")
			.Background("#333")
			.HasHeader(header)
			.HasBadge("Error")
			.HasMessage(message)
			.Dismiss()
			.WithButton("OK", _ => { })
			.Animates(true)
			//.AnimationInDuration(0.35)
			.Queue();
	}

	/// <summary>
	/// 警告メッセージ通知（2行）
	/// </summary>
	/// <param name="manager"></param>
	/// <param name="header">ヘッダーテキスト</param>
	/// <param name="message">詳細メッセージ</param>
	/// <returns></returns>
	public static INotificationMessage Warn(
		this INotificationMessageManager manager,
		string header,
		string message
	)
	{
		return manager
			.CreateMessage()
			.Accent("#E0A030")
			.Background("#333")
			.HasHeader(header)
			.HasBadge("Warn")
			.HasMessage(message)
			.Dismiss()
			.WithButton("OK", _ => { })
			.Animates(true)
			//.AnimationInDuration(0.35)
			.Queue();
	}

	/// <summary>
	/// 情報メッセージ通知（2行）
	/// </summary>
	/// <param name="manager"></param>
	/// <param name="header">ヘッダーテキスト</param>
	/// <param name="message">詳細メッセージ</param>
	public static INotificationMessage Info(
		this INotificationMessageManager manager,
		string header,
		string message,
		bool isDelay = false
	)
	{
		var b =
			manager
				.CreateMessage()
				.Accent("#1751C3")
				.Background("#333")
				.HasHeader(header)
				.HasBadge("Info")
				.HasMessage(message)
				.Animates(true)
				.Dismiss()
			;

		if (isDelay)
		{
			return b.WithDelay(TimeSpan.FromSeconds(5)).Queue();
		}
		else
		{
			return b.WithButton("OK", _ => { }).Queue();
		}
	}

	public static INotificationMessage Loading(
		this INotificationMessageManager manager,
		string header,
		string message,
		bool isIndeterminate = true,
		DownloadProgress? progress = null
	)
	{
		return manager
			.CreateMessage()
			.Accent("#1751C3")
			.Background("#333")
			.HasHeader(header)
			.HasMessage(message)
			.WithOverlay(
				new ProgressBar
				{
					VerticalAlignment = VerticalAlignment.Bottom,
					HorizontalAlignment = HorizontalAlignment.Stretch,
					Height = 3,
					BorderThickness = new Thickness(0),
					Foreground = new SolidColorBrush(Color.FromArgb(128, 128, 128, 255)),
					Background = Brushes.Transparent,
					IsIndeterminate = isIndeterminate,
					IsHitTestVisible = false,
					Minimum = 0.0,
					Maximum = 1.0,
					Value = progress?.Value ?? 0,
				})
			.Animates(true)
			.Queue();
	}
}