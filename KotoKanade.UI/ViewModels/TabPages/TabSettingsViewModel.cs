using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Epoxy;
using KotoKanade.Core.Models;
using KotoKanade.Core.Util;

namespace KotoKanade.ViewModels;

[ViewModel]
public class TabSettingsViewModel
{
	public Command? Ready { get; set; }
	public Command? OpenLicense { get; set; }

	public Command? ResetAllSettings { get; private set; }

	public Command? DownloadCastData { get; private set; }

	public MainViewModel? MainViewModel { get; set; }
	public string? AppName { get; set; }
	public string? AppVer { get; set; }
	public string LatestAppVer { get; set; } = string.Empty;
	public string AppDownloadPath { get; set; } = "https://github.com/InuInu2022/KotoKanade/releases";
	public string CastDataVersion { get; set; } = string.Empty;
	public string LatestCastDataVersion { get; set; } = string.Empty;
	internal DownloadProgress? DlProgress { get; private set; }

	public TabSettingsViewModel()
	{
		AppName = AppUtil.GetAppName();

		AppVer = $"ver. {AppUtil.GetAppVer()}";

		Ready = Command.Factory.Create(GetReady());

		OpenLicense = Command.Factory.
			Create(async () =>
			{
				var path = Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					@"licenses\"
				);
				var command = string.Empty;
				if (System.OperatingSystem.IsWindows())
				{
					command = "explorer.exe";
				}
				else if (System.OperatingSystem.IsMacOS())
				{
					command = "open";
				}
				else if (System.OperatingSystem.IsLinux())
				{
					command = "xdg-open";
				}
				else
				{
					throw new NotSupportedException("非対応プラットフォームです");
				}
				await Task.Run(() => Process.Start(command, path))
				.ConfigureAwait(false);
			});

		ResetAllSettings = Command.Factory.Create(async () =>
		{
			await SettingManager
				.ResetAllAsync()
				.ConfigureAwait(false);
			RestartApp();
		});

		DownloadCastData = Command.Factory.Create(SetDownloadCastDataEvent());
	}

	private static void RestartApp()
	{
		MainWindowUtil
			.GetDesktop()?
			.MainWindow?
			.Close();

		// アプリケーションを再起動する
		Process.Start(Environment.ProcessPath!);

		// 現在のアプリケーションを終了する
		MainWindowUtil
			.GetDesktop()?
			.Shutdown();
	}

	private Func<ValueTask> SetDownloadCastDataEvent()
	{
		return async () =>
		{
			var notify = MainViewModel.Manager;
			DlProgress = new DownloadProgress();

			var loading = notify
				.Loading(
					"Cast data downloading...",
					"ボイスライブラリデータを更新しています。",
					progress: DlProgress,
					isIndeterminate: true);

			await CastDefManager
				.UpdateDefinitionAsync(DlProgress)
				.ConfigureAwait(true);

			await CastDefManager
				.ReloadCastDefsAsync()
				.ConfigureAwait(true);

			if (MainViewModel is not null)
			{
				MainViewModel.HasCastDataUpdate = false;
				await MainViewModel
					.CheckAsync()
					.ConfigureAwait(true);
				MainViewModel.HasUpdate = MainViewModel.HasAppUpdate;
				await MainViewModel
					.LoadCastDataAsync(true)
					.ConfigureAwait(true);
			}

			notify.Dismiss(loading);
		};
	}

	private Func<ValueTask> GetReady()
	{
		return async () =>
		{
			CastDataVersion = "ver. " + await CastDefManager
				.GetVersionAsync()
				.ConfigureAwait(true);
			try
			{
				LatestCastDataVersion = "ver. " + await CastDefManager
					.GetRepositoryVersionAsync()
					.ConfigureAwait(true);

				var update = UpdateChecker.Build();
				LatestAppVer = await update
					.GetRepositoryVersionAsync()
					.ConfigureAwait(true)
				;
				AppDownloadPath = await update
					.GetDownloadUrlAsync()
					.ConfigureAwait(true);
			}
			catch (Exception ex)
			{
				var notify = MainViewModel.Manager;
				notify.Warn("Update check failed.",$"更新を確認できませんでした。ネットワーク接続を確認するかキャストデータを確認してください。 {ex.Message}");
			}
		};
	}
}

public sealed class DownloadProgress : IProgress<double>
{
	public double Value { get; private set; }
	public void Report(double value)
	{
		Value = value;
	}
}