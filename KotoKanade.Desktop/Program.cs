// <copyright file="Program.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System;
using Avalonia;

namespace KotoKanade;

public static class Program
{
    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().
            UsePlatformDetect().
            LogToTrace();

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().
        StartWithClassicDesktopLifetime(args);
}
