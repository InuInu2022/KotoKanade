using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

using KotoKanade.Core.Util;
using LibSasara.Model;
using LibSasara.Model.FullContextLabel;
using LibSasara.VoiSona;
using LibSasara.VoiSona.Model.Talk;
using MathNet.Numerics.Interpolation;
using SharpOpenJTalk.Lang;

using WanaKanaNet;

namespace KotoKanade.Core.Models;

public sealed partial class TalkDataConverter
{
	public TalkDataConverter()
	{
	}

	private static readonly NLog.Logger Logger
		= NLog.LogManager.GetCurrentClassLogger();

	private const int scaleLabLenToMsec = 10000;
	private const int scaleLabLenToSec = 10000000;
	private static readonly SortedDictionary<int, decimal> defaultTempo = new() { { 0, 120 } };

	public static async ValueTask GenerateFileAsync(
		SongData processed,
		string exportPath,
		string? castName = null,
		(bool isSplit, double threthold)? splitNote = null,
		double[]? emotions = null,
		TalkGlobalParam? globalParams = null,
		decimal consonantOffset = 0.0m,
		string castVersion = "",
		double timeScaleFactor = 0.030
	)
	{
		var TemplateTalk = await TemplateLoader
			.LoadVoiSonaTalkTemplateAsync()
			.ConfigureAwait(true);
		await InitOpenJTalkAsync()
			.ConfigureAwait(true);
		var rates = await CastDefManager
			.CulcEmoRatesAsync(castName, emotions)
			.ConfigureAwait(true);

		var us = ProcessSongData(
			processed,
			rates,
			globalParams,
			splitNote,
			consonantOffset
		);

		if (us is null or [])
		{
			const string msg = "解析に失敗しました。。。";
			Logger.Error(msg);
			await Console.Error.WriteLineAsync(msg)
				.ConfigureAwait(false);
			return;
		}

		var tstprj = TemplateTalk
			.ReplaceAllUtterancesAsPrj(us);

		if (castName is not null)
		{
			var voice = await CastDefManager
				.GetVoiceByCastNameAsync(castName, castVersion)
				.ConfigureAwait(false);
			tstprj = tstprj
				.ReplaceVoiceAsPrj(voice);
		}

		await LibVoiSona
			.SaveAsync(exportPath, tstprj.Data.ToArray())
			.ConfigureAwait(false);
	}

	private static ImmutableList<Utterance>
	ProcessSongData(
		SongData processed,
		double[]? emotionRates,
		TalkGlobalParam? globalParams,
		(bool isSplit, double threthold)? splitNote,
		decimal consonantOffset
	)
	{
		processed.TempoList ??= defaultTempo;
		var sw = new Stopwatch();
		sw.Start();

		ImmutableList<Utterance> us;
		if (processed.TimingList?.Any() is not true)
		{
			us = ProcessModeA(processed, emotionRates, globalParams, splitNote, consonantOffset);
		}
		else if (processed.PitchList?.Any() is not true)
		{
			us = ProcessModeB(processed, emotionRates, globalParams, splitNote, consonantOffset);
		}
		else
		{
			us = ProcessModeC(processed, emotionRates, globalParams, splitNote, consonantOffset);
		}

		sw.Stop();
		Debug.WriteLine($"★processed: {sw.ElapsedMilliseconds} msec.");

		return us;
	}

	/// <summary>
	/// notes only
	/// </summary>
	/// <param name="processed"></param>
	/// <param name="emotionRates"></param>
	/// <param name="globalParams"></param>
	/// <param name="splitNote"></param>
	/// <param name="consonantOffset"></param>
	/// <returns></returns>
	private static ImmutableList<Utterance> ProcessModeA(
		SongData processed,
		double[]? emotionRates,
		TalkGlobalParam? globalParams,
		(bool isSplit, double threthold)? splitNote,
		decimal consonantOffset
	)
	{
		ImmutableList<Utterance> us;
		ImmutableList<List<Note>> pl = processed
			.PhraseList?
			.ToImmutableList()
			?? [];
		try
		{
			us = pl
				.Where(n => n.Count != 0)
				.AsParallel().AsOrdered()
				.WithDegreeOfParallelism(Environment.ProcessorCount)
				.WithMergeOptions(ParallelMergeOptions.NotBuffered)
				.Select(ToUtteranceWithoutLab(
					processed,
					emotionRates,
					globalParams,
					splitNote,
					consonantOffset))
				.AsSequential()
				.ToImmutableList()
				?? [];
		}
		catch (AggregateException e)
		{
			Logger.Info("error at mode A");
			LogErrorWithInnerException(e);
			throw;
		}
		catch (Exception e)
		{
			Logger.Error($"msg: {e.Message}, {e.GetType().Name}, {e.StackTrace}, {e.HResult}");
			throw;
		}

		return us;
	}

