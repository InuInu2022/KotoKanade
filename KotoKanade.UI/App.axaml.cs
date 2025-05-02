// <copyright file="App.axaml.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.Converters;

using KotoKanade.Views;

namespace KotoKanade;

public sealed class App : Application
{
	public override void Initialize()
	{
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			//cli here
			var args = desktop.Args;
			desktop.MainWindow = new MainWindow();
		}
		if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = new MainView();
		}

		base.OnFrameworkInitializationCompleted();
	}
}
