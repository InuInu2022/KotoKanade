using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using LibSasara;
using LibSasara.Model;

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
		string? labPath = null
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
		if(trackset is null || song is null){
			await Console.Error.WriteLineAsync($"Error!: ソングデータがありません: { path }")
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

		if(!string.IsNullOrEmpty(labPath)){
			songData.Label = await SasaraLabel
				.LoadAsync(labPath!)
				.ConfigureAwait(false);
			songData.TimingList
				= SplitLabByPhrase(songData.Label);
		}

		//中間データに解析・変換
		return songData;
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
			if(phrase.Count > 0)
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
			.Where(ln => PhonemeUtil.IsNoSounds(ln))
			?? [];

		var phrase = Enumerable.Empty<LabLine>().ToList();

		foreach(var line in lines)
		{
			if(phrase.Count > 0)
			{
				var last = phrase[^1];
				var isOver = IsOverThrethold(threthold, line, last);
				//閾値以下
				//ブレス指定はpauを生むのでここでは判定不要
				if(isOver)
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