	/// <summary>
	/// notes and timing
	/// </summary>
	/// <param name="processed"></param>
	/// <param name="emotionRates"></param>
	/// <param name="globalParams"></param>
	/// <param name="splitNote"></param>
	/// <param name="consonantOffset"></param>
	/// <returns></returns>
	private static ImmutableList<Utterance> ProcessModeB(
		SongData processed,
		double[]? emotionRates,
		TalkGlobalParam? globalParams,
		(bool isSplit, double threthold)? splitNote,
		decimal consonantOffset
	)
	{
		ImmutableList<Utterance> us;
		var zipped = processed
			.PhraseList?
			.Zip(processed.TimingList!, (note, LabLine) => (note, LabLine))
			.ToImmutableList();

		try
		{
			us = zipped?
				.Where(n => n.note.Count != 0)
				.AsParallel().AsOrdered()
				.WithDegreeOfParallelism(Environment.ProcessorCount)
				.WithMergeOptions(ParallelMergeOptions.NotBuffered)
				.Select(tuple => ToUtteranceCore(
					processed,
					tuple.note,
					tuple.LabLine,
					null,
					emotionRates,
					globalParams,
					splitNote,
					consonantOffset))
				.AsSequential()
				.ToImmutableList()
				?? [];
		}
		catch (AggregateException e)
		{
			Logger.Info("error at mode B");
			LogErrorWithInnerException(e);
			throw;
		}
		catch (Exception e)
		{
			Logger.Error($"msg: {e.Message}, {e.GetType().Name}, {e.StackTrace}, {e.HResult}");
			throw;
		}

		return us;
	}

	/// <summary>
	/// notes, timing, and pitches
	/// </summary>
	/// <param name="processed"></param>
	/// <param name="emotionRates"></param>
	/// <param name="globalParams"></param>
	/// <param name="splitNote"></param>
	/// <param name="consonantOffset"></param>
	/// <returns></returns>
	private static ImmutableList<Utterance> ProcessModeC(
		SongData processed,
		double[]? emotionRates,
		TalkGlobalParam? globalParams,
		(bool isSplit, double threthold)? splitNote,
		decimal consonantOffset
	)
	{
		ImmutableList<Utterance> us;
		var zipped = processed
			.PhraseList?
			.Zip(processed.TimingList!, (note, LabLine) => (note, LabLine))
			.Zip(processed.PitchList!, (tuple, f0) => (tuple.note, tuple.LabLine, f0))
			.ToImmutableList();

		try
		{
			us = zipped?
				.Where(n => n.note.Count != 0)
				.AsParallel().AsOrdered()
				.WithDegreeOfParallelism(Environment.ProcessorCount)
				.WithMergeOptions(ParallelMergeOptions.NotBuffered)
				.Select(tuple => ToUtteranceCore(
					processed,
					tuple.note,
					tuple.LabLine,
					tuple.f0,
					emotionRates,
					globalParams,
					splitNote,
					consonantOffset))
				.AsSequential()
				.ToImmutableList()
				?? [];
		}
		catch (AggregateException e)
		{
			Logger.Info("error at mode C");
			LogErrorWithInnerException(e);
			throw;
		}
		catch (Exception e)
		{
			Logger.Error($"msg: {e.Message}, {e.GetType().Name}, {e.StackTrace}, {e.HResult}");
			throw;
		}

		return us;
	}

