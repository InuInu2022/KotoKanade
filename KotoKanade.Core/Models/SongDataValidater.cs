using System;

namespace KotoKanade.Core.Models;

public static class SongDataValidater
{
	public static ValidatedResult
	Validate(SongData data, bool isUseLab, bool isUseWav)
	{
		// フレーズデータが空でないか
		var checkResult = CheckBadPhrase(data);
		if (checkResult != null) return checkResult;

		var hasTiming = data.TimingList?.Any() == true;
		var hasPitch = data.PitchList?.Any() == true;

		//タイミングデータが空でないか
		checkResult = CheckLabFile(isUseLab, hasTiming);
		if (checkResult != null) return checkResult;

		//音声データが空でないか
		checkResult = CheckWavFile(isUseWav, hasPitch, hasTiming);
		if (checkResult != null) return checkResult;

		var phraseCount = data.PhraseList?.Count() ?? 0;
		var timingCount = data.TimingList?.Count() ?? 0;

		// タイミング歌唱指導するとき、長さが合うか
		checkResult = CheckTimingCount(isUseLab, phraseCount, timingCount);
		if (checkResult != null) return checkResult;

		var pitchCount = data.PitchList?.Count() ?? 0;

		// ピッチ歌唱指導するとき、長さが合うか
		checkResult = CheckTimingAndPitchCount(isUseWav, phraseCount, timingCount, pitchCount);
		if (checkResult != null) return checkResult;


		if (!hasTiming && !hasPitch)
		{
			return new() { IsValid = true, Type = ResultType.AllValid };
		}

		return new()
		{
			IsValid = true,
			Type = ResultType.AllValid,
		};

		/*
		return new()
		{
			IsValid = false,
			Type = ResultType.Unknown,
		};
		*/
	}

	/// <summary>
	/// フレーズデータが空でないか
	/// </summary>
	/// <param name="data"></param>
	/// <returns></returns>
	static ValidatedResult?
	CheckBadPhrase(SongData data)
	{
		var isBadPhrase = data.PhraseList?.Any() == false;
		if (isBadPhrase)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.BadPhrase,
			};
		}
		return null;
	}

	static ValidatedResult?
	CheckLabFile(bool isUseLab, bool hasTiming)
	{
		if (isUseLab && !hasTiming)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.LabNotFound,
			};
		}
		return null;
	}

	static ValidatedResult?
	CheckWavFile(bool isUseWav, bool hasPitch, bool hasTiming)
	{
		if (isUseWav && !hasPitch)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.WavNotFound,
			};
		}

		if (isUseWav && !hasTiming)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.LabNotFound,
			};
		}
		return null;
	}

	static ValidatedResult?
	CheckTimingCount(bool isUseLab, int phraseCount, int timingCount)
	{
		if (isUseLab && phraseCount != timingCount)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.TimingDataCountExcept,
			};
		}
		return null;
	}

	static ValidatedResult?
	CheckTimingAndPitchCount(bool isUseWav, int phraseCount, int timingCount, int pitchCount)
	{
		if (isUseWav && phraseCount != pitchCount)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.PitchDataCountExcept,
			};
		}
		if (isUseWav && phraseCount != timingCount)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.TimingDataCountExcept,
			};
		}
		if (isUseWav && timingCount != phraseCount)
		{
			return new()
			{
				IsValid = false,
				Type = ResultType.TimingAndPitchCountExcept,
			};
		}
		return null;
	}
}

public record ValidatedResult
{
	public required bool IsValid { get; init; }
	public ResultType Type { get; init; }
}

public enum ResultType
{
	AllValid,
	BadPhrase,
	LabNotFound,
	WavNotFound,
	TimingDataCountExcept,
	PitchDataCountExcept,
	TimingAndPitchCountExcept,

	Unknown = 999,
}