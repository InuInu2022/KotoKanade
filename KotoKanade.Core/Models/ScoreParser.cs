using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LibSasara;
using LibSasara.Model;
using SasaraUtil.Models;

namespace KotoKanade.Core.Models;

public static class ScoreParser
{
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
	public static async ValueTask<SongData> ProcessCcsAsync(
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
			await Console.Error.WriteLineAsync($"Error!: ソングデータがありません: {path}")
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

			//TODO:tmgやf0を渡す
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

	private static async ValueTask ProcessPitchAndTimingFromWavAsync(
		string? wavPath,
		bool useWav,
		SongData songData
	)
	{
		if (useWav && !string.IsNullOrEmpty(wavPath) && File.Exists(wavPath) && songData.TimingList?.Any() == true)
		{
			var mc = await MediaConverter
				.FactoryAsync()
				.ConfigureAwait(false);
			var wav = await mc
				.ConvertAsync(wavPath)
				.ConfigureAwait(false);
			//estimate by world
			var (fs, _, len, x) = await WorldUtil
				.ReadWavAsync(wav.Path)
				.ConfigureAwait(false);
			var wp = new WorldParam(fs);
			var estimated = await WorldUtil
				.EstimateF0Async(x, len, wp)
				.ConfigureAwait(false);
			songData.PitchList = SplitF0ByTiming(estimated, songData.TimingList);
		}
	}

	private static async ValueTask LoadAndSetTimingListAsync(
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
