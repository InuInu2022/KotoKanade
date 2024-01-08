using System.Diagnostics;
using System.Reflection;
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
				await Task.Run(() => Process.Start("explorer.exe", path))
				.ConfigureAwait(false);
			});
	}

}
