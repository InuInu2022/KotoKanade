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

	public double BottomEstimateThrethold { get; set; }
		= SettingManager.BottomEstimateThrethold;

	public static bool IsForceUseDownloadedFFMpeg
	{
		get => SettingManager.IsForceUseDownloadedFFMpeg;
		set => SettingManager.IsForceUseDownloadedFFMpeg = value;
	}

	public Command? ResetBottomEstimateThrethold { get; set; }

	public TabScTmgPitViewModel()
	{
		ResetBottomEstimateThrethold = Command.Factory.Create(ResetBottomEstimateThretholdEvent);
	}

	Func<ValueTask> ResetBottomEstimateThretholdEvent => () =>
	{
		BottomEstimateThrethold = SettingManager.DefaultBottomEstimateThrethold;
		return default;
	};

	[PropertyChanged(nameof(BottomEstimateThrethold))]
	[SuppressMessage("","IDE0051")]
	private ValueTask BottomEstimateThretholdChangedAsync(double value)
	{
		if (BottomEstimateThrethold == value) return default;

		SettingManager.BottomEstimateThrethold = value;
		return default;
	}
}
