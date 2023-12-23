// <copyright file="App.axaml.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using HotAvalonia;

using KotoKanade.Views;

namespace KotoKanade;

public sealed class App : Application
{
	public override void Initialize()
	{
		this.EnableHotReload(); // Ensure this line **precedes** `AvaloniaXamlLoader.Load(this);`
		AvaloniaXamlLoader.Load(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
		{
			desktop.MainWindow = new MainWindow();
		}
		if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewPlatform)
		{
			singleViewPlatform.MainView = new MainView();
		}

		base.OnFrameworkInitializationCompleted();
	}
}
