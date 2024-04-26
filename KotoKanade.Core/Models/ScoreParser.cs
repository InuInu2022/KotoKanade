using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using LibSasara;
using LibSasara.Model;

namespace KotoKanade.Core.Models;

public static class ScoreParser
{
	private static readonly NLog.Logger Logger
		= NLog.LogManager.GetCurrentClassLogger();
	/// <summary>
	/// songのccs/ccstデータを解析する
	/// </summary>
	/// <remarks>
	/// 最初のSongトラックのみ
	/// オプションで再現度合いを切り替え
	///  1. 楽譜データのみ
	///  2. 楽譜＋調声データ
	///  3. 楽譜＋フルPITCH(wav)＋フルTMG(lab)
	/// </remarks>
	/// <param name="path"></param>
	/// <returns></returns>
	public static async ValueTask<SongData>
	ProcessCcsAsync(
		string path,
		string? labPath = null,
		string? wavPath = null,
		bool useLab = false,
		bool useWav = false
	)
	{
		var ccs = await SasaraCcs.LoadAsync(path)
			.ConfigureAwait(false);
		var trackset = ccs
			.GetTrackSets<SongUnit>()
			.FirstOrDefault();
		var song = trackset?
			.Units
			.FirstOrDefault();
		if (trackset is null || song is null)
		{
			var msg = $"Error!: ソングデータがありません: {path}";
			Logger.Error(msg);
			await Console.Error.WriteLineAsync(msg)
				.ConfigureAwait(false);
			return new SongData();
		}

		var songData = new SongData()
		{
			//トラック名
			SongTrackName = trackset.Name,
			//楽譜のテンポ・ビート
			TempoList = song.Tempos,
			BeatList = song.Beat,
			//1note=1uttranceは重いはず
			//なので休符で区切ったphrase単位に
			PhraseList = SplitByPhrase(song, 0),
		};

		//labファイルのパスを指定したとき
		await LoadAndSetTimingListAsync(labPath, useLab, useWav, songData)
			.ConfigureAwait(false);

		//音声ファイルを指定したとき
		await ProcessPitchAndTimingFromWavAsync(wavPath, useWav, songData)
			.ConfigureAwait(false);

		//中間データに解析・変換
		return songData;
	}

	// caches
	static readonly ConcurrentDictionary<ulong, (int, int, int, double[])> WavCache = [];
	static readonly ConcurrentDictionary<ulong, WorldParam> EstimatedCache = [];

	private static async ValueTask
	ProcessPitchAndTimingFromWavAsync(
		string? wavPath,
		bool useWav,
		SongData songData
	)
	{
		bool isTarget = useWav && !string.IsNullOrEmpty(wavPath) && File.Exists(wavPath) && songData.TimingList?.Any() == true;
		if(!isTarget){ return; }

		var sw = SWStart(wavPath);
		//get wav hash
		var fileStream = new FileStream(wavPath!, FileMode.Open);
		var hasher = new System.IO.Hashing.XxHash3();
		await using (fileStream.ConfigureAwait(false))
		{
			await hasher
				.AppendAsync(fileStream)
				.ConfigureAwait(false);
		}
		var wavHash = hasher.GetCurrentHashAsUInt64();
		hasher.Reset();

		SWFactory(sw);
		var hasCached = WavCache
			.TryGetValue(wavHash, out (int, int, int, double[]) wavCache);
		MediaConverter.SafeTempFile? wav = default;
		if (!hasCached)
		{
			var mc = await MediaConverter
				.FactoryAsync()
				.ConfigureAwait(false);

			wav = await mc
				.ConvertAsync(wavPath!)
				.ConfigureAwait(false);
		}
		SWConvert(sw);
		//estimate by world
		var (fs, nbit, len, x) = hasCached
			? wavCache
			: await WorldUtil
				.ReadWavAsync(wav?.Path ?? string.Empty)
				.ConfigureAwait(false);
		if (!hasCached)
		{
			WavCache.TryAdd(wavHash, (fs, nbit, len, x));
		}
		SWReadWav(sw);
		var wp = new WorldParam(fs);
		var hasCachedEstimated = EstimatedCache
			.TryGetValue(wavHash, out var cachedEstimated);
		var bottom = SettingManager.DoAutoTuneThreshold
			? songData.GetBottomPitch()
			: SettingManager.BottomEstimateThrethold;
		bottom = Math.Min(bottom, 70.0);
		if(SettingManager.DoAutoTuneThreshold)
		{
			SettingManager.BottomEstimateThrethold = bottom;
		}
		var estimated = hasCachedEstimated
			? cachedEstimated
			: await WorldUtil
				.EstimateF0Async(x, len, wp,
					bottomPitch:bottom,
					doParallel:SettingManager.DoParallelEstimate)
				.ConfigureAwait(false);
		if (!hasCachedEstimated)
		{
			EstimatedCache.TryAdd(wavHash, estimated!);
		}
		SWEstimate(sw);
		songData.PitchList = SplitF0ByTiming(estimated!, songData.TimingList!);
	}

	[Conditional("DEBUG")]
	private static void SWEstimate(Stopwatch sw)
	{
		sw.Stop();
		Debug.WriteLine($" ★★ Estimate {sw.ElapsedMilliseconds}");
		sw.Reset();
	}