	private static void LogErrorWithInnerException(AggregateException e)
	{
		Logger.Error(e.Message);
		Logger.Info($"Environment.ProcessorCount:{Environment.ProcessorCount}");
		foreach (var ex in e.Flatten().InnerExceptions)
		{
			Logger.Error($"-Message: {ex.Message}");
			Logger.Error($"-Exception Type: {ex.GetType().Name}");
			Logger.Error($"-Stack Trace: {ex.StackTrace}");
			Logger.Error($"-HResult: {ex.HResult}");
			if (ex is IndexOutOfRangeException)
				Logger.Error($"The data source is corrupt. Query stopped. {ex.Source}");
		}
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
		var userdic = Path.Combine(
			System.AppDomain.CurrentDomain.BaseDirectory,
			"lib/userdic/user.dic"
		);
		_ = await Task
			.Run(() => _jtalk.Initialize(path, userdic))
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
			f0: null,
			emotionRates,
			globalParams,
			noteSplit,
			consonantOffset);
	}

	private static Utterance ToUtteranceCore(
		SongData data,
		List<Note> notes,
		List<LabLine>? labLines = null,
		List<decimal>? f0 = null,
		double[]? emotionRates = null,
		TalkGlobalParam? globalParams = null,
		(bool isSplit, double threthold)? noteSplit = null,
		decimal consonantOffset = 0.0m
	)
	{
		//フレーズをセリフ化
		var text = GetPhraseText(notes);

		//notes,lablines事前処理
		//「ー」処理
		notes = ManageLongVowelSymbols(notes);
		//「っ」対応
		notes = ManageCloseConsonant(notes);
		//lab音素をnote由来に合わせて分割
		if (labLines?.Count > 0)
		{
			var (nNotes, nLines) = ManageSameNoteVowels((notes, labLines));
			notes = nNotes;
			labLines = nLines;
		}
		//分割
		if (noteSplit?.isSplit is true)
		{
			var (ns, ln) = SplitNoteIfSetOption(data, noteSplit, notes, labLines);
			notes = ns;
			labLines = ln;
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
		var pitch = f0 is null
			? GetPitches(notes, data)
			: GetF0(f0, labLines);

		//フレーズ最初が子音の時のオフセット
		var offset = 0.0m;
		var firstPh = string.IsNullOrEmpty(phoneme)
			? string.Empty
			: phoneme
				.Split('|')
				.FirstOrDefault()?
				.Split(',')
				.FirstOrDefault() ?? string.Empty;
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
				.Invariant($"{labLines[0].From/ scaleLabLenToSec:F3}");
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
				: $"0:1:{string.Join(':', emotionRates.Select(em => em / 100.0))}",
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
			nu.HuskyShift = globalParams.HuskyShift;
		}
		Debug.WriteLine($"u[{text}], start:{nu.Start}");
		return nu;
	}

	private static (List<Note>, List<LabLine>?)
	SplitNoteIfSetOption(
		SongData data,
		(bool isSplit, double threthold)? noteSplit,
		List<Note> notes,
		List<LabLine>? labLines = null
	)
	{
		//事前に分割前で発音を取得
		var basePhonemes = GetFullContext(notes);
		var filtered = basePhonemes
			.Lines
			.Where(v => !PhonemeUtil.IsNoSounds(v));
		var basePhraseMoras = FullContextLabUtil
			.SplitByMora(filtered.Cast<FCLabLineJa>());
		DebugDispBasePhAndPron(basePhonemes, basePhraseMoras);

		var baseIndex = 0;
		var moraIndex = 0;
		var phonemeIndex = 0;
		//ノート単位で計算
		var retNotes = notes.Select((n, i) =>
		{
			//分割前の音素数計算
			var notefc = GetFullContext([n])
				.Lines
				.Where(v => !PhonemeUtil.IsNoSounds(v));
			var noteMoraCount = FullContextLabUtil
				.SplitByMora(notefc.Cast<FCLabLineJa>())
				.Count;

			if(basePhraseMoras.Count <= moraIndex || basePhraseMoras.Count < moraIndex + noteMoraCount){
				Debug.Fail("ノート分割で範囲外アクセス");
				Logger.Error($"ノート分割で範囲外アクセス({nameof(SplitNoteIfSetOption)})");
				Logger.Info($"note:{n.Lyric}, basePhraseMoras.Count:{basePhraseMoras.Count}, moraIndex:{moraIndex}, noteMoraCount:{noteMoraCount}");
				return n;
			}
			var baseNoteMoras = basePhraseMoras
				.Skip(moraIndex)
				.Take(noteMoraCount)
				.ToList()
				;
			var basePhonemeCount = baseNoteMoras
				.Sum(v => v.Count);
			DebugDispCompare(i, notefc, baseNoteMoras);

			baseIndex += basePhonemeCount;
			moraIndex += noteMoraCount;

			// 時間をミリ秒で計算
			var dur = labLines is null
				//ノートの長さから計算
				? LibSasara.SasaraUtil
					.ClockToTimeSpan(
						n.Duration,
						data.TempoList ?? defaultTempo)
					.TotalMilliseconds
				//音素から
				: GetDurationByLabLine(n, phonemeIndex, labLines)
				;
			// 分割閾値の設定
			var th = noteSplit?.threthold ?? 100000;
			th = Math.Max(th, 100);

			// 閾値未満の場合、分割なし
			if (dur < th)
			{
				if (labLines is not null)
				{
					phonemeIndex += basePhonemeCount;
				}
				//フレーズ側の音素に置き換え
				n.Phonetic = GetPhoneticFromMora(baseNoteMoras);
				return n;
			}

			// 分割数の計算
			return labLines is null
				//lab無しの場合
				? CulcSplitNumWithoutLab(ref labLines, n, ref phonemeIndex, dur, th, baseNoteMoras)
				//lab有りの場合
				: CulcSplitNumWithLab(ref labLines, n, ref phonemeIndex, th, baseNoteMoras);
		})
		.ToList()
		;
		return (retNotes, labLines);
	}

	private static string GetPhoneticFromMora(
		IEnumerable<List<FCLabLineJa>> baseNoteMoras
	)
	{
		var ph = baseNoteMoras
			.Select(m => string.Join(',', m.Select(p => p.Phoneme)));
		var s = string
			.Join(',', ph)
			.Replace("|", "", StringComparison.Ordinal);
		return s;
	}

	[Conditional("DEBUG")]
	private static void DebugDispBasePhAndPron(
		FullContextLab basePhonemes,
		IList<List<FCLabLineJa>> basePhraseMoras
	){
		var tmp = basePhraseMoras
			.Select((v, i) => $"{i}[{string.Join(',', v.Select(v2 => v2.Phoneme))}]")
			;
		Debug.WriteLine($"BASE PH: {GetPhonemeLabel(basePhonemes)}");
		Debug.WriteLine($"BASE PR: {GetPronounce(GetPhonemeLabel(basePhonemes))}");
		Debug.WriteLine(string.Join("\n", tmp));
	}

	[Conditional("DEBUG")]
	private static void DebugDispCompare(
		int i,
		IEnumerable<FullContextLabLine> notefc,
		List<List<FCLabLineJa>> baseNoteMoras
	)
	{
		var b = string.Join("|",
			FullContextLabUtil
				.SplitByMora(notefc.Cast<FCLabLineJa>())
				.Select(v => string.Join(",", v.Select(v2 => v2.Phoneme)))
			);
		var s = string.Join("|",
			baseNoteMoras.Select(v => string.Join(",", v.Select(v2 => v2.Phoneme))));

		Debug.WriteLine($"Compare n[{i}]:{b}  phrase:{s}");
		Debug.WriteLineIf(!string.Equals(b, s, StringComparison.Ordinal), $"Diff! {b} != {s}");
	}

	// 分割数の計算 labあり
	private static Note CulcSplitNumWithLab(
		ref List<LabLine> labLines,
		Note n,
		ref int phonemeIndex,
		double th,
		IList<List<FCLabLineJa>> baseMoras
	){
		const double minPhLen = 0.025;
		var lines = GetSpanLabLines(labLines, n, phonemeIndex);
		var result = lines
			.Select(ln =>
			{
				//N以外の子音はそのまま
				if (PhonemeUtil.IsConsonant(ln.Phoneme)
				&& !PhonemeUtil.IsNasal(ln.Phoneme))
				{
					return (IsNeedSplit: false, Line: ln);
				}
				//分割しても音素最小値より大きくなるなら分割する
				var msecLen = ln.Length / scaleLabLenToMsec;
				var isNeedSplit =
					msecLen > th
					&& msecLen * 2 > minPhLen;
				return (IsNeedSplit: isNeedSplit, Line: ln);
			})
			;
		// 追加音素不要なら元のノート返却
		if (result.All(v => !v.IsNeedSplit))
		{
			phonemeIndex += baseMoras.Sum(m => m.Count);
			//フレーズ側の音素に置き換え
			n.Phonetic = GetPhoneticFromMora(baseMoras);
			return n;
		}
		// 音素分割拡張
		var sph = baseMoras
			.Select(m=>string.Join(',', m.Select(p=>p.Phoneme)))
			.ToArray()
			;//GetPhonemeLabel(GetFullContext([n])).Split('|');
		var (sp, ln) = ExtendLabLines(sph, result, labLines, th, phonemeIndex);
		sph = sp;
		labLines = ln;
		// 音素と歌詞の更新
		n.Phonetic = string
			.Join(',', sph);
		n.Phonetic = n.Phonetic.Replace("|", "", StringComparison.Ordinal);

		var s = string.Join(',', sph);
		s = s
			.Replace(",|", "|", StringComparison.Ordinal)
			.Replace("|,", ",", StringComparison.Ordinal);
		n.Lyric = GetPronounce(s);
		phonemeIndex += n.Phonetic.Split(',').Length;
		return n;
	}

	// 分割数の計算 labなし
	private static Note CulcSplitNumWithoutLab(
		ref List<LabLine>? labLines,
		Note n,
		ref int phonemeIndex,
		double dur,
		double th,
		IList<List<FCLabLineJa>> baseMoras
	)
	{
		var spCount = (int) Math.Floor(dur / th) + 1;
		//var ph = GetPhonemeLabel(GetFullContext([n]));
		var sph = baseMoras
			.Select(m => string.Join(',', m.Select(p => p.Phoneme)))
			.ToArray();//ph.Split('|');
		var add = spCount - sph.Length;

		// 追加音素不要なら元のノート返却
		if (add <= 0)
		{
			phonemeIndex += baseMoras.Sum(m => m.Count);
			//フレーズ側の音素に置き換え
			n.Phonetic = GetPhoneticFromMora(baseMoras);
			return n;
		}

		// 音素分割拡張
		var (sp, ln) = ExtendPhonemesWithPattern(sph, add, labLines, phonemeIndex);
		sph = sp;
		labLines = ln;
		// 音素と歌詞の更新
		n.Phonetic = string
			.Join(',', sph);
		n.Lyric = GetPronounce(string.Join('|', sph));
		phonemeIndex += n.Phonetic.Split(',').Length;
		return n;
	}

	private static double GetDurationByLabLine(Note note, int phonemeIndex, List<LabLine> labLines)
	{
		var target = GetSpanLabLines(labLines, note, phonemeIndex);
		if (target is []) { return 0.0; }

		var start = target[0].From;
		var last = target[^1].To;
		return (last - start) / scaleLabLenToMsec;
	}

	private static List<LabLine> GetSpanLabLines(List<LabLine> labLines, Note note, int phonemeIndex)
	{
		var ph = GetPhonemeLabel(GetFullContext([note]));
		int phLen = GetPhLen(ph);
		var s = phonemeIndex;
		s = Math.Max(s, 0);
		var e = s + phLen;
		e = Math.Min(e, labLines.Count);
		return labLines[s..e];
	}

	private static int GetPhLen(string ph) => ph.Split([",", "|"], StringSplitOptions.None).Length;

	private static (string[] ph,List<LabLine>? lines) ExtendPhonemesWithPattern(
		string[] sph,
		int add,
		List<LabLine>? labLines,
		int phonemeIndex
	)
	{
		//最後の音素が対象
		var target = sph[^1].Split(',')[^1];
		var targetIndex = sph.Length;
		phonemeIndex += sph
			.Sum(s => s.Split(",").Length) - 1;
		phonemeIndex = phonemeIndex < 0 ? 0 : phonemeIndex;
		var resultPhonemes = target switch
		{
			//ん
			"N" => add > 2 ?
			[
				.. sph[..targetIndex],
				.. Enumerable
					.Repeat("u", add - 1)
					.Append("N"),
				.. sph[targetIndex..],
			]
			: sph,
			//っ
			"cl" => sph,
			//無効
			"xx" or "sil" or "pau" => sph,
			//それ以外（母音）
			string s =>
			[
				.. sph[..targetIndex],
				.. Enumerable.Repeat(s, add),
				.. sph[targetIndex..],
			],
		};
		if(labLines is null){
			return (resultPhonemes, labLines);
		}

		//split lablines
		switch (target)
		{
			case "N":
			{
				//「ンン」にならないようにadd <= 2はskip
				if (add <= 2){ break;}
				if(labLines.Count <= phonemeIndex){
						phonemeIndex = labLines.Count - 1;
				}
				var line = labLines[phonemeIndex];
				var lines = DivideLabLine(line, add+1)
					.Select(ln => new LabLine(ln.From, ln.To, "u"))
					.ToList()
					;
				lines[0] = new(lines[0].From, lines[0].To, "N");
				lines[^1] = new(lines[^1].From, lines[^1].To, "N");

				labLines = [
					..labLines[..phonemeIndex],
					..lines,
					..labLines[(phonemeIndex+1)..],
				];
				break;
			}
			case "cl":
				break;
			case "xx" or "sil" or "pau":
				break;
			case string:
			{
				if(labLines.Count <= phonemeIndex){
					phonemeIndex = labLines.Count - 1;
				}
				var line = labLines[phonemeIndex];
				var lines = DivideLabLine(line, add+1);
				labLines = [
					..labLines[..phonemeIndex],
					..lines,
					..labLines[(phonemeIndex+1)..],
				];
				break;
			}
		}
		return (resultPhonemes, labLines);
	}

	private static (string[] ph, List<LabLine> lines)
	ExtendLabLines(
		string[] moras,
		IEnumerable<(bool IsNeedSplit, LabLine Line)> targetLines,
		List<LabLine> baseLabLines,
		double threthold,
		int currentPhIndex
	)
	{
		//事前計算した音素リストから対象を分割拡張
		var flatMoras = moras
			.Select((m, i) => m
				.Split(',', StringSplitOptions.None)
				.Select(v => (Index: i, Moras: m, Phoneme: v)))
			.SelectMany(m => m);
		Debug.Assert(flatMoras.Take(targetLines.Count() + 1).Count() == targetLines.Count());

		var combined = targetLines
			.Zip(flatMoras, (a, b) => (
				a.IsNeedSplit,
				a.Line,
				b.Index,
				b.Moras,
				b.Phoneme
			));
			//TODO: [d,o]を[d][o]に
		var result = combined
			.Select((t) =>
			{
				if (!t.IsNeedSplit) {return [(t.Line,t.Moras,t.Index)];}

				int div = (int)Math.Floor(t.Line.Length / scaleLabLenToMsec / threthold) + 1;
				var line = PatternSplitLine((t.IsNeedSplit, t.Line), div);
				var mora = PatternSplitMora((t.Line.Phoneme, t.Moras), div);

				return line
					.Zip(mora, (a, b) => (Line: a, Moras: b, Index:t.Index));
				//return line;
			})
			.ToList()
			;
		var flat = result.SelectMany(v => v);
		//TODO: fix
		var resultPhonemes = flat
			.Select(v => v.Line.Phoneme)	//moraだと多い
			.Select(v => v switch
			{
				"a" or "i" or "u" or "e" or "o" or "N"
					=> $"{v}|",
				_ => v,
			})
			.ToArray()
			;
		//var middle = flat.Select(v => v.Line);
		var afterIdx = currentPhIndex + combined.Count();
		/*
		if(baseLabLines.Count <= currentPhIndex
			|| baseLabLines.Count <= afterIdx)
		{
			Debug.Fail("範囲外アクセス発生");
			return (resultPhonemes, baseLabLines);
		}*/
		baseLabLines =
		[
			..baseLabLines[..currentPhIndex],
			..flat.Select(v=>v.Line),
			..baseLabLines[afterIdx..],
		];

		return (resultPhonemes, baseLabLines);
	}

	/// <summary>
	/// moraをパターン別に分割
	/// </summary>
	/// <seealso cref="PatternSplitLine(ValueTuple{bool, LabLine}, int)"/>
	private static IEnumerable<string>
	PatternSplitMora
	(
		(string Phoneme, string Mora) t,
		int div
	)
	{
		IEnumerable<string> ret = [t.Phoneme];
		switch (t.Phoneme)
		{
			case "N":
			{
				//長すぎる「ん」は間に[u]を分割挿入
				//mora
				if(div <= 2){ break; }
				var moras = Enumerable
					.Repeat("u", div)
					.ToArray();
				moras[0] = "N";
				moras[^1] = "N";
				ret = moras;
				break;
			}
			case "cl":
			{
				//TODO: cl pattern
				//長すぎる「っ」は母音を前に付与
				break;
			}
			case "xx" or "sil" or "pau":
				//do nothing
				break;
			case string s:
			{
				var moras = Enumerable
					.Repeat(s, div)
					.ToArray();
				moras[0] = t.Phoneme;
				ret = moras;
				break;
			}
		}
		return ret;
	}

	/// <summary>
	/// <see cref="LabLine"/>をパターン別に分割
	/// </summary>
	/// <seealso cref="PatternSplitMora(ValueTuple{string, string}, int)"/>
	private static IEnumerable<LabLine>
	PatternSplitLine(
		(bool IsNeedSplit, LabLine Line) t,
		int div
	)
	{
		IEnumerable<LabLine> ret = [t.Line];
		switch (t.Line.Phoneme)
		{
			case "N":
			{
				//長すぎる「ん」は間に[u]を分割挿入
				if(div <= 2){ break; }

				//lab
				var lines = DivideLabLine(t.Line, div)
					.Select(ln => new LabLine(ln.From, ln.To, "u"))
					.ToList()
					;
				lines[0] = new(lines[0].From, lines[0].To, "N");
				lines[^1] = new(lines[^1].From, lines[^1].To, "N");

				ret = lines.AsEnumerable();
				break;
			}
			case "cl":
			{
				//TODO: cl pattern
				//長すぎる「っ」は母音を前に付与
				break;
			}
			case "xx" or "sil" or "pau":
				//do nothing
				break;
			case string:
			{
				//母音は単純分割
				var lines = DivideLabLine(t.Line, div);
				ret = lines;
				break;
			}
		}
		return ret;
	}

	private static IEnumerable<LabLine> DivideLabLine(LabLine line, int n)
	{
		var timeStep = (line.To - line.From) / n;

		for (int i = 0; i < n; i++)
		{
			var from = line.From + (timeStep * i);
			var to = (i == n - 1) ? line.To : from + timeStep;
			yield return new LabLine(from, to, line.Phoneme);
		}
	}

	/// <summary>
	/// 歌詞中の「ー」対応
	/// </summary>
	/// <param name="p"></param>
	[SuppressMessage("", "CA1865")]
	private static List<Note> ManageLongVowelSymbols(List<Note> p)
	{
		//いきなり「ー」で始まるときは「アー」に強制変換
		if (p[0].Lyric?.StartsWith('ー') ?? false)
		{
			p[0].Lyric = $"ア{p[0].Lyric}";
		}

		//途中のノートの「ー」始まり歌詞
		for (var i = 1; i < p.Count; i++)
		{
			var lyric = p[i].Lyric;
			if (lyric is null) continue;
			if (!lyric.StartsWith('ー'))
			{
				continue;
			}
			//「ー」の時は前のノートの母音歌詞に置換
			var prev = p[i - 1];
			var ph = GetPhonemeLabel(GetFullContext([prev]))
				.Split('|').LastOrDefault()?
				.Split(',').LastOrDefault()
				?? "a"
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
				.Split('|').LastOrDefault()?
				.Split(',').LastOrDefault() ?? "a"
				;
			var last = IsInvalidPhoneme(ph) ? "a" : ph;
			p[i].Lyric = $"{GetPronounce(last)}{lyric}";
		}
		return p;
	}

	private static (List<Note> notes, List<LabLine> lines) ManageSameNoteVowels(
		(IReadOnlyList<Note> notes, IReadOnlyList<LabLine> lines) nl
	)
	{
		//同じノートで母音が続いている場合はlab側の母音を分割

		char[] sep = ['|', ','];
		int notePhIndex = 0;

		var retNotes = new List<Note>(nl.notes);
		var retLines = new List<LabLine>(nl.lines);
		for(var i = 0; i < nl.notes.Count; i++)
		{
			var lyric = nl.notes[i].Lyric;
			if (lyric is null)
			{
				continue;
			}

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
					retLines = [
						..retLines[..^1],
						last1,
						last2,
					];
					labPh = last2.Phoneme;
				}

				if (!string.Equals(tPh, labPh, StringComparison.Ordinal)
					&& IsSameVowels(j, tPh, retLines, notePhIndex))
				{
					//lab側が違っていれば前の音素を分割して長さ合わせる
					var prevIndex = notePhIndex + j - 1;
					prevIndex = Math.Max(prevIndex, 0);
					prevIndex = Math.Min(prevIndex, nl.lines.Count);
					var prev = nl.lines[prevIndex];

					//前後に分ける
					var (bPrev, aPrev) = SplitLabLine(prev);

					//分割した音素を差し込む
					var next = notePhIndex + j;
					retLines = [
						..retLines[..prevIndex],
						bPrev, aPrev,
						..retLines[next..],
					];
				}
			}
			notePhIndex += span.Length;
		}

		return (retNotes, retLines);
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
		ImmutableList<(TimeSpan start, TimeSpan end, double logF0, int counts)> d = notes
			.ToImmutableList()
			.ConvertAll(n =>
			(
				start: LibSasara.SasaraUtil
					.ClockToTimeSpan(
						tempo,
						n.Clock
					),
				end: LibSasara.SasaraUtil
					.ClockToTimeSpan(
						tempo,
						n.Clock + n.Duration
					),
				logF0: Math.Log(LibSasara.SasaraUtil
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

	private static string GetF0(
		List<decimal> f0,
		List<LabLine>? labLines
	){
		//TODO: 現在はフレーズ単位だが、音素単位に変える

		//set immutable
		var imF0 = f0.ToImmutableArray();
		var imLabLines = labLines?.ToImmutableList() ?? [];

		//Debug.Assert(labLines is null);

		//音素ごとのf0のlistを算出
		const double sample = 0.005;
		var offset = imLabLines[0].From;
		var phonemePitches = imLabLines
			.AsParallel().AsOrdered()
			.Select((ln, i) =>
			{
				//slice
				var sIdx = (int) ((ln.From - offset) / scaleLabLenToSec / sample);
				var eIdx = (int) ((ln.To - offset) / scaleLabLenToSec / sample);
				Debug.Assert(sIdx >= 0 && eIdx <= imF0.Length);
				ReadOnlySpan<decimal> span = imF0
					.AsSpan()[sIdx..eIdx]
					;

				if(span.Length<2)
				{
					return [(ln.Length, (double)span[0] ,i)];
				}

				var idx = Enumerable
					.Range(0, span.Length)
					.Select(i => i * sample)
					;
				var f0Data = span
					.ToArray()
					.Select(v => (double) v)
					;

				//補完データ化
				var interpolate = LogLinear
					.Interpolate(
						idx,
						f0Data
					);

				//取り出し
				var estimated = SplitPointCulclater
					.EstimateRatios(ln.Length / scaleLabLenToSec);

				var result = estimated
					.Ratios
					.Select(v => (
						v,
						interpolate
							.Interpolate(v * ln.Length / scaleLabLenToSec),
						i
					))
					;
				return result;
			})
			.ToImmutableArray()
			;

		//文字列化
		return BuildFrameF0String(phonemePitches);
	}

	private static string BuildFrameF0String(
		ImmutableArray<IEnumerable<(double v, double, int i)>> phonemePitches)
	{
		var sb = new StringBuilder(100000);
		for (var i = 0; i < phonemePitches.Length; i++)
		{
			var phStr = phonemePitches[i]
				.Select(v =>
				{
					var cnt = (v.i + 1 + v.v)
						.ToString("F3", CultureInfo.InvariantCulture);
					var logF0 = Math.Log(v.Item2);
					var isInvalid = double.IsNaN(logF0) || double.IsInfinity(logF0);
					var strF0 = isInvalid
						? string.Empty
						: logF0
							.ToString("F4", CultureInfo.InvariantCulture);
					return $"{cnt}:{strF0}";
				})
				;
			sb.Append(
				CultureInfo.InvariantCulture,
				$"{string.Join(',', phStr)}");
			if (i < phonemePitches.Length - 1) sb.Append(',');
		}
		Debug.WriteLine(sb.ToString());
		return sb.ToString();
	}

	private static string MakeAccText(FullContextLab fcLabel)
	{
		var mCount = fcLabel
			.Lines
			.Cast<FCLabLineJa>()
			.FirstOrDefault()?
			.UtteranceInfo?
			.Mora
			?? 0
			;
		var ac = Enumerable
			.Range(0, mCount > 1 ? mCount - 1 : 0)
			.Select(s => "h")
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
			.Select(s => string.Join(',', s))
			.Where(s => !string.IsNullOrEmpty(s))
			;

		return string.Join('|', splited);
	}

	[SuppressMessage("", "S1854")]
	private static FullContextLab GetFullContext(IEnumerable<Note> notes)
	{
		//_jtalk ??= new OpenJTalkAPI();
		if(!notes.Any()){ return new(string.Empty); }

		var lyrics = GetPhraseText(notes);
		if (fcLabelCache
			.TryGetValue(lyrics, out var cachedLabel))
		{
			//キャッシュがあればキャッシュを返す
			return cachedLabel;
		}

		var text = Enumerable.Empty<string>().ToList();
		lock (_jtalk)
		{
			foreach (var s in SplitString(lyrics))
			{
				var chunk = _jtalk.GetLabels(s);
				text.AddRange(chunk);
			}
			//text = _jtalk.GetLabels(lyrics);
		}

		if (text is [])
		{
			return new(string.Empty);
		}
		//キャッシュを残して保存
		var ret = new FullContextLab(string.Join('\n', text));
		fcLabelCache.TryAdd(lyrics, ret);
		return ret;
	}

	static ReadOnlyCollection<string> SplitString(string text)
	{
		const int chunkMax = 100;
		if (text.Length <= chunkMax)
		{
			return new ReadOnlyCollection<string>([text]);
		}
		var span = text.AsSpan();
		var splitCount = span.Length / chunkMax;
		var index = 0;
		var ret = new List<string>();
		for (var i = 0; i < splitCount; i++)
		{
			var chunk = span.Slice(index, chunkMax);
			index += chunkMax;
			for(var j = 0; j < chunk.Length; j++)
			{
				if (!IsNotSplittable(chunk[^(j+1)..^j]))
				{
					index -= j;
					break;
				}
			}
			ret.Add(chunk.ToString());
		}
		ret.Add(span[index..].ToString());
		return ret.AsReadOnly();

		static bool IsNotSplittable(ReadOnlySpan<char> check)
		{
			return NotSplitRegex().IsMatch(check);
		}
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
			.ToImmutableList()
			//.AsParallel().AsSequential()
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
				var start = LibSasara.SasaraUtil.ClockToTimeSpan(
					tempo,
					n.Clock
				).TotalMilliseconds;
				if (isConso1stPh)
				{
					start -= (double)(offset * 1000);
				}

				//終了時間
				var end = LibSasara.SasaraUtil.ClockToTimeSpan(
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
				Debug.WriteLine($"nLen[{n.Lyric}]: {nLen}");
				var sub = nLen / repeat;
				var len = Enumerable
					.Range(0, repeat)
					.Select(_ => sub / 1000m);

				len = isConso1stPh && offset > 0 ? [offset, .. len] : len;

				var str = len.Select(v => v.ToString("N3", CultureInfo.InvariantCulture));
				return string.Join(',', str);
			})
			;
		var s = string
			.Join(',', timings);
		return $"0.005,{s},0.125";
	}

	[SuppressMessage("", "S6618")]
	private static string GetDurationsFromLab(List<LabLine> lines)
	{
		if(lines is null or []){
			return "1:0";
		}
		var s = lines
			.Select((line, i) =>
			{
				var val = line.Length == 0
					? 0
					: line.Length / 10000000;
				return string.Create(
					CultureInfo.InvariantCulture,
					$"{i + 1}:{val:F3}");
			})
			;
		return string.Join(',', s);
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
		var result = GetPhonemeLabel(GetFullContext([n]));
		var pipe = result.Split('|');
		if(pipe.Length == 0){ return false; }
		var comma = pipe[0].Split(',');
		if(comma.Length == 0){ return false; }
		var check = comma[0];
		return PhonemeUtil
				.IsConsonant(check)
			&& !string
				.Equals(check, "N", StringComparison.Ordinal);
	}

	/// <summary>
	/// フルコンテクストラベルのキャッシュ
	/// </summary>
	private static readonly ConcurrentDictionary<string, FullContextLab> fcLabelCache = [];
	private static readonly OpenJTalkAPI _jtalk = new();

	private static int CountPhonemes(Note n)
	{
		//ノート歌詞が「ー」の時はOpenJTalkでエラーになるので解析しない
		if (string.Equals(n.Lyric, "ー", StringComparison.Ordinal))
		{
			//母音音素一つになるので1
			return 1;
		}
		const int minCount = 1;
		if (n.Lyric is null)
		{
			return minCount;
		}

		var isCached = fcLabelCache
			.TryGetValue(n.Lyric, out var fullContextLab);
		if(fullContextLab is null){
			isCached = false;
		}
		var fcLabel = isCached
			? fullContextLab!
			: GetFullContext([n]);
		fcLabelCache.TryAdd(n.Lyric, fcLabel);

		if(fcLabel.Lines.Count == 0)
		{
			return minCount;
		}

		var ret = fcLabel
			.Lines
			.Cast<FCLabLineJa>()
			.Select(s => s.Phoneme)
			//前後sil除外
			.Count(s => !string.Equals(s, "sil", StringComparison.Ordinal));
		return Math.Max(minCount, ret);
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
		if(!p.Any()) { return string.Empty; }
		var concated = string
			.Concat(p.Select(n => n.Lyric));

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
		var time = LibSasara.SasaraUtil
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

	[GeneratedRegex("[ぁぃぅぇぉァィゥェォゕゖヵヶゃゅょャュョゎヮっッんンー]", RegexOptions.Compiled,1000)]
	private static partial Regex NotSplitRegex();
#else
	private static readonly Regex SpecialLabelRegex
		= new(
			"[’※$＄@＠%％^＾_＿=＝]",
			RegexOptions.Compiled,
			TimeSpan.FromSeconds(1));
#endif
}