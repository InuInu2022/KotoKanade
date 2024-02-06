using System.Diagnostics;

using static KotoKanade.Core.Models.MediaConverter;

namespace Core;

public class MediaConverterTests
{
	[Theory]
	[InlineData("../../../file/test.mp3")]
	public async Task ConvertAsync(string path)
	{
		var converter = await MediaConverter
			.FactoryAsync();

		converter.Should().NotBeNull();

		var prg = new Progress<ConvertProgressInfo>();

		using var result = await converter
			.ConvertAsync(
				Path.GetFullPath(path),
				prg
			);
		Debug.WriteLine(result);
		result
			.Should().NotBeNull();
		File.Exists(result.Path)
			.Should().BeTrue();
	}
}
