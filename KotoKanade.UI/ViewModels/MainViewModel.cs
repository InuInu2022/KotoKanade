// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using Epoxy;

namespace KotoKanade.ViewModels;

[ViewModel]
public sealed class MainViewModel
{
	public Command Ready { get; }
	public string Title { get; private set; } = string.Empty;

	public MainViewModel()
	{
		// A handler for window loaded
		Ready = Command.Factory.Create(() =>
		{
			Title = "Hello Epoxy!";
			return default;
		});
	}
}
