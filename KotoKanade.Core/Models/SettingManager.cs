using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Preferences;

namespace KotoKanade.Core.Models;

public static class SettingManager
{
	private static readonly Preferences _pref = new();
    public static event EventHandler<PropertyChangedEventArgs>? PropertyChanged;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static T? Get<T>(string name, T defaultValue)
		=> _pref.Get(name, defaultValue);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static void Set<T>(string name, T value)
	{
		_pref.Set(name, value);
		PropertyChanged?.Invoke(null, new PropertyChangedEventArgs(name));
	}

	public static async ValueTask ResetAllAsync(
		CancellationToken ctx = default
	)
	{
		await _pref
			.ClearAsync(ctx)
			.ConfigureAwait(false);
	}

	public static Task<bool> ResetAsync(string name)
		=> _pref
			.RemoveAsync(name)
			;//.ConfigureAwait(false);

	public static int SelectedTab
	{
		get => Get(nameof(SelectedTab), 0);
		set => Set(nameof(SelectedTab), value);
	}

	public static double GlobalSpeed
	{
		get => Get(nameof(GlobalSpeed), 1.0);
		set => Set(nameof(GlobalSpeed), value);
	}

	public static double GlobalVolume
	{
		get => Get(nameof(GlobalVolume), 0.0);
		set => Set(nameof(GlobalVolume), value);
	}

	public static double GlobalPitch
	{
		get => Get(nameof(GlobalPitch), 0.0);
		set => Set(nameof(GlobalPitch), value);
	}

	public static double GlobalAlpha
	{
		get => Get(nameof(GlobalAlpha), 0.0);
		set => Set(nameof(GlobalAlpha), value);
	}

	public static double GlobalIntonation
	{
		get => Get(nameof(GlobalIntonation), 1.0);
		set => Set(nameof(GlobalIntonation), value);
	}

	public static bool DoSplitNotes
	{
		get => Get(nameof(DoSplitNotes), true);
		set => Set(nameof(DoSplitNotes), value);
	}

	public static double ThretholdSplitNote
	{
		get => Get(nameof(ThretholdSplitNote), DefaultThretholdSplitNote);
		set => Set(nameof(ThretholdSplitNote), value);
	}
	public const int DefaultThretholdSplitNote = 250;

	public static decimal ConsonantOffset {
		get => Get(nameof(ConsonantOffset), DefaultConsonantOffset);
		set => Set(nameof(ConsonantOffset), value);
	}
	public const decimal DefaultConsonantOffset = -0.05m;

	#region ModeC
	public static bool DoParallelEstimate {
		get => Get(nameof(DoParallelEstimate), true);
		set => Set(nameof(DoParallelEstimate), value);
	}

	public static double BottomEstimateThrethold {
		get => Get(nameof(BottomEstimateThrethold), DefaultBottomEstimateThrethold);
		set => Set(nameof(BottomEstimateThrethold), value);
	}

	public const double DefaultBottomEstimateThrethold = 50.0;

	public static bool DoAutoTuneThreshold {
		get => Get(nameof(DoAutoTuneThreshold), false);
		set => Set(nameof(DoAutoTuneThreshold), value);
	}

	public static bool IsForceUseDownloadedFFMpeg
	{
		get => Get(nameof(IsForceUseDownloadedFFMpeg), false);
		set => Set(nameof(IsForceUseDownloadedFFMpeg), value);
	}
	#endregion

}
