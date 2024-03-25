using System;
using System.Diagnostics;
using KotoKanade.Core.Models;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace KotoKanade.Core.Util;

public static class MediaUtil
{
	const string ffmpegDownloadPath = "./lib/ffmpeg/";

	private static readonly NLog.Logger Logger
		= NLog.LogManager.GetCurrentClassLogger();

	/// <summary>
	/// まとめてチェック
	/// </summary>
	/// <param name="ctx"></param>
	/// <returns></returns>
	public static async ValueTask<bool>
	IsFFMpegInstalledAsync(
		CancellationToken ctx = default
	){
		//path通ってる？
		if(!SettingManager.IsForceUseDownloadedFFMpeg){
			var hasPath = await IsFFMpegInstalledPathAsync(ctx)
			.ConfigureAwait(false);
			if (hasPath){ return true; }
		}

		//通ってなければ独自パスある？
		if (HasFFMpegExePath()) { return true; }

		//標準ダウンロード先にダウンロード済？
		return IsFFMpegDownloaded();
	}

	public static async ValueTask<bool>
	IsFFMpegInstalledPathAsync(
		CancellationToken ctx = default
	)
	{
		var processStartInfo = new ProcessStartInfo
		{
			FileName = "ffmpeg",
			Arguments = "-version",
			UseShellExecute = false,
			RedirectStandardOutput = true,
			RedirectStandardError = true,
			CreateNoWindow = true,
		};

		try
		{
			using var process = Process.Start(processStartInfo);
			if (process is null) { return false; }
			await process
				.WaitForExitAsync(ctx)
				.ConfigureAwait(false);
			var output = await process
				.StandardOutput
				.ReadToEndAsync(ctx)
				.ConfigureAwait(false);
			return output
				.Contains("ffmpeg version", StringComparison.Ordinal);
		}
		catch
		{
			Logger.Warn("ffmpeg path check error.");
			return false;
		}
	}

	public static bool
	IsFFMpegDownloaded()
	{
		var isDlDirExists = Directory.Exists(ffmpegDownloadPath);
		var isExePathExists = Path.Exists(FFmpeg.ExecutablesPath);

		if(
			isDlDirExists && !isExePathExists
		)
		{
			FFmpeg.SetExecutablesPath(ffmpegDownloadPath);
		}

		return isDlDirExists;
	}

	public static bool
	HasFFMpegExePath()
	{
		if(string.IsNullOrEmpty(FFmpeg.ExecutablesPath)){ return false; }
		return Directory.Exists(FFmpeg.ExecutablesPath);
	}

	public static async ValueTask
	DownloadFFMpegAsync(
		IProgress<ProgressInfo>? progress = null
	)
	{
		Logger.Info($"Start download FFMpeg to {ffmpegDownloadPath}");
		await FFmpegDownloader
			.GetLatestVersion(
				FFmpegVersion.Official,
				ffmpegDownloadPath,
				progress
			)
			.ConfigureAwait(false);
		FFmpeg.SetExecutablesPath(ffmpegDownloadPath);
		if(!Directory.Exists(ffmpegDownloadPath)){
			const string msg = $"downloaded ffmpeg path ({ffmpegDownloadPath}) not found or cannot access!";
			Logger.Error(msg);
			throw new DirectoryNotFoundException(msg);
		}
	}
}