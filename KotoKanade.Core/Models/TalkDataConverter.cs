using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using CevioCasts;

using KotoKanade.Core.Util;

using LibSasara;
using LibSasara.Model;
using LibSasara.Model.FullContextLabel;
using LibSasara.VoiSona;
using LibSasara.VoiSona.Model.Talk;

using SharpOpenJTalk.Lang;

using WanaKanaNet;

namespace KotoKanade.Core.Models;

public static partial class TalkDataConverter
{
	[ThreadStatic]
	private static Cast? _defs;

	private static readonly SortedDictionary<int, decimal> defaultTempo = new() { { 0, 120 } };

	public static async ValueTask GenerateFileAsync(
		SongData processed,
		string exportPath,
		string? castName = null,
		(bool isSplit, double threthold)? splitNote = null,
		double[]? emotions = null,
		TalkGlobalParam? globalParams = null,
		decimal consonantOffset = 0.0m
	)
	{
		var TemplateTalk = await TemplateLoader
			.LoadVoiSonaTalkTemplateAsync()
			.ConfigureAwait(true);
		await InitOpenJTalkAsync()
			.ConfigureAwait(true);
		var rates = await CulcEmoRatesAsync(castName, emotions)
			.ConfigureAwait(true);

		processed.TempoList ??= defaultTempo;
		var sw = new Stopwatch();
		sw.Start();

		ImmutableList<Utterance> us;
		if (processed.TimingList?.Any() is not true)
		{
			us = processed
				.PhraseList?
				.AsParallel().AsOrdered()
				.Select(ToUtteranceWithoutLab(processed, rates, globalParams, splitNote, consonantOffset))
				.ToImmutableList()
				?? []
				;
		}
		else
		{
			var zipped = processed
				.PhraseList?
				.Zip(processed.TimingList, (note, LabLine) => (note, LabLine));

			us = zipped?
				//.AsParallel().AsOrdered()
				.Select( tuple => ToUtteranceCore(
					processed,
					tuple.note,
					tuple.LabLine,
					rates,
					globalParams,
					splitNote,
					consonantOffset))
				.ToImmutableList()
				?? []
				;
		}

		if (us is null)
		{
			await Console.Error.WriteLineAsync("解析に失敗しました。。。")
				.ConfigureAwait(false);
			return;
		}

		var tstprj = TemplateTalk
			.ReplaceAllUtterancesAsPrj(us);

		sw.Stop();
		Debug.WriteLine($"★processed: {sw.ElapsedMilliseconds} msec.");

		if (castName is not null)
		{
			var voice = await GetVoiceByCastNameAsync(castName)
				.ConfigureAwait(false);
			tstprj = tstprj
				.ReplaceVoiceAsPrj(voice);
		}

		await LibVoiSona
			.SaveAsync(exportPath, tstprj.Data.ToArray())
			.ConfigureAwait(false);
	}

