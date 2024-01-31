using System;
using System.Diagnostics;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace KotoKanade.Core.Util;

public static class MediaUtil
{
	const string ffmpegDownloadPath = "./lib/ffmpeg/";

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
		var hasPath = await IsFFMpegInstalledPathAsync(ctx)
			.ConfigureAwait(false);
		if (hasPath){ return true; }

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
			return false;
		}
	}

	public static bool
	IsFFMpegDownloaded()
	{
		var exists = Directory.Exists(ffmpegDownloadPath);

		if(
			exists &&
			!string.Equals(
				Path.GetFullPath(ffmpegDownloadPath),
				Path.GetFullPath(FFmpeg.ExecutablesPath),
				StringComparison.Ordinal
			)
		)
		{
			FFmpeg.SetExecutablesPath(ffmpegDownloadPath);
		}

		return exists;
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
		await FFmpegDownloader
			.GetLatestVersion(
				FFmpegVersion.Official,
				ffmpegDownloadPath,
				progress
			)
			.ConfigureAwait(false);
		FFmpeg.SetExecutablesPath(ffmpegDownloadPath);
		if(!Directory.Exists(ffmpegDownloadPath)){
			throw new DirectoryNotFoundException("downloaded ffmpeg path not found or cannot access!");
		}
	}
}