// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using Epoxy;

namespace KotoKanade.ViewModels;

[ViewModel]
public record GlobalParam
{
	public string? Name { get; set; }

	public double Max { get; set; }
	public double Min { get; set; }
	public double Value { get; set; }
	public double Tick { get; set; } = 0.01;

	public GlobalParam(
		string? name, double max, double min, double value, double tick = 0)
	{
		Name = name;
		Max = max;
		Min = min;
		Value = value;
		Tick = tick;
	}
}
