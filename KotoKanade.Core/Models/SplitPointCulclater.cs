using System.Collections.Immutable;
using MathNet.Numerics.Interpolation;

namespace KotoKanade.Core.Models;

using IntervalRatio = (double Interval, ImmutableList<double> Ratios);

public static class SplitPointCulclater
{
	// 計測結果
	static readonly ImmutableList<IntervalRatio> measurementResults =
	[
		(0.025, [0.100, 0.300, 0.500, 0.700, 0.900,]),
		(0.05, [0.033, 0.100, 0.167, 0.233, 0.300, 0.367, 0.500, 0.700, 0.850, 0.950,]),
		(
			0.1,
			[
				0.017,
				0.050,
				0.083,
				0.117,
				0.150,
				0.183,
				0.233,
				0.300,
				0.367,
				0.417,
				0.450,
				0.483,
				0.517,
				0.550,
				0.583,
				0.633,
				0.700,
				0.767,
				0.850,
				0.950,
			]
		),
		(
			0.2,
			[
				0.008,
				0.025,
				0.042,
				0.058,
				0.075,
				0.092,
				0.108,
				0.125,
				0.142,
				0.158,
				0.175,
				0.192,
				0.220,
				0.260,
				0.300,
				0.340,
				0.380,
				0.409,
				0.427,
				0.445,
				0.464,
				0.482,
				0.500,
				0.518,
				0.536,
				0.555,
				0.573,
				0.591,
				0.611,
				0.633,
				0.656,
				0.678,
				0.700,
				0.722,
				0.744,
				0.767,
				0.789,
				0.833,
				0.900,
				0.967,
			]
		),
		(
			0.4,
			[
				0.0035,
				0.0105,
				0.018,
				0.0245,
				0.0315,
				0.039,
				0.046,
				0.053,
				0.06,
				0.067,
				0.074,
				0.081,
				0.0885,
				0.095,
				0.102,
				0.1095,
				0.116,
				0.1235,
				0.1305,
				0.1375,
				0.1445,
				0.1515,
				0.159,
				0.1695,
				0.185,
				0.2,
				0.2155,
				0.2305,
				0.2455,
				0.261,
				0.276,
				0.287,
				0.2945,
				0.3015,
				0.308,
				0.3155,
				0.3225,
				0.341,
				0.37,
				0.3995,
				0.429,
				0.449,
				0.4605,
				0.4725,
				0.4835,
				0.495,
				0.507,
				0.518,
				0.5295,
				0.541,
				0.553,
				0.564,
				0.5755,
				0.5875,
				0.599,
				0.61,
				0.6205,
				0.6305,
				0.641,
				0.651,
				0.6615,
				0.6715,
				0.682,
				0.692,
				0.703,
				0.7135,
				0.7235,
				0.734,
				0.744,
				0.7545,
				0.7645,
				0.775,
				0.785,
				0.8,
				0.819,
				0.8455,
				0.88,
				0.9145,
				0.9485,
				0.983,
			]
		),
		(
			0.5,
			[
				0.004,
				0.011,
				0.018,
				0.025,
				0.032,
				0.039,
				0.046,
				0.054,
				0.061,
				0.068,
				0.075,
				0.082,
				0.089,
				0.096,
				0.104,
				0.111,
				0.118,
				0.125,
				0.132,
				0.139,
				0.146,
				0.154,
				0.161,
				0.168,
				0.175,
				0.182,
				0.189,
				0.196,
				0.211,
				0.233,
				0.256,
				0.278,
				0.300,
				0.322,
				0.344,
				0.367,
				0.389,
				0.403,
				0.410,
				0.417,
				0.424,
				0.431,
				0.438,
				0.445,
				0.452,
				0.459,
				0.466,
				0.472,
				0.479,
				0.486,
				0.493,
				0.500,
				0.507,
				0.514,
				0.521,
				0.528,
				0.534,
				0.541,
				0.548,
				0.555,
				0.562,
				0.569,
				0.576,
				0.583,
				0.590,
				0.597,
				0.604,
				0.611,
				0.619,
				0.626,
				0.633,
				0.641,
				0.648,
				0.656,
				0.663,
				0.670,
				0.678,
				0.685,
				0.693,
				0.700,
				0.707,
				0.715,
				0.722,
				0.730,
				0.737,
				0.744,
				0.752,
				0.759,
				0.767,
				0.774,
				0.781,
				0.789,
				0.796,
				0.817,
				0.850,
				0.883,
				0.917,
				0.950,
				0.983,
				0.995,
			]
		),
		(
			0.6,
			[
				0.003,
				0.009,
				0.015,
				0.021,
				0.027,
				0.033,
				0.039,
				0.045,
				0.052,
				0.058,
				0.064,
				0.070,
				0.076,
				0.082,
				0.088,
				0.094,
				0.100,
				0.106,
				0.112,
				0.118,
				0.124,
				0.130,
				0.136,
				0.142,
				0.148,
				0.155,
				0.161,
				0.167,
				0.173,
				0.179,
				0.185,
				0.191,
				0.197,
				0.209,
				0.227,
				0.245,
				0.264,
				0.282,
				0.300,
				0.318,
				0.336,
				0.355,
				0.373,
				0.391,
				0.403,
				0.409,
				0.414,
				0.420,
				0.426,
				0.431,
				0.437,
				0.443,
				0.449,
				0.454,
				0.460,
				0.466,
				0.471,
				0.477,
				0.483,
				0.489,
				0.494,
				0.500,
				0.506,
				0.511,
				0.517,
				0.523,
				0.529,
				0.534,
				0.540,
				0.546,
				0.551,
				0.557,
				0.563,
				0.569,
				0.574,
				0.580,
				0.586,
				0.591,
				0.597,
				0.603,
				0.609,
				0.615,
				0.621,
				0.627,
				0.633,
				0.639,
				0.645,
				0.652,
				0.658,
				0.664,
				0.670,
				0.676,
				0.682,
				0.688,
				0.694,
				0.700,
				0.706,
				0.712,
				0.718,
				0.724,
				0.730,
				0.736,
				0.742,
				0.748,
				0.755,
				0.761,
				0.767,
				0.773,
				0.779,
				0.785,
				0.791,
				0.797,
				0.812,
				0.837,
				0.863,
				0.887,
				0.913,
				0.938,
				0.962,
				0.988,
			]
		),
		(
			1.0,
			[
				0.002,
				0.005,
				0.009,
				0.013,
				0.016,
				0.020,
				0.024,
				0.027,
				0.031,
				0.035,
				0.038,
				0.042,
				0.045,
				0.049,
				0.053,
				0.056,
				0.060,
				0.064,
				0.067,
				0.071,
				0.075,
				0.078,
				0.082,
				0.085,
				0.089,
				0.093,
				0.096,
				0.100,
				0.104,
				0.107,
				0.111,
				0.115,
				0.118,
				0.122,
				0.125,
				0.129,
				0.133,
				0.136,
				0.140,
				0.144,
				0.147,
				0.151,
				0.155,
				0.158,
				0.162,
				0.165,
				0.169,
				0.173,
				0.176,
				0.180,
				0.184,
				0.187,
				0.191,
				0.195,
				0.198,
				0.206,
				0.218,
				0.229,
				0.241,
				0.253,
				0.265,
				0.276,
				0.288,
				0.300,
				0.312,
				0.324,
				0.335,
				0.347,
				0.359,
				0.371,
				0.382,
				0.394,
				0.402,
				0.405,
				0.408,
				0.412,
				0.415,
				0.419,
				0.422,
				0.425,
				0.429,
				0.432,
				0.436,
				0.439,
				0.442,
				0.446,
				0.449,
				0.453,
				0.456,
				0.459,
				0.463,
				0.466,
				0.469,
				0.473,
				0.476,
				0.480,
				0.483,
				0.486,
				0.490,
				0.493,
				0.497,
				0.500,
				0.503,
				0.507,
				0.510,
				0.514,
				0.517,
				0.520,
				0.524,
				0.527,
				0.531,
				0.534,
				0.537,
				0.541,
				0.544,
				0.547,
				0.551,
				0.554,
				0.558,
				0.561,
				0.564,
				0.568,
				0.571,
				0.575,
				0.578,
				0.581,
				0.585,
				0.588,
				0.592,
				0.595,
				0.598,
				0.602,
				0.605,
				0.609,
				0.612,
				0.616,
				0.619,
				0.623,
				0.626,
				0.630,
				0.633,
				0.637,
				0.640,
				0.644,
				0.647,
				0.651,
				0.654,
				0.658,
				0.661,
				0.665,
				0.668,
				0.672,
				0.675,
				0.679,
				0.682,
				0.686,
				0.689,
				0.693,
				0.696,
				0.700,
				0.704,
				0.707,
				0.711,
				0.714,
				0.718,
				0.721,
				0.725,
				0.728,
				0.732,
				0.735,
				0.739,
				0.742,
				0.746,
				0.749,
				0.753,
				0.756,
				0.760,
				0.763,
				0.767,
				0.770,
				0.774,
				0.777,
				0.781,
				0.784,
				0.788,
				0.791,
				0.795,
				0.798,
				0.808,
				0.825,
				0.842,
				0.858,
				0.875,
				0.892,
				0.908,
				0.925,
				0.942,
				0.958,
				0.975,
				0.992,
			]
		),
	];

	// スプライン補完を用いて数列を推定するメソッド
	public static IntervalRatio EstimateRatios(double arbitraryInterval)
	{
		// 補完に使用する測定結果を取得
		var (_, Ratios) = GetClosestMeasurementResult(arbitraryInterval);

		// 補完用のデータポイントを作成
		// 0.0 - 1.0
		var intervals = Enumerable
			.Range(0, Ratios.Count)
			.Select(i => (double)i / Ratios.Count);

		// スプライン補完を実行
		var spline = CubicSpline.InterpolateNatural(intervals, Ratios);

		// 任意の期間に対する数列を補完
		int dataPoints = (int)(arbitraryInterval / 0.005);
		var estimatedValues = Enumerable
			.Range(0, dataPoints)
			.Select(i =>
			{
				var cnt = (double)i / dataPoints;
				return spline.Interpolate(cnt);
			});

		return (arbitraryInterval, [.. estimatedValues]);
	}

	// 最も近い測定結果を取得するメソッド
	static IntervalRatio GetClosestMeasurementResult(double arbitraryInterval)
	{
		// 測定結果と与えられた期間の差分を計算し、最も近いものを返す
		return measurementResults
			.OrderBy(result => Math.Abs(result.Interval - arbitraryInterval))
			.FirstOrDefault();
	}
}
