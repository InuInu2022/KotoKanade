using Avalonia.Preferences;

namespace KotoKanade.Core.Models;

public static class SettingManager
{
	private static readonly Preferences _pref = new();

	public static bool DoParallelEstimate {
		get => _pref.Get(nameof(DoParallelEstimate), true);
		set => _pref.Set(nameof(DoParallelEstimate), value);
	}

	public static double BottomEstimateThrethold {
		get => _pref.Get(nameof(BottomEstimateThrethold), 50.0);
		set => _pref.Set(nameof(BottomEstimateThrethold), value);
	}
}