	private static async Task<double[]?> CulcEmoRatesAsync(string? castName, double[]? emotions)
	{
		double[]? rates = null;
		if (castName is not null && emotions is null)
		{
			//感情数を調べる
			var cast = await GetCastDefAsync(castName)
				.ConfigureAwait(false);
			rates = cast
				.Emotions
				.Select(e => 0.00)
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

	private static async ValueTask<Voice> GetVoiceByCastNameAsync(string castName)
	{
		var cast = await GetCastDefAsync(castName)
			.ConfigureAwait(false);

		return new Voice(
			Array.Find(cast.Names, n => n.Lang == Lang.English)?.Display ?? "error",
			cast.Cname,
			cast.Versions[^1]);
	}

	[ThreadStatic]
	private static Definitions? loadedDefinitions;

	public static async ValueTask<Definitions> GetCastDefinitionsAsync()
	{
		if (loadedDefinitions is not null) { return loadedDefinitions; }

		var path = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory,
			"lib/data.json"
		);
		var jsonString = await Task
			.Run(() => File.ReadAllText(path))
			.ConfigureAwait(false);
		var defs = Definitions.FromJson(jsonString);
		if (defs is null)
		{
			await Console.Error.WriteLineAsync($"invalid cast definitions data: {path}")
				.ConfigureAwait(false);
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

	public static async ValueTask<Cast> GetCastDefAsync(string castName)
	{
		if (_defs is not null &&
			Array.Exists(_defs.Names, n => string.Equals(n.Display, castName, StringComparison.OrdinalIgnoreCase))
		)
		{
			//読み込み済みならそのまま返す
			return _defs;
		}

		var defs = await GetCastDefinitionsAsync()
			.ConfigureAwait(false);

		_defs = Array.Find(defs.Casts,
				c => c.Product == Product.VoiSona
				&& c.Category == CevioCasts.Category.TextVocal
				&& Array.Exists(c.Names, n => string.Equals(n.Display, castName, StringComparison.OrdinalIgnoreCase))
			)
			?? throw new ArgumentException(
				$"cast name {castName} is not found in cast data. please check https://github.com/InuInu2022/cevio-casts/ ",
				nameof(castName));
		return _defs;
	}

	/// <summary>
	/// initialize openjtalk
	/// </summary>
	/// <seealso cref="DisposeOpenJTalkAsync"/>
	private static async ValueTask InitOpenJTalkAsync()
	{
		var path = Path.Combine(
			System.AppDomain.CurrentDomain.BaseDirectory,
			"lib/open_jtalk_dic_utf_8-1.11/");
		_ = await Task
			.Run(() =>
			{
				//_jtalk = new OpenJTalkAPI();
				return _jtalk.Initialize(path);
			})
			.ConfigureAwait(false);
	}

	public static async ValueTask DisposeOpenJTalkAsync()
	{
		await Task.Run(_jtalk.Dispose)
			.ConfigureAwait(false);
	}


	private static Func<List<Note>, Utterance>
	ToUtteranceWithoutLab(
		SongData data,
		double[]? emotionRates = null,
		TalkGlobalParam? globalParams = null,
		(bool isSplit, double threthold)? noteSplit = null,
		decimal consonantOffset = 0.0m
	)
	{
		return p => ToUtteranceCore(
			data,
			p,
			labLines: null,
			emotionRates,
			globalParams,
			noteSplit,
			consonantOffset);
	}

	private static Utterance ToUtteranceCore(
		SongData data,
		List<Note> notes,
		List<LabLine>? labLines = null,
		double[]? emotionRates = null,
		TalkGlobalParam? globalParams = null,
		(bool isSplit, double threthold)? noteSplit = null,
		decimal consonantOffset = 0.0m
	)
	{
		//フレーズをセリフ化
		var text = GetPhraseText(notes);

		//分割
		if (noteSplit?.isSplit is true)
		{
			notes = SplitNoteIfSetOption(data, noteSplit, notes);
		}else{
			//「ー」処理
			notes = ManageLongVowelSymbols(notes);
			//「っ」対応
			notes = ManageCloseConsonant(notes);
		}

		//lab音素をnote由来に合わせて分割
		//TODO: note分割のときにうまくいくかチェック
		if(labLines is not null)
		{
			var (nNotes, nLines) = ManageSameNoteVowels((notes, labLines));
			notes = nNotes;
			labLines = nLines;
		}

		var fcLabel = GetFullContext(notes);

		//フレーズの音素
		var phoneme = GetPhonemeLabel(fcLabel);

		//読み
		var pronounce = GetPronounce(phoneme);
		//アクセントの高低
		//TODO: ノートの高低に合わせる
		//とりあえず数だけ合わせる
		var accent = MakeAccText(fcLabel);
		//調整後LEN
		var timing = labLines is null ? "" : GetDurationsFromLab(labLines);
		//PIT
		//楽譜データだけならnote高さから計算
		//TODO:ccsやwavがあるなら解析して割当
		var pitch = GetPitches(notes, data);

		//フレーズ最初が子音の時のオフセット
		var offset = 0.0m;
		var firstPh = phoneme.Split('|')[0].Split(',')[0];
		if (PhonemeUtil.IsConsonant(firstPh))
		{
			//とりあえず 固定値
			offset = consonantOffset;
		}

		return CreateUtterance(
			data,
			emotionRates,
			consonantOffset,
			notes,
			text,
			phoneme,
			pronounce,
			accent,
			timing,
			pitch,
			offset,
			globalParams,
			labLines
		);
	}

	[SuppressMessage("","S6618")]
	private static Utterance CreateUtterance(
		SongData data,
		double[]? emotionRates,
		decimal consonantOffset,
		List<Note> p,
		string text,
		string phoneme,
		string pronounce,
		string accent,
		string timing,
		string pitch,
		decimal offset,
		TalkGlobalParam? globalParams = null,
		List<LabLine>? labLines = null
	)
	{
		var start = labLines is null
			? GetStartTimeString(data, p, offset)
			: FormattableString
				.Invariant($"{labLines[0].From/10000000:F3}");
		var nu = new Utterance(
			text: text,
			//tsmlを無理やり生成
			tsml: GetTsml(text, pronounce, phoneme, accent),
			//開始時刻
			start:start,
			//書き出しファイル名、とりあえずセリフ
			exportName: $"{text}"
		)
		{
			//感情比率
			//感情数に合わせる
			RawFrameStyle = emotionRates is null
				? "0:1:1.000:0.000:0.000:0.000:0.000"
				: $"0:1:{string.Join(":", emotionRates.Select(em => em / 100.0))}",
			//調整前LEN
			PhonemeOriginalDuration = GetSplittedTiming(p, data, consonantOffset),
		};
		//timing
		if (!string.IsNullOrEmpty(timing))
		{
			nu.PhonemeDuration = timing;
		}
		//pitch
		if (!string.IsNullOrEmpty(pitch))
		{
			nu.RawFrameLogF0 = pitch;
		}
		//global params
		if (globalParams is not null)
		{
			nu.SpeedRatio = globalParams.SpeedRatio;
			nu.C0Shift = globalParams.C0Shift;
			nu.LogF0Shift = globalParams.LogF0Shift;
			nu.LogF0Scale = globalParams.LogF0Scale;
			nu.AlphaShift = globalParams.AlphaShift;
		}
		Debug.WriteLine($"u[{text}], start:{nu.Start}");
		return nu;
	}

	private static List<Note> SplitNoteIfSetOption(
		SongData data,
		(bool isSplit, double threthold)? noteSplit,
		List<Note> p)
	{
		//「ー」対応
		p = ManageLongVowelSymbols(p);
		//「っ」対応
		p = ManageCloseConsonant(p);

		//TODO: labも一緒に分割
		return p.ConvertAll(n =>
		{
			var dur = SasaraUtil
				.ClockToTimeSpan(
					n.Duration,
					data.TempoList ?? defaultTempo)
				.TotalMilliseconds;
			var th = noteSplit?.threthold ?? 100000;
			//th = th < 100 ? 100 : th;
			th = Math.Max(th, 100);

			if (dur < th) return n;

			var spCount = (int)Math.Floor(dur / th) + 1;

			var ph = GetPhonemeLabel(GetFullContext(new List<Note> { n }));
			var sph = ph.Split('|');
			var add = spCount - sph.Length;

			if (add <= 0) return n;

			sph = sph[^1].Split(',')[^1] switch
			{
				//ん
				"N" =>
				[
					.. sph,
					.. Enumerable
						.Repeat("u", add - 1)
						.Append("N"),
				],
				//っ
				"cl" => sph,
				//無効
				"xx" or "sil" or "pau" => sph,
				//それ以外（母音）
				string s =>
					[.. sph, .. Enumerable.Repeat(s, add)],
			};
			n.Phonetic = string
				.Join(",", sph);
			n.Lyric = GetPronounce(string.Join("|", sph));
			return n;
		})
		;
	}

	/// <summary>
	/// 歌詞中の「ー」対応
	/// </summary>
	/// <param name="p"></param>
	[SuppressMessage("", "CA1865")]
	private static List<Note> ManageLongVowelSymbols(List<Note> p)
	{
		//いきなり「ー」で始まるときは「アー」に強制変換
		if (p[0].Lyric?.StartsWith("ー", StringComparison.Ordinal) ?? false)
		{
			p[0].Lyric = $"ア{p[0].Lyric}";
		}

		//途中のノートの「ー」始まり歌詞
		for (var i = 1; i < p.Count; i++)
		{
			var lyric = p[i].Lyric;
			if (lyric is null) continue;
			if (!lyric.StartsWith("ー", StringComparison.Ordinal))
			{
				continue;
			}
			//「ー」の時は前のノートの母音歌詞に置換
			var prev = p[i - 1];
			var ph = GetPhonemeLabel(GetFullContext(new List<Note> { prev }))
				.Split('|')[^1]
				.Split(',')[^1]
				;
			var last = IsInvalidPhoneme(ph) ? "a" : ph;
			p[i].Lyric = lyric
				.Replace(
				"ー",
				GetPronounce(last));
		}
		return p;
	}

	private static bool IsInvalidPhoneme(string ph)
	{
		return string.IsNullOrEmpty(ph) ||
			ph is "cl" or "xx" or "sil" or "pau";
	}

	private static List<Note> ManageCloseConsonant(List<Note> p)
	{
		//途中のノートの「っ」始まり歌詞
		for (var i = 1; i < p.Count; i++)
		{
			var lyric = p[i].Lyric;
			if (lyric is null) continue;
			if (
				lyric[0] is not ('っ' or 'ッ')
			)
			{
				continue;
			}
			//「っ」始まりの時は前のノートの最後の母音を冒頭に付与
			var prev = p[i - 1];
			var ph = GetPhonemeLabel(GetFullContext([prev]))
				.Split('|')[^1]
				.Split(',')[^1]
				;
			var last = IsInvalidPhoneme(ph) ? "a" : ph;
			p[i].Lyric = $"{GetPronounce(last)}{lyric}";
		}
		return p;
	}

	private static (List<Note> notes, List<LabLine> lines) ManageSameNoteVowels(
		(List<Note> notes, List<LabLine> lines) nl
	)
	{
		//同じノートで母音が続いている場合はlab側の母音を分割

		char[] sep = ['|', ','];
		int notePhIndex = 0;

		for(var i = 0; i < nl.notes.Count; i++)
		{
			var lyric = nl.notes[i].Lyric;
			if (lyric is null) continue;
			var ph = GetPhonemeLabel(GetFullContext([nl.notes[i]]));
			ReadOnlySpan<string> span = ph.Split(sep);
			for(var j = 0; j < span.Length; j++){
				if (span[j] is not string tPh) continue;

				var labPh = nl.lines
					.ElementAtOrDefault(notePhIndex + j)?
					.Phoneme;

				if(labPh is null)
				{
					//lab側の長さが短いときは最後の音素を分割
					var (last1, last2) = SplitLabLine(nl.lines[^1]);
					nl.lines = [
						..nl.lines[..^1],
						last1,
						last2,
					];
					labPh = last2.Phoneme;
				}

				if (!string.Equals(tPh, labPh, StringComparison.Ordinal)
					&& IsSameVowels(j, tPh, nl.lines, notePhIndex))
				{
					//lab側が違っていれば前の音素を分割して長さ合わせる
					var prevIndex = notePhIndex + j - 1;
					var prev = nl.lines[prevIndex];

					//前後に分ける
					var (bPrev, aPrev) = SplitLabLine(prev);

					//分割した音素を差し込む
					var next = notePhIndex + j;
					nl.lines = [
						..nl.lines[..prevIndex],
							bPrev, aPrev,
							..nl.lines[next..],
						];
				}
			}
			notePhIndex += span.Length;
		}

		return (nl.notes,nl.lines);
	}

	private static bool IsSameVowels(int j, string tPh, List<LabLine> ln, int notePhIndex)
	{
		return j != 0
			&& PhonemeUtil.IsVowel(tPh)
			&& string.Equals(
				ln[notePhIndex + j - 1].Phoneme,
				tPh,
				StringComparison.Ordinal);
	}

	private static (LabLine first, LabLine second) SplitLabLine(LabLine line)
	{
		var f = new LabLine(line.From, line.To - (line.Length / 2), line.Phoneme);
		var s = new LabLine(line.To - (line.Length / 2), line.To, line.Phoneme);
		return (f, s);
	}

	private static readonly WanaKanaOptions kanaOption = new()
	{
		CustomKanaMapping = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			{"cl","ッ"},
			{"di","ディ"},
		},
	};
	private static string GetPronounce(string phonemes)
	{
		var sb = new StringBuilder(phonemes);
		sb.Replace('|', ' ');
		sb.Replace(",", string.Empty);
		//読みを変えたフレーズ
		var yomi = WanaKana.ToKatakana(sb.ToString(), kanaOption);
		return yomi.Replace(" ", string.Empty);
	}

	private static string GetPitches(
		List<Note> notes,
		SongData data)
	{
		var tempo = data.TempoList ?? defaultTempo;
		List<(TimeSpan start, TimeSpan end, double logF0, int counts)> d = notes
			.ConvertAll(n =>
			(
				start: SasaraUtil
					.ClockToTimeSpan(
						tempo,
						n.Clock
					),
				end: SasaraUtil
					.ClockToTimeSpan(
						tempo,
						n.Clock + n.Duration
					),
				logF0: Math.Log(SasaraUtil
					.OctaveStepToFreq(n.PitchOctave, n.PitchStep)),
				counts: CountPhonemes(n)
			))
			;

		var pitches = Enumerable.Empty<(double ph, double logF0)>().ToList();
		//var offset = d[0].start.TotalMilliseconds;
		var total = 1;  //冒頭sil分offset
		foreach (var (start, end, logF0, counts) in d)
		{
			//TODO:時間を見て分割数を決める？
			//一旦固定分割数で
			const int split = 20;
			var length = split * counts;
			const double add = 1.0 / split;
			for (var i = 0; i <= length; i++)
			{
				var t = total + i * add;
				pitches.Add((t, logF0));
			}

			total += counts;
		}

		var sb = new StringBuilder(100000);
		for (int i = 0; i < pitches.Count; i++)
		{
			var sf = pitches[i].ph
				.ToString("F2", CultureInfo.InvariantCulture);
			var logF0 = pitches[i].logF0;
			sb.Append(sf).Append(':').Append(logF0);
			if (i < pitches.Count - 1) sb.Append(',');
		}
		return sb.ToString();
	}

	private static string MakeAccText(FullContextLab fcLabel)
	{
		var mCount = fcLabel
			.Lines
			.Cast<FCLabLineJa>()
			.First()?
			.UtteranceInfo?
			.Mora
			?? 0
			;
		var ac = Enumerable
			.Range(0, mCount > 1 ? mCount - 1 : 0)
			.Select(s => "h")
			//.ToArray()
			;
		return $"l{string.Concat(ac)}";
	}

	private static string GetPhonemeLabel(
		FullContextLab fcLabel
	)
	{
		var moras = fcLabel.Lines
			.Cast<FCLabLineJa>();
		var splited = FullContextLabUtil
			.SplitByMora(moras)
			.Select(s => s
				.Select(s2 => s2.Phoneme)
				.Where(s2 => !string.Equals(s2, "sil", StringComparison.Ordinal)))
			.Select(s => string.Join(",", s))
			.Where(s => !string.IsNullOrEmpty(s))
			;

		return string.Join("|", splited);
	}

	[SuppressMessage("", "S1854")]
	private static FullContextLab GetFullContext(IEnumerable<Note> notes)
	{
		//_jtalk ??= new OpenJTalkAPI();

		var lyrics = GetPhraseText(notes);
		if (fcLabelCache
			.TryGetValue(lyrics, out var cachedLabel))
		{
			//キャッシュがあればキャッシュを返す
			return cachedLabel;
		}

		var text = Enumerable.Empty<string>();
		lock (_jtalk)
		{
			text = _jtalk.GetLabels(lyrics);
		}

		if (text is null)
		{
			return new FullContextLab(string.Empty);
		}

		return new FullContextLab(string.Join("\n", text));
	}

	/// <summary>
	/// 音素数で等分分割した時間を求める
	/// </summary>
	/// <param name="notes"></param>
	/// <returns></returns>
	private static string GetSplittedTiming(
		List<Note> notes,
		SongData song,
		decimal offset = 0.0m
	)
	{
		var tempo = song.TempoList ?? defaultTempo;
		var timings = notes
			.AsParallel().AsSequential()
			.Select((n, i) =>
			{
				//オフセット準備
				//var is1stNote = i is 0;
				var isConso1stPh = Check1stConsoPhoneme(n);
				var isConsoNextPh = CheckNextConsoPhoneme(notes, i);

				//音素数を数える
				//OpenJTalkで正確に数える
				int count = CountPhonemes(n);

				//開始時間
				var start = SasaraUtil.ClockToTimeSpan(
					tempo,
					n.Clock
				).TotalMilliseconds;
				if (isConso1stPh)
				{
					start -= (double)(offset * 1000);
				}

				//終了時間
				var end = SasaraUtil.ClockToTimeSpan(
					tempo,
					n.Clock + n.Duration
				).TotalMilliseconds;
				if (isConsoNextPh)
				{
					end -= (double)(offset * 1000);
				}

				var repeat = count > 1 && isConso1stPh && offset > 0 ? count - 1 : count;

				//ノートあたりの長さを音素数で等分
				var nLen = (decimal)(end - start);
				nLen = isConso1stPh ? nLen - (offset * 1000) : nLen;
				var sub = nLen / repeat;
				var len = Enumerable
					.Range(0, repeat)
					.Select(_ => sub / 1000m);

				len = isConso1stPh && offset > 0 ? [offset, .. len] : len;

				var str = len.Select(v => v.ToString("N3", CultureInfo.InvariantCulture));
				return string.Join(",", str);
			})
			;
		var s = string
			.Join(",", timings);
		return $"0.005,{s},0.125";
	}

	[SuppressMessage("", "S6618")]
	private static string GetDurationsFromLab(List<LabLine> lines)
	{
		var s = lines
			.Select((line, i)
				=> FormattableString
					.Invariant($"{i+1}:{line.Length / 10000000:F3}")
			)
			;
		return string.Join(",", s);
	}

	private static bool CheckNextConsoPhoneme(List<Note> p, int i)
	{
		var isConsoNext = false;
		if ((i + 1) < p.Count)
		{
			var next = p[i + 1];
			isConsoNext = Check1stConsoPhoneme(next);
		}
		return isConsoNext;
	}

	private static bool Check1stConsoPhoneme(Note n)
	{
		var result = GetPhonemeLabel(
				GetFullContext(new List<Note>() { n })
			).Split('|')[0].Split(',')[0];
		return PhonemeUtil.IsConsonant(result) && !string.Equals(result, "N", StringComparison.Ordinal);
	}

	/// <summary>
	/// フルコンテクストラベルのキャッシュ
	/// </summary>
	private static Dictionary<string, FullContextLab> fcLabelCache = [];
	private static readonly OpenJTalkAPI _jtalk = new();

	private static int CountPhonemes(Note n)
	{
		//ノート歌詞が「ー」の時はOpenJTalkでエラーになるので解析しない
		if (string.Equals(n.Lyric, "ー", StringComparison.Ordinal))
		{
			//母音音素一つになるので1
			return 1;
		}
		if (n.Lyric is null)
		{
			return 1;
		}

		var isCached = fcLabelCache
			.TryGetValue(n.Lyric, out var fullContextLab);
		var fcLabel = isCached
			? fullContextLab!
			: GetFullContext(new List<Note> { n });

		return fcLabel
			.Lines
			.Cast<FCLabLineJa>()
			.Select(s => s.Phoneme)
			//前後sil除外
			.Count(s => !string.Equals(s, "sil", StringComparison.Ordinal));
	}

	/// <summary>
	/// ソング用の特殊ラベルを消してフレーズのセリフを得る
	/// </summary>
	/// <param name="p"></param>
	/// <remarks>
	/// - 「’」（全角アポストロフィ）はトークにもあるが意味が異なるので除外
	/// - 「※$＄」（ファルセット）指定はALPで擬似的に再現できるが将来TODO
	/// </remarks>
	/// <returns></returns>
	private static string GetPhraseText(IEnumerable<Note> p)
	{
		var concated = string
			.Concat(p.Select(n => n.Lyric));

		//TODO: ユーザー辞書対応
		concated = concated
			.Replace(
			"クヮ", "クワ");

		if (string.IsNullOrEmpty(concated))
		{
			concated = "ラ";
		}

		return SpecialLabelRegex
			.Replace(concated, string.Empty);
	}

	private static string GetTsml(
		string text,
		string pronounce,
		string phoneme,
		string accent)
	{
		var bytes = Encoding.UTF8.GetByteCount(pronounce);
		return $"""<acoustic_phrase><word begin_byte_index="0" chain="0" end_byte_index="{bytes}" hl="{accent}" original="{text}" phoneme="{phoneme}" pos="感動詞" pronunciation="{pronounce}">{text}</word></acoustic_phrase>""";
	}

	//	noteから算出
	private static string GetStartTimeString(
		SongData data,
		List<Note> p,
		decimal offset = 0.0m
	)
	{
		var time = SasaraUtil
			.ClockToTimeSpan(
				data.TempoList!,
				p[0].Clock
			);
		var seconds = ((decimal)time.TotalMilliseconds / 1000.0m) - offset;
		Debug.WriteLine($"+ clock:{p[0].Clock}, time:{time.TotalMilliseconds}, seconds:{seconds}");
		return seconds
			.ToString("N3", CultureInfo.InvariantCulture);
	}

#if NET7_0_OR_GREATER
	[GeneratedRegex(
		"[’※$＄@＠%％^＾_＿=＝]",
		RegexOptions.None,
		matchTimeoutMilliseconds: 1000)]
	private static partial Regex SpecialLabelRegexClass();
	private static readonly Regex SpecialLabelRegex = SpecialLabelRegexClass();
#else
	private static readonly Regex SpecialLabelRegex
		= new(
			"[’※$＄@＠%％^＾_＿=＝]",
			RegexOptions.Compiled,
			TimeSpan.FromSeconds(1));
#endif
}