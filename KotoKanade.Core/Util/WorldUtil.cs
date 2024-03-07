using System.Collections.Immutable;
using System.Diagnostics;
using DotnetWorld.API;
using DotnetWorld.API.Structs;
using WavData = (int SampleRate, int nbit, int len, double[] x);

namespace KotoKanade.Core.Models;

public static class WorldUtil
{
	public static async ValueTask<WorldParam> EstimateF0Async(
		double[] x,
		int audioLength,
		WorldParam wParam,
		int nbit = 16,
		double bottomPitch = 50.0,
		bool doParallel = true
	)
	{
		var sw = new Stopwatch();
		sw.Start();

		var opt = new HarvestOption();
		DotnetWorld.API.Core.InitializeHarvestOption(opt);
		opt.frame_period = wParam.FramePeriod;
		opt.f0_floor = bottomPitch; //声の周波数の下のライン

		sw.Stop();
		Console.WriteLine($"[time]Estimate init {sw.Elapsed.TotalSeconds}");
		sw.Restart();

		wParam.F0Length = await Task.Run(() =>
				DotnetWorld.API.Core.GetSamplesForDIO(
					wParam.SampleRate,
					audioLength,
					wParam.FramePeriod
				)
			)
			.ConfigureAwait(false);
		wParam.F0 = new double[wParam.F0Length];
		wParam.TimeAxis = new double[wParam.F0Length];

		sw.Stop();
		Console.WriteLine($"[time]GetSamplesForDIO {sw.Elapsed.TotalSeconds}");
		sw.Restart();

		var seps = doParallel
			? SeparateWavData((wParam.SampleRate, nbit, audioLength, x))
			: [(wParam.SampleRate, nbit, audioLength, x)]
			;
		var wParams = seps
			.AsParallel().AsOrdered()
			.Select(v => EstimateCore(v, opt))
			.ToList();
		var timeAxis = wParams
			.Select(v => v.TimeAxis)
			.SelectMany(arr => arr ?? []).ToArray();
		var f0 = wParams
			.Select(v => v.F0)
			.SelectMany(arr => arr ?? []).ToArray();
		wParam.TimeAxis = timeAxis;
		wParam.F0 = f0;

		sw.Stop();
		Console.WriteLine($"[time]Harvest {sw.Elapsed.TotalSeconds}");
		sw.Restart();

		return wParam;
	}

	private static WorldParam EstimateCore(
		(int SampleRate, int nbit, int len, double[] x) wavData,
		HarvestOption opt
	){
		var sepParam = new WorldParam(wavData.SampleRate);
		sepParam.F0Length = DotnetWorld.API.Core.GetSamplesForDIO(
			wavData.SampleRate,
			wavData.len,
			sepParam.FramePeriod
		);
		sepParam.F0 = new double[sepParam.F0Length];
		sepParam.TimeAxis = new double[sepParam.F0Length];
		try
		{
			DotnetWorld.API.Core
				.Harvest(wavData.x, wavData.len, wavData.SampleRate, opt, sepParam.TimeAxis, sepParam.F0);
		}
		catch (System.Exception e)
		{
			throw new InvalidOperationException($"Estimate f0 Error: {e.Message}");
		}
		return sepParam;
	}

	private static ImmutableArray<WavData> SeparateWavData(WavData wavData)
	{
		//fix param
		int retNBit = wavData.nbit;
		int retSampleRate = wavData.SampleRate;

		int separateCount = Environment.ProcessorCount;
		int sepLen = wavData.len / separateCount;
		//int modLen = wavData.len % separateCount;

		ReadOnlySpan<double> span = wavData.x;
		var builder = ImmutableArray.CreateBuilder<WavData>(separateCount + 1);
		var from = 0;
		for (var i = 0; i < separateCount; i++)
		{
			var to = from + sepLen;
			Debug.Assert(from <= wavData.len);
			Debug.Assert(to <= span.Length);
			var sliced = span[from..to].ToArray();
			builder.Add((retSampleRate, retNBit, sliced.Length, sliced));
			from = to;
		}
		var last = span[from..].ToArray();
		if (last.Length > 0)
		{
			builder.Add((retSampleRate, retNBit, last.Length, last));
		}
		return builder.ToImmutable();
	}

	/// <summary>
	/// Read wav file
	/// </summary>
	public static async ValueTask<WavData> ReadWavAsync(string filename)
	{
		var isExist = System.IO.File.Exists(filename);
		if (!isExist)
		{
			const string msg = "A internal wav file is not found.";
			throw new ArgumentException(msg, nameof(filename));
		}

		var audioLength = Tools.GetAudioLength(filename);
		if (audioLength < 0)
		{
			//TODO: xplat chunk reader
			throw new InvalidDataException($"{filename} has invalid data!");
		}

		var x = new double[audioLength];

		return await Task.Run(() =>
			{
				Tools.WavRead(filename, out int fs, out int nbit, x);
				return (fs, nbit, audioLength, x);
			})
			.ConfigureAwait(false);
	}
}

public record WorldParam
{
	public double FramePeriod { get; set; }
	public int SampleRate { get; set; }

	public double[]? F0 { get; set; }
	public double[]? TimeAxis { get; set; }
	public int F0Length { get; set; }

	public double[,]? Spectrogram { get; set; }
	public double[,]? Aperiodicity { get; set; }
	public int FFTSize { get; set; }

	public WorldParam(int sampleRate, double framePeriod = 5.0)
	{
		SampleRate = sampleRate;
		FramePeriod = framePeriod;
	}
}