	[Conditional("DEBUG")]
	private static void SWReadWav(Stopwatch sw)
	{
		sw.Stop();
		Debug.WriteLine($" ★★ ReadWav {sw.ElapsedMilliseconds}");
		sw.Reset();
		sw.Start();
	}

	[Conditional("DEBUG")]
	private static void SWConvert(Stopwatch sw)
	{
		sw.Stop();
		Debug.WriteLine($" ★★ Convert {sw.ElapsedMilliseconds}");
		sw.Reset();
		sw.Start();
	}

	[Conditional("DEBUG")]
	private static void SWFactory(Stopwatch sw)
	{
		sw.Stop();
		Debug.WriteLine($" ★★ Factory {sw.ElapsedMilliseconds}");
		sw.Reset();
		sw.Start();
	}

	private static Stopwatch SWStart(string? wavPath)
	{
		var sw = new Stopwatch();
		Debug.WriteLine($" ★★Start: {Path.GetFileName(wavPath)}");
		sw.Start();
		return sw;
	}

	private static async ValueTask
	LoadAndSetTimingListAsync(
		string? labPath,
		bool useLab,
		bool useWav,
		SongData songData
	)
	{
		if ((useLab || useWav) && !string.IsNullOrEmpty(labPath))
		{
			songData.Label = await SasaraLabel
				.LoadAsync(labPath!)
				.ConfigureAwait(false);
			songData.TimingList
				= SplitLabByPhrase(songData.Label);
		}
	}

	private static ImmutableList<List<decimal>>
	SplitF0ByTiming(
		WorldParam estimated,
		IEnumerable<List<LabLine>> timing
	)
	{
		ReadOnlySpan<double> f0 = estimated.F0;
		var fp = estimated.FramePeriod * 10000;

		var timingByPhrase = timing
			.AsEnumerable()
			.Select(phrase =>
			{
				var start = phrase[0];
				var end = phrase[^1];
				var sidx = (int)Math.Round(start.From / fp, MidpointRounding.ToNegativeInfinity);
				var eidx = (int)Math.Round(end.To / fp, MidpointRounding.ToPositiveInfinity);
				return (Start:sidx, End:eidx);
			});

		var ret = ImmutableList.CreateBuilder<List<decimal>>();
		foreach (var (Start, End) in timingByPhrase)
		{
			if(f0.Length - 1 < End){ continue; }
			var phraseF0 = f0[Start..End];
			var list = phraseF0
				.ToArray()
				.Select(f => (decimal)f)
				.ToList()
				;
			ret.Add(list);
		}
		return [.. ret];
	}

	/// <summary>
	/// ノートをフレーズ単位で分割
	/// </summary>
	/// <param name="song"></param>
	/// <param name="threthold">同じフレーズとみなすしきい値（tick; 960=1/4）</param>
	/// <returns></returns>
	private static ReadOnlyCollection<List<Note>> SplitByPhrase(
		SongUnit song,
		int threthold = 0
	)
	{
		//返すよう
		List<List<Note>> list = [];

		//念の為ソート
		var notes = song
			.Notes
			.AsParallel()
			.OrderBy(n => n.Clock);

		var phrase = Enumerable.Empty<Note>().ToList();
		foreach (var note in notes)
		{
			if (phrase.Count > 0)
			{
				var last = phrase[^1];
				var isOver = IsOverThrethold(threthold, note, last);
				if (
					//しきい値以下は同じフレーズとみなす
					isOver
					||
					//またはブレス指定があればしきい値以下でも別フレーズ
					last.Breath
				)
				{
					list.Add(phrase);
					phrase = Enumerable
						.Empty<Note>()
						.ToList();
				}
			}
			phrase.Add(note);
		}
		list.Add(phrase);

		return new ReadOnlyCollection<List<Note>>(list);

		static bool IsOverThrethold(int threthold, Note note, Note last)
		{
			return Math.Abs(note.Clock - (last.Clock + last.Duration)) > threthold;
		}
	}

	/// <summary>
	/// labをフレーズ単位で分割。<see cref="Lab.SplitToSentence(double)"/>とは別基準で分割します。
	/// </summary>
	/// <returns></returns>
	private static ReadOnlyCollection<List<LabLine>>
	SplitLabByPhrase(
		Lab lab,
		int threthold = 0
	)
	{
		List<List<LabLine>> list = [];

		//pau,silを除外
		IEnumerable<LabLine> lines = lab
			.Lines?
			.Where(ln => !PhonemeUtil.IsNoSounds(ln))
			?? [];

		var phrase = Enumerable.Empty<LabLine>().ToList();

		foreach (var line in lines)
		{
			if (phrase.Count > 0)
			{
				var last = phrase[^1];
				var isOver = IsOverThrethold(threthold, line, last);
				//閾値以下
				//ブレス指定はpauを生むのでここでは判定不要
				if (isOver)
				{
					list.Add(phrase);
					phrase = Enumerable
						.Empty<LabLine>()
						.ToList();
				}
			}
			phrase.Add(line);
		}
		list.Add(phrase);

		return new ReadOnlyCollection<List<LabLine>>(list);

		static bool IsOverThrethold(
			int threthold,
			LabLine line,
			LabLine last
		)
		{
			return Math.Abs(line.From - last.To) > threthold;
		}
	}
}
