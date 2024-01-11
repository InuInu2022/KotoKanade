// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using Epoxy;

namespace KotoKanade.ViewModels;

[ViewModel]
public record StyleRate
{
	public string Name { get; private set; }
	public double Rate { get; set; }

	public StyleRate(string name, double rate)
	{
		Name = name;
		Rate = rate;
	}
}