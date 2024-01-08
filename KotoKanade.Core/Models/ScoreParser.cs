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
	public static async ValueTask<SongData> ProcessCcsAsync(string path)
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

		//中間データに解析・変換
		return new SongData()
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
}
