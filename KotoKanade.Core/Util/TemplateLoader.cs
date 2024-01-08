using LibSasara;
using LibSasara.Model;
using LibSasara.VoiSona;
using LibSasara.VoiSona.Model.Talk;

namespace KotoKanade.Core.Util;

public static class TemplateLoader
{
	static readonly string CcstAudioTemplate
		= Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"Templates/audio_track.ccst"
		);

	static readonly string CcstSongTemplate
		= Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"Templates/song_track.ccst"
		);

	static readonly string CcstTalkTemplate
		= Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"Templates/talk_track.ccst"
		);

	static readonly string CcsProjectTemplate
		= Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"Templates/project.ccs"
		);

	static readonly string TstprjTalkTemplate
		= Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"Templates/template.tstprj"
		);

	public static async ValueTask<TstPrj>
	LoadVoiSonaTalkTemplateAsync(CancellationToken ctx = default)
	{
		return await LibVoiSona
			.LoadAsync<TstPrj>(TstprjTalkTemplate, ctx)
			.ConfigureAwait(false);
	}

	public static async ValueTask<CcstTrack> LoadAudioTrackAsync()
	{
		return await LoadAnyTrackAsync(CcstAudioTemplate)
			.ConfigureAwait(false);
	}

	public static async ValueTask<CcstTrack>
	LoadTalkTrackAsync()
	{
		return await LoadAnyTrackAsync(CcstTalkTemplate)
			.ConfigureAwait(false);
	}

	public static async ValueTask<CcstTrack>
	LoadSongTrackAsync()
	{
		return await LoadAnyTrackAsync(CcstSongTemplate)
			.ConfigureAwait(false);
	}

	/// <summary>
    /// テンプレートのCeVIOプロジェクトファイルを読み込む
    /// </summary>
    /// <returns>テンプレートCeVIOプロジェクトファイル</returns>
    /// <exception cref="InvalidDataException"></exception>
	public static async ValueTask<CcsProject>
	LoadProjectAsync()
	{
		return await SasaraCcs
			.LoadAsync<CcsProject>(CcsProjectTemplate)
			.ConfigureAwait(false)
			?? throw new InvalidDataException("loaded project is invalid");
	}

	/// <summary>
    /// 任意のテンプレートトラックファイルを読み込む
    /// </summary>
    /// <param name="path"></param>
    /// <seealso cref="LoadAudioTrackAsync"/>
    /// <seealso cref="LoadTalkTrackAsync"/>
    /// <seealso cref="LoadSongTrackAsync"/>
    /// <exception cref="InvalidDataException"></exception>
	public static async ValueTask<CcstTrack>
	LoadAnyTrackAsync(string path)
	{
		var track = await SasaraCcs
			.LoadAsync<CcstTrack>(path)
			.ConfigureAwait(false)
			?? throw new InvalidDataException($"{path} is invalid!");
		return track;
	}
}
