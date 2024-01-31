using System.Collections.Concurrent;
using System.Diagnostics;

using KotoKanade.Core.Util;

using Xabe.FFmpeg;

namespace KotoKanade.Core.Models;

public sealed class MediaConverter: IAsyncDisposable, IDisposable
{
	public ConcurrentBag<SafeTempFile> SafeTempFiles { get; } = [];
	private static MediaConverter? instance;

	private static bool IsPluginResetted { get; set; }
	private MediaConverter()
	{

		//TODO: unsupported platform show message
	}

	public static async ValueTask<MediaConverter>
	FactoryAsync(
		IProgress<ProgressInfo>? downloadProgress = null
	)
	{
		instance ??= new MediaConverter();

		var result = await MediaUtil
			.IsFFMpegInstalledAsync()
			.ConfigureAwait(false)
			;

		if(!result){
			await MediaUtil
				.DownloadFFMpegAsync(downloadProgress)
				.ConfigureAwait(false);
		}

		IsPluginResetted = true;
		return instance;
	}

	public async ValueTask<SafeTempFile>
	ConvertAsync(
		string filePath,
		IProgress<ConvertProgressInfo>? convertProgress = null
	)
	{
		var result = await ConvertByFFMpegAsync(filePath, convertProgress)
			.ConfigureAwait(false);
		SafeTempFiles.Add(result);
		return result;
	}

	static async ValueTask<SafeTempFile>
	ConvertByFFMpegAsync(
		string filePath,
		IProgress<ConvertProgressInfo>? convertProgress = null
	)
	{
		var info = await FFmpeg.GetMediaInfo(filePath)
			.ConfigureAwait(false);
		//16bit mono 48k wav (PCM signed 16bit little endien)
		var stream = info
			.AudioStreams
			.FirstOrDefault()?
			.SetBitrate(192)	//bitrate 192に統一
			.SetChannels(1)
			.SetSampleRate(48000)
			.SetCodec(AudioCodec.pcm_s16le);

		var temp = new SafeTempFile("wav");

		var conversion = FFmpeg.Conversions.New()
			.AddStream(stream)
			.SetOutput(temp.Path)
			.UseMultiThread(true)
			;
		if(convertProgress is not null)
		{
			conversion.OnProgress += (sender, args) =>
			{
				convertProgress.Report(new(){
					Percent = args.Percent,
				});
				Debug.WriteLine($"convert: {args.Percent}");
			};
		}

		var result = await conversion
			.Start()
			.ConfigureAwait(false)
			;

		Debug.WriteLine($"ffmpeg: {result.Arguments}");
		return temp;
	}

	public record struct ConvertProgressInfo
	{
		public int Percent { get; set; }
	}

	public record SafeTempFile(
		string? Extension = null
	) : IDisposable
	{
		private readonly string tempPath
			= System.IO.Path.ChangeExtension(
				System.IO.Path.Combine(
					System.IO.Path.GetTempPath(),
					System.IO.Path.GetRandomFileName()
				),
				Extension
			);
		private bool _disposed; // To track whether Dispose has been called

		public string Path => tempPath;

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!_disposed)
			{
				if (disposing)
				{
					// Dispose managed state (managed objects).
				}

				// Free unmanaged resources (unmanaged objects) and override a finalizer below.
				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}

				_disposed = true;
			}
		}
	}

	public async ValueTask
	DisposeAsync()
	{
		await Task.Run(Dispose)
			.ConfigureAwait(false);
	}

	public void
	Dispose()
	{
		Parallel
			.ForEach(
				SafeTempFiles,
				f => f.Dispose()
			);
	}
}
