using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using CevioCasts;
using CevioCasts.UpdateChecker;
using LibSasara.VoiSona.Model.Talk;

namespace KotoKanade.Core.Models;

public static class CastDefManager
{
	[ThreadStatic]
	private static Cast? _cast;
	[ThreadStatic]
	private static Definitions? loadedDefinitions;

	/// <summary>
	/// ボイスライブラリのデータを読み込む。既に読み込み済みならそれを返す。
	/// </summary>
	/// <seealso cref="ReloadCastDefsAsync(CancellationToken)"/>
	public static async ValueTask<Definitions>
	GetAllCastDefsAsync(
		CancellationToken token = default
	)
	{
		return loadedDefinitions ?? await LoadCastDefsAsync(token)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// 強制的にボイスライブラリのデータを読み込む。jsonデータを更新した後などに使う。
	/// </summary>
	/// <seealso cref="GetAllCastDefsAsync(CancellationToken)"/>
	public static ValueTask<Definitions>
	ReloadCastDefsAsync(CancellationToken token = default)
	{
		return LoadCastDefsAsync(token);
	}

	private static async ValueTask<Definitions>
	LoadCastDefsAsync(CancellationToken token = default)
	{
		var path = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"lib/data.json"
		);
		var jsonString = await File
			.ReadAllTextAsync(path, token)
			.ConfigureAwait(false);
		var defs = Definitions.FromJson(jsonString);
		if (defs is null)
		{
#pragma warning disable PH_P007 // Unused Cancellation Token
			await Console.Error.WriteLineAsync($"invalid cast definitions data: {path}")
				.ConfigureAwait(false);
#pragma warning restore PH_P007 // Unused Cancellation Token
			ThrowInvalidException(path);
		}
		loadedDefinitions = defs;
		return defs;

		[DoesNotReturn]
		static void ThrowInvalidException(string path)
		{
			throw new InvalidDataException($"invalid cast definitions data: {path}");
		}
	}

	public static async ValueTask<string> GetVersionAsync(
		CancellationToken token = default
	)
	{
		var def = await GetAllCastDefsAsync(token)
			.ConfigureAwait(false);

		return def.Version;
	}

	public static async ValueTask<string> GetRepositoryVersionAsync(
		CancellationToken token = default
	)
	{
		var defs = await GetAllCastDefsAsync(token)
			.ConfigureAwait(false);
		var update = GithubRelease
			.Build(defs);
		try
		{
			var version = await update
				.GetRepositoryVersionAsync()
				.ConfigureAwait(false);
			return version.ToString();
		}
		catch (System.Exception)
		{
			throw;
		}
	}

	public static async ValueTask<Cast>
	GetSingleCastDefAsync(string castName)
	{
		if (_cast is not null &&
			Array.Exists(_cast.Names, n => string.Equals(n.Display, castName, StringComparison.OrdinalIgnoreCase))
		)
		{
			//読み込み済みならそのまま返す
			return _cast;
		}

		var defs = await CastDefManager
			.GetAllCastDefsAsync()
			.ConfigureAwait(false);

		_cast = Array.Find(defs.Casts,
				c => c.Product == Product.VoiSona
				&& c.Category == CevioCasts.Category.TextVocal
				&& Array.Exists(c.Names, n => string.Equals(n.Display, castName, StringComparison.OrdinalIgnoreCase))
			)
			?? throw new ArgumentException(
				$"cast name {castName} is not found in cast data. please check https://github.com/InuInu2022/cevio-casts/ ",
				nameof(castName));
		return _cast;
	}

	public static async ValueTask<bool> HasUpdateAsync(
		CancellationToken token = default
	)
	{
		var defs = await GetAllCastDefsAsync(token)
			.ConfigureAwait(false);
		var update = GithubRelease
			.Build(defs);
		return await update
			.IsAvailableAsync()
			.ConfigureAwait(false);
	}

	public static async ValueTask UpdateDefinitionAsync(
		IProgress<double>? progress = default,
		CancellationToken token = default
	)
	{
		var defs = await GetAllCastDefsAsync(token)
			.ConfigureAwait(false);
		var update = GithubRelease
			.Build(defs);
		var tempPath = Path.GetTempPath();
		Debug.WriteLine($"temp download: {tempPath}");
		await update
			.DownloadAsync(
				tempPath,//"lib/data.json",
				percent: progress,
				cancellationToken: token
			)
			.ConfigureAwait(false);

		var destPath = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"lib/data.json"
		);

		try
		{
			var tempStream = new FileStream(
				Path.Combine(tempPath,"data.json"),
				FileMode.Open,
				FileAccess.Read,
				FileShare.Read,
				4096,
				FileOptions.Asynchronous);
			var destStream = new FileStream(
				destPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
			await using (tempStream.ConfigureAwait(false))
			{
				await using (destStream.ConfigureAwait(false))
				{
					await tempStream
						.CopyToAsync(destStream, token)
						.ConfigureAwait(false);
				}
			}
		}
		catch (System.Exception e)
		{
			Debug.WriteLine($"{e.Message}");
			throw;
		}
	}

	internal static async ValueTask<Voice>
	GetVoiceByCastNameAsync(
		string castName,
		string version
	)
	{
		var cast = await GetSingleCastDefAsync(castName)
			.ConfigureAwait(false);

		var selectedVer = cast.Versions.Contains(version, StringComparer.Ordinal)
			? version : cast.Versions[^1];

		return new Voice(
			Array.Find(cast.Names, n => n.Lang == Lang.English)?.Display ?? "error",
			cast.Cname,
			selectedVer);
	}

	internal static async Task<double[]?>
	CulcEmoRatesAsync(string? castName, double[]? emotions)
	{
		double[]? rates = null;
		if (castName is not null && emotions is null)
		{
			//感情数を調べる
			var cast = await CastDefManager
				.GetSingleCastDefAsync(castName)
				.ConfigureAwait(false);
			rates = cast
				.Emotions
				.Select(_ => 0.00)
				.ToArray();
			//とりあえず最初の感情だけMAX
			rates[0] = 1.00;
		}
		if (emotions is not null)
		{
			//感情比率設定可能に
			if (emotions.Length == 0)
			{
				await Console.Error.WriteLineAsync($"emotion {emotions} is length 0.")
					.ConfigureAwait(false);
			}
			rates = emotions;
		}

		return rates;
	}
}
