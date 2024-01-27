using System.Reflection;

namespace KotoKanade.Core.Util;

public static class AppUtil
{
	public static string GetAppVer(){
		return Assembly
			.GetEntryAssembly()?
			.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute))
			.Cast<AssemblyInformationalVersionAttribute>()
			.FirstOrDefault()?
			.InformationalVersion ?? string.Empty;
	}

	public static string GetAppName(){
		return Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
	}

	public const bool IsDebug
#if DEBUG
		= true;
#else
		= false;
#endif
}
