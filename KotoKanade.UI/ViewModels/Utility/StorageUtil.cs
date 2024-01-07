using System.Collections.Generic;
using System.IO;
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
		string changeExt = ".new.ccs"
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
				new("ccs"){
					Patterns = patterns,
					AppleUniformTypeIdentifiers = appleUniformTypeId,
				},
			},
		}).ConfigureAwait(true);
	}
}
