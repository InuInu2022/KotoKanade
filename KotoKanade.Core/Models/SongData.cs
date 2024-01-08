using System.Collections.Generic;
using LibSasara.Model;

namespace KotoKanade.Core.Models;

/// <summary>
/// トーク変換用中間ソングデータ
/// </summary>
public sealed record SongData{
	public string? SongTrackName { get; set; }

	//フレーズ単位で分割されたNoteのリスト
	//フレーズ単位でセリフ化する
	public IEnumerable<List<Note>>? PhraseList { get; set; }

	//ピッチ調声データ(LogF0)のリスト
	public IEnumerable<List<decimal>>? PitchList { get; set; }

	//タイミング調声データのリスト
	//ccsに記録されたものであれば細かい中間タイミングデータが取れる
	//なにもない時はでっち上げ
	//labファイルがある時は子音と母音だけなので中間タイミングデータをでっち上げ
	//TODO:public IEnumerable<List<>>? TimingList { get; set; }

	public Lab? Label { get; set; }

	public SortedDictionary<int, decimal>? TempoList { get; set; }
	public SortedDictionary<int, (int Beats, int BeatType)>? BeatList { get; set; }

	//TODO:将来的に対応するパラメータ
	//g Alpha
	//g PitchShift => Pitch?
	//g PitchTune => Into.
	//g Husky
	//Volume => VOL
	//Alpha => ALP
}
