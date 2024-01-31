using System;
using System.Threading.Tasks;
using DotnetWorld.API;
using DotnetWorld.API.Structs;

namespace SasaraUtil.Models;

public static class WorldUtil
{
	public static async ValueTask<WorldParam> EstimateF0Async(
		double[] x,
		int audioLength,
		WorldParam wParam
	)
	{
		var opt = new HarvestOption();
		DotnetWorld.API.Core.InitializeHarvestOption(opt);
		opt.frame_period = wParam.FramePeriod;
		opt.f0_floor = 90.0;    //声の周波数の下のライン

		wParam.F0Length = await Task.Run(() =>
			DotnetWorld.API.Core.GetSamplesForDIO(
				wParam.Fs,
				audioLength,
				wParam.FramePeriod
			))
			.ConfigureAwait(false);
		wParam.F0 = new double[wParam.F0Length];
		wParam.TimeAxis = new double[wParam.F0Length];

		await Task.Run(() =>
		{
			try
			{
				DotnetWorld.API.Core.Harvest(
					x,
					audioLength,
					wParam.Fs,
					opt,
					wParam.TimeAxis,
					wParam.F0
				);
			}
			catch (System.Exception e)
			{
				throw new InvalidOperationException($"Estimate f0 Error: {e.Message}");
			}
		})
		.ConfigureAwait(false);

		return wParam;
	}

	/// <summary>
	/// Read wav file
	/// </summary>
	public static async ValueTask<(int fs, int nbit, int len, double[] x)> ReadWavAsync(string filename)
	{
		var isExist = System.IO.File.Exists(filename);
		if (!isExist) {
			const string msg = "A internal wav file is not found.";
			throw new ArgumentException(msg, nameof(filename));
		}

		var audioLength = Tools.GetAudioLength(filename);
		if(audioLength<0){
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
	public int Fs { get; set; }

	public double[]? F0 { get; set; }
	public double[]? TimeAxis { get; set; }
	public int F0Length { get; set; }

	public double[,]? Spectrogram { get; set; }
	public double[,]? Aperiodicity { get; set; }
	public int FFTSize { get; set; }

	public WorldParam(int fs, double framePeriod = 5.0)
	{
		Fs = fs;
		FramePeriod = framePeriod;
	}
}