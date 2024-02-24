using System.Diagnostics;
using Xunit.Abstractions;

namespace Core;

public class SplitPointCulclaterTests(
	ITestOutputHelper output
){
	private readonly ITestOutputHelper _output = output;

	[Theory]
	[InlineData(0.025, 5)]
	[InlineData(0.05, 10)]
	[InlineData(0.1, 20)]
	[InlineData(0.075, 15)]
	[InlineData(0.5, (int)(0.5/0.005))]
	[InlineData(0.614, (int)(0.614/0.005))]
	public void EstRatiosInRng(
		double interval,
		int count
	)
	{
		// Act
		var result = SplitPointCulclater
			.EstimateRatios(interval);

		var msg = $"[{result.Interval}]\n{string.Join(",\n", result.Ratios.Select(v => $"	{v}"))}";
		_output.WriteLine(msg);
		Console.WriteLine(msg);
		Debug.WriteLine(msg);

		// Assert
		result.Ratios.Count
			.Should().Be(count);

		result.Ratios[^1]
			.Should().BeInRange(0, 1,
			$"{nameof(result.Ratios)}",
			result.Ratios[^1]);

		result.Ratios
			.Zip(
				result.Ratios.Skip(1),
				(prev, next) => (prev, next))
			.All(pair => pair.next > pair.prev)
			.Should().BeTrue();
	}
}
