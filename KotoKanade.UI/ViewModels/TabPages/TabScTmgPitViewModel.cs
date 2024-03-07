using System.Diagnostics.CodeAnalysis;
using Epoxy;
using KotoKanade.Core.Models;

namespace KotoKanade.ViewModels;

[ViewModel]
[SuppressMessage("", "S1118")]
[SuppressMessage("", "RCS1102")]
public class TabScTmgPitViewModel
{
	public static bool DoParallelEstimate
	{
		get => SettingManager.DoParallelEstimate;
		set => SettingManager.DoParallelEstimate = value;
	}

	public static double BottomEstimateThrethold
	{
		get => SettingManager.BottomEstimateThrethold;
		set => SettingManager.BottomEstimateThrethold = value;
	}
}
