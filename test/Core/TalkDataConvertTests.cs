using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using KotoKanade.Core.Models;
using LibSasara.Model;

namespace Core;

public class TalkDataConvertTests
{
	[ModuleInitializer]
	[SuppressMessage("", "xUnit1013")]
	[SuppressMessage("", "S3168")]
	[SuppressMessage("", "VSTHRD100")]
	public static async void Initialize()
	{
		var method = typeof(TalkDataConverter).GetMethod("InitOpenJTalkAsync", BindingFlags.NonPublic | BindingFlags.Static);
		await (dynamic?)method!
			.Invoke("InitOpenJTalkAsync", null)
			;
	}

	[Theory]
	[SuppressMessage("", "CA1861")]
	[InlineData(new string[] { "あーっ" }, new string[] { "あーっ" })]
	[InlineData(new string[] { "ち", "きゅ", "ー", "は" }, new string[] { "ち", "きゅ", "ウ", "は" })]
	[InlineData(new string[] { "ち", "きゅ", "ーは" }, new string[] { "ち", "きゅ", "ウは" })]
	[InlineData(new string[] { "あっ", "ーっ" }, new string[] { "あっ", "アっ" })]
	[InlineData(new string[] { "ーっ" }, new string[] { "アーっ" })]
	public void Test1(string[] lyricsArray, string[] expectLyrics)
	{
		List<Note> target = lyricsArray
			.Select(n => new Note() { Lyric = n })
			.ToList();
		var result = RefManageLongVowelSymbols(target);

		Assert.True(result?.Select(n => n.Lyric).SequenceEqual(expectLyrics, StringComparer.Ordinal));

		static List<Note>? RefManageLongVowelSymbols(List<Note> p)
		{
			var method = typeof(TalkDataConverter).GetMethod("ManageLongVowelSymbols", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new DataException();
			try
			{
				var ret = method.Invoke("ManageLongVowelSymbols", [p]) as List<Note>;
				return ret ?? default;
			}
			catch (Exception ex)
			{
				throw ex.InnerException ?? new InvalidDataException(ex.Message);
			}
		}
	}

	public static TheoryData<
		List<Note>,
		List<LabLine>,
		(List<Note>, List<LabLine>)
	> SameNoteVowelsData
		=> new(){
			{
				[
					new(){Lyric="かー"},
				],
				[
					new(0,10,"k"),
					new(10,15, "a"),
				],
				(
					[
						new(){Lyric="かー"},
					],
					[
						new(0,10,"k"),
						new(10,20, "a"),
						new(15,20, "a"),
					]
				)
			},
		};

	[Theory]
	[MemberData(nameof(SameNoteVowelsData))]
	[SuppressMessage("","MA0016")]
	public void ManageSameNoteVowels(
		List<Note> notes,
		List<LabLine> lines,
		(List<Note>, List<LabLine>) expect
	)
	{
		var result = CallMethodWithReturn<(List<Note>, List<LabLine>), (List<Note>, List<LabLine>)>(nameof(ManageSameNoteVowels), (notes, lines));
		result.Should().Be(expect);
	}

	private static TRet? CallMethodWithReturn<TRet, TArg>(
		string methodName,
		TArg arg
	)
	{
		var method = typeof(TalkDataConverter).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) ?? throw new DataException();
		try
		{
			var ret = method.Invoke(methodName, [arg]);
			return (TRet?) ret;
		}
		catch (Exception ex)
		{
			throw ex.InnerException ?? new InvalidDataException(ex.Message);
		}
	}
}