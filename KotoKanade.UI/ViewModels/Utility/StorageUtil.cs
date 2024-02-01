using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace KotoKanade.ViewModels;

public static class StorageUtil
{
	private static readonly IStorageProvider? _storage =
		MainWindowUtil
			.GetWindow()?
			.StorageProvider;

	private static readonly string[] appleUniformTypeId = ["content"];

	#region fileformats
	private static readonly FilePickerFileType cevioFiles =
		new("CeVIO files")
		{
			Patterns = [ "*.ccs", "*.ccst" ],
			MimeTypes = ["text/xml"],
			AppleUniformTypeIdentifiers = appleUniformTypeId,
		};
	private static readonly FilePickerFileType cevioProjFile =
		new("CeVIO project file")
		{
			Patterns = [ "*.ccs" ],
			MimeTypes = [ "text/xml" ],
			AppleUniformTypeIdentifiers = appleUniformTypeId,
		};
	private static readonly FilePickerFileType cevioTrackFile =
		new("CeVIO track file")
		{
			Patterns = [ "*.ccst" ],
			MimeTypes = [ "text/xml" ],
			AppleUniformTypeIdentifiers = appleUniformTypeId,
		};
	private static readonly FilePickerFileType labFiles =
		new("Timing label files")
		{
			Patterns = [ "*.lab" ],
			MimeTypes = [ "text/plain" ],
			AppleUniformTypeIdentifiers = appleUniformTypeId,
		};
	private static readonly FilePickerFileType audioFiles =
		new("Audio files")
		{
			Patterns = ["*.wav", "*.mp3", "*.aac", "*.ogg", "*.opus", "*.weba"],
			MimeTypes = ["audio/*"],
			AppleUniformTypeIdentifiers = appleUniformTypeId,
		};
	#endregion

	/// <summary>
	/// open file folder dialog
	/// </summary>
	/// <returns></returns>
	public static async ValueTask<IReadOnlyList<IStorageFile?>?> OpenCevioFileAsync(
		bool allowMultiple,
		string? path,
		string title = "開くファイルを選んでください",
		OpenCcsType openType = OpenCcsType.CssAndCsst
	)
	{
		if(_storage is null){
			return default;
		}

		FilePickerFileType[] types = openType switch
		{
			OpenCcsType.CcsOnly => [cevioProjFile],
			OpenCcsType.CsstOnly => [cevioTrackFile],
			_ => [cevioFiles,cevioProjFile,cevioTrackFile]
		};

		var opt = new FilePickerOpenOptions()
		{
			Title = title,
			AllowMultiple = allowMultiple,
			FileTypeFilter = types,
		};
		if(path is not null){
			opt.SuggestedStartLocation = await _storage
				.TryGetFolderFromPathAsync(path)
				.ConfigureAwait(true);
		}
		return await _storage.OpenFilePickerAsync(opt)
			.ConfigureAwait(true);
	}

	public static async ValueTask<IReadOnlyList<IStorageFile?>?> OpenLabFileAsync(
		bool allowMultiple,
		string? path,
		string title = "開くファイルを選んでください"
	)
	{
		if(_storage is null){
			return default;
		}

		FilePickerFileType[] types = [labFiles];

		var opt = new FilePickerOpenOptions()
		{
			Title = title,
			AllowMultiple = allowMultiple,
			FileTypeFilter = types,
		};
		if(path is not null){
			opt.SuggestedStartLocation = await _storage
				.TryGetFolderFromPathAsync(path)
				.ConfigureAwait(true);
		}
		return await _storage.OpenFilePickerAsync(opt)
			.ConfigureAwait(true);
	}

	public static async ValueTask<IReadOnlyList<IStorageFile?>?> OpenWavFileAsync(
		bool allowMultiple,
		string? path,
		string title = "開く音声ファイルを選んでください"
	)
	{
		if(_storage is null){
			return default;
		}

		FilePickerFileType[] types =
		[
			audioFiles,
			FilePickerFileTypes.All,
		];

		var opt = new FilePickerOpenOptions()
		{
			Title = title,
			AllowMultiple = allowMultiple,
			FileTypeFilter = types,
		};
		if(path is not null){
			opt.SuggestedStartLocation = await _storage
				.TryGetFolderFromPathAsync(path)
				.ConfigureAwait(true);
		}
		return await _storage.OpenFilePickerAsync(opt)
			.ConfigureAwait(true);
	}

	/// <summary>
	/// 読み込んだファイル(IStorageFile)からファイルパスのリストを取得する
	/// </summary>
	/// <param name="opened"></param>
	/// <returns></returns>
	public static IReadOnlyList<string>
	GetPathesFromOpenedFiles(IReadOnlyList<IStorageFile?>? opened)
	{
		if(opened is not {Count: > 0}){
			return [];
		}
		return opened
			.Where(v => v is not null)
			.Select(v => v!.Path.LocalPath)
			.ToImmutableList()
			;
	}

	/// <summary>
	/// ファイルを保存
	/// </summary>
	/// <param name="path"></param>
	/// <param name="patterns"></param>
	/// <param name="title"></param>
	/// <param name="changeExt"></param>
	/// <returns></returns>
	public static async ValueTask<IStorageFile?> SaveAsync(
		string path,
		IReadOnlyList<string> patterns,
		string title = "保存先を選んでください",
		string changeExt = ".new.ccs",
		string targetFileTypes = "ccs"
	)
	{
		if(_storage is null){
			return default;
		}

		var dir = await _storage
			.TryGetFolderFromPathAsync(path)
			.ConfigureAwait(true);
		var fileName = Path.GetFileName(path);
		return await _storage.SaveFilePickerAsync(new()
		{
			Title = title,
			SuggestedStartLocation = dir!,
			SuggestedFileName = Path.ChangeExtension(
				fileName,
				changeExt),
			FileTypeChoices = new FilePickerFileType[]{
				new(targetFileTypes){
					Patterns = patterns,
					AppleUniformTypeIdentifiers = appleUniformTypeId,
				},
			},
		}).ConfigureAwait(true);
	}
}
