using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Epoxy;
using KotoKanade.Core.Util;

namespace KotoKanade.ViewModels;

[ViewModel]
public class TabSettingsViewModel
{
	public Command? OpenLicense { get; set; }
	public string? AppName { get; set; }
	public string? AppVer { get; set; }

	public TabSettingsViewModel()
	{
		AppName = AppUtil.GetAppName();

		AppVer = $"ver. {AppUtil.GetAppVer()}";

		OpenLicense = Command.Factory.
			Create(async ()=>{
				var path = Path.Combine(
					AppDomain.CurrentDomain.BaseDirectory,
					@"licenses\"
				);
				var command = string.Empty;
				if(System.OperatingSystem.IsWindows()){
					command = "explorer.exe";
				}else if(System.OperatingSystem.IsMacOS()){
					command = "open";
				}else if(System.OperatingSystem.IsLinux()){
					command = "xdg-open";
				}else{
					throw new NotSupportedException("非対応プラットフォームです");
				}
				await Task.Run(() => Process.Start(command, path))
				.ConfigureAwait(false);
			});
	}

}
