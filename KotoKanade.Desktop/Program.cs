// <copyright file="Program.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System.Globalization;
using Avalonia;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace KotoKanade;

public static class Program
{
	private static readonly NLog.Logger Logger
		= NLog.LogManager.GetCurrentClassLogger();

	// Avalonia configuration, don't remove; also used by visual designer.
	public static AppBuilder BuildAvaloniaApp()
	{
		return AppBuilder.Configure<App>().
			UsePlatformDetect().
			LogToTrace();
	}

	// Initialization code. Don't use any Avalonia, third-party APIs or any
	// SynchronizationContext-reliant code before AppMain is called: things aren't initialized
	// yet and stuff might break.
	[STAThread]
	public static int Main(string[] args)
	{
		try
		{
			InitLogger();
			Logger.Info("App starting...");
			var os = Environment.OSVersion;
			Logger.Info($"""
				-----
				Platform: {os.Platform}
				OS Version: {os.VersionString}
				CPU counts: {Environment.ProcessorCount.ToString(CultureInfo.InvariantCulture)}
				-----
				""");
		  	return BuildAvaloniaApp().
				StartWithClassicDesktopLifetime(args);
		}
		catch (Exception ex)
		{
			Logger.Fatal(ex, $"Main App Error!\n{ex.Message}");
			Logger.Error(ex.InnerException);
			Logger.Error(ex.StackTrace);
			Logger.Error(ex.HResult);
			return 1;
		}
		finally
		{
			Logger.Info("App finising...");
		}
	}

	private static void InitLogger()
	{
		var config = new LoggingConfiguration();

		var fileTarget = new FileTarget();
		config.AddTarget("file", fileTarget);

		fileTarget.Name = "f";
		fileTarget.FileName = "${basedir}/logs/${shortdate}.log";
		fileTarget.Layout = "${longdate} [${uppercase:${level}}] ${message}";

		var rule1 = new LoggingRule("*", LogLevel.Info, fileTarget);
		config.LoggingRules.Add(rule1);

		LogManager.Configuration = config;
	}
}
