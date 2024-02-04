using System.Diagnostics.CodeAnalysis;
using CevioCasts;
using LibSasara.VoiSona.Model.Talk;

namespace KotoKanade.Core.Models;

public static class CastDefManager
{
	[ThreadStatic]
	private static Cast? _cast;
	[ThreadStatic]
	private static Definitions? loadedDefinitions;

	public static async ValueTask<Definitions>
	GetAllCastDefsAsync(
		CancellationToken token = default
	)
	{
		if (loadedDefinitions is not null) { return loadedDefinitions; }

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
