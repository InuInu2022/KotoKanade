using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;

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
		(List<Note>, List<LabLine>),	//talk
		(List<Note>, List<LabLine>)		//song
	> SameNoteVowelsData
		=> new(){
			//case 1 かー
			{
				(
					[
						new(){Lyric="かー"},
					],
					[
						new(0,10,"k"),
						new(10,20, "a"),
					]
				),
				(
					[
						new(){Lyric="かー"},
					],
					[
						new(0,10,"k"),
						new(10,15, "a"),
						new(15,20, "a"),
					]
				)
			},
			//case 2 ふうに
			{
				(
					[
						new(){Lyric="ふうに"},
					],
					[
						new(0,10,"f"),
						new(10,15, "u"),
						new(15,20, "n"),
						new(20,25, "i"),
					]
				),
				(
					[
						new(){Lyric="ふうに"},
					],
					[
						new(0,10,"f"),
						new(10,12.5, "u"),
						new(12.5,15, "u"),
						new(15,20, "n"),
						new(20,25, "i"),
					]
				)
			},
		};

	[Theory]
	[MemberData(nameof(SameNoteVowelsData))]
	[SuppressMessage("","MA0016")]
	public void ManageSameNoteVowels(
		(List<Note>, List<LabLine>) actual,
		(List<Note>, List<LabLine>) expect
	)
	{
		var result = CallMethodWithReturn<(List<Note>, List<LabLine>), (List<Note>, List<LabLine>)>(nameof(ManageSameNoteVowels), actual);

		//count
		result.Item1.Count.Should().Be(expect.Item1.Count);
		result.Item2.Count.Should().Be(expect.Item2.Count);
		//lyrics
		result.Item1.Select(n => n.Lyric).ToString()
			.Should().Be(expect.Item1.Select(n => n.Lyric).ToString());
		//phonemes
		result.Item2
			.Should().BeEquivalentTo(expect.Item2);

	}

	public static TheoryData<
		LabLine,
		int,
		IEnumerable<LabLine>
	> DivideLabLineData
	=> new()
	{
		{
			new(0,10,"a"),
			2,
			[
				new(0, 5, "a"),
				new(5, 10, "a"),
			]
		},
		{
			new(0,100,"a"),
			4,
			[
				new(0, 25, "a"),
				new(25, 50, "a"),
				new(50, 75, "a"),
				new(75, 100, "a"),
			]
		},
	};

	[Theory]
	[MemberData(nameof(DivideLabLineData))]
	public void DivideLabLine(
		LabLine actual,
		int n,
		IEnumerable<LabLine> expect
	)
	{
		var result = CallMethodWithReturn<
			IEnumerable<LabLine>,
			object[]
		>(nameof(DivideLabLine), [actual, n]);

		result
			.Should().BeEquivalentTo(expect);

		var tdc = new TalkDataConverter();
		var r2 = DivideLabLineInner(tdc, actual, n);
		r2.Should().BeEquivalentTo(expect);

		[UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = nameof(DivideLabLine))]
    	static extern IEnumerable<LabLine> DivideLabLineInner(
			TalkDataConverter tdc,
			LabLine labLine,
			int intValue);
	}

	private static TRet? CallMethodWithReturn<TRet, TArg>(
		string methodName,
		TArg arg
	)
	{
		var method = typeof(TalkDataConverter).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static) ?? throw new DataException();
		try
		{
			var ret = arg is object[] objArg
				? method.Invoke(methodName, objArg)
				: method.Invoke(methodName, [arg]);
			return (TRet?) ret;
		}
		catch (Exception ex)
		{
			throw ex.InnerException ?? new InvalidDataException(ex.Message);
		}
	}
}