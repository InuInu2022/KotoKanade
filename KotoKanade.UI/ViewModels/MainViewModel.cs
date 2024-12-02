// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Notification;
using CevioCasts;
using Epoxy;
using FluentAvalonia.UI.Controls;
using KotoKanade.Core.Models;
using KotoKanade.Core.Util;

namespace KotoKanade.ViewModels;

[ViewModel]
public sealed class MainViewModel
{
	private bool IsNotReady { get; set; } = true;
	public Command Ready { get; }
	public Command Close { get; }
	public string Title { get; private set; } = "test";

	public Well FileDropTarget { get; }
		= Well.Factory.Create<Grid>();

	public string DefaultCcs { get; set; } = string.Empty;
	public string? OpenedCcsPath { get; set; }
	public Command SelectCcs { get; }

	public bool IsUseLabFile { get; set; }
	public string DefaultLab { get; set; } = string.Empty;
	public Command SelectLab { get; }
	public string? OpenedLabPath { get; set; }

	public bool IsUseWavFile { get; set; }
	public string DefaultWav { get; set; } = string.Empty;
	public Command SelectWav { get; }

	public string? OpenedWavPath { get; set; }

	public int SelectedTab { get; set; }
		= SettingManager.SelectedTab;

	public FAComboBoxItem? SelectedCastItem { get; set; }
	public int SelectedCastIndex { get; set; }
	public ObservableCollection<Cast>? TalkCasts { get; set; }
	public ObservableCollection<StyleRate>? Styles { get; set; }
	public ObservableCollection<GlobalParam>? GlobalParams { get; set; }

	public int SelectedCastVersionIndex { get; set; }
	public ObservableCollection<string>? TalkCastVersions { get; set; }

	public bool IsSplitNotes { get; set; }
		= SettingManager.DoSplitNotes;

	public double ThretholdSplitNote {get;set;}
		= SettingManager.ThretholdSplitNote;
	public Command? ResetThretholdSplitNote { get; }
	public Command? GotoSettingTab { get; private set; }
	public decimal ConsonantOffsetSec { get; set; }
		= SettingManager.ConsonantOffset;
	public Command? ResetConsonantOffset { get; }
	public double TimeScaleFactor { get; set; } = 0.030;

	public Command ExportFile { get; set; }
	public bool CanExport { get; set; }

	public bool HasCastDataUpdate { get; set; }
	public bool HasAppUpdate { get; set; }
	public bool HasUpdate { get; set; }

	private static readonly NLog.Logger Logger
		= NLog.LogManager.GetCurrentClassLogger();

	public static INotificationMessageManager Manager { get; }
		= new NotificationMessageManager();
	private readonly INotificationMessageManager _notify;

	public MainViewModel()
	{
		SelectCcs = Command.Factory.Create(SelectCcsAsync);
		SelectLab = Command.Factory.Create(SelectLabAsync);
		SelectWav = Command.Factory.Create(SelectWavAsync);

		_notify = Manager;

		// A handler for window loaded
		Ready = Command.Factory.Create(ReadyFunc);

		Close = Command.Factory.Create(CloseEvent);

		ExportFile = Command.Factory.Create(ExportEvent);

		FileDropTarget.Add(DragDrop.DropEvent, DropFileEventAsync);

		//ResetConsonantOffset = Command.Factory.Create(ResetConsonantEvent);

		ResetConsonantOffset = Command.Factory.Create(ResetParameterEvent);
		ResetThretholdSplitNote = Command.Factory.Create(ResetParameterEvent);

		GotoSettingTab = Command.Factory.Create(() => {
			SelectedTab = (int) TabIndex.Setting;
			return default;
		});

		Styles = [
			new("Normal", 12.3),
			new("Fine", 22.3),
			new("Sad", 32.3),
		];

		GlobalParams = [
			new("Speed", 5.0, 0.2, 1, 0.01 ),
			new("Volume", 8.0, -8.0, 0, 0.01 ),
			new("Pitch", 600.0, -600.0, 0, 1 ),
			new("Alpha", 1.0, -1.0, 0, 0.01 ),
			new("Into.", 2.0, 0.0, 1.0, 0.01 ),
			new("Hus.", 20.0, -20.0, 1.0, 0.01 ),
		];
	}

	private async ValueTask DropFileEventAsync(DragEventArgs args)
	{
		var list = args.Data.GetFiles();
		if(list is null){ return; }

		var pathes = await Task
			.Run(() => list.Select(v => v.Path.LocalPath).ToList().AsReadOnly())
			.ConfigureAwait(true);
		if (pathes is not { Count: > 0 }) { return; }

		var ccs = pathes
			.FirstOrDefault(p => p.EndsWith(".ccs", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".ccst", StringComparison.OrdinalIgnoreCase));
		if(ccs is not null)
		{
			OpenedCcsPath = ccs;
			DefaultCcs = Path.GetFileName(OpenedCcsPath);
		}

		if (IsUseWavFile){
			var wav = pathes
				.FirstOrDefault(p => p.EndsWith(".wav", StringComparison.OrdinalIgnoreCase));
			if(wav is not null)
			{
				OpenedWavPath = wav;
				DefaultWav = Path.GetFileName(OpenedWavPath);
			}else
			{
				AutoSearchFile("wav");
			}
		}
		if (IsUseLabFile || IsUseWavFile)
		{
			var lab = pathes
				.FirstOrDefault(p => p.EndsWith(".lab", StringComparison.OrdinalIgnoreCase));
			if(lab is not null)
			{
				OpenedLabPath = lab;
				DefaultLab = Path.GetFileName(OpenedLabPath);
			}else
			{
				AutoSearchFile("lab");
			}
		}

		CanExport = CheckExportable();
	}

	private async ValueTask SelectCcsAsync()
	{
		var songCcs = await StorageUtil.OpenCevioFileAsync(
			title: "ソングデータを含むccsファイルを選んでください",
			allowMultiple: false,
			path: null
		).ConfigureAwait(true);

		var pathes = StorageUtil
			.GetPathesFromOpenedFiles(songCcs);
		if (pathes is not { Count: > 0 }) { return; }

		//pathes.ToList().ForEach(f => Debug.WriteLine($"path: {f}"));
		OpenedCcsPath = pathes[0];
		DefaultCcs = Path.GetFileName(pathes[0]);

		SelectAutoFiles();

		CanExport = CheckExportable();
	}

	private void SelectAutoFiles()
	{
		//auto file select (same name file only)
		if (IsUseWavFile)
		{
			AutoSearchFile("wav");
		}
		if (IsUseLabFile || IsUseWavFile)
		{
			AutoSearchFile("lab");
		}
	}

	private void AutoSearchFile(string extension)
	{
		var search = Path.ChangeExtension(OpenedCcsPath, extension);
		if(search is null){ return; }

		var isExists = File.Exists(search);
		if (isExists)
		{
			if (string.Equals(extension, "wav", StringComparison.Ordinal))
			{
				OpenedWavPath = search;
				DefaultWav = Path.GetFileName(search);
			}
			else if (string.Equals(extension, "lab", StringComparison.Ordinal))
			{
				OpenedLabPath = search;
				DefaultLab = Path.GetFileName(search);
			}
		}
	}

	private async ValueTask SelectLabAsync()
	{
		var selectedLab = await StorageUtil.OpenLabFileAsync(
			title: "歌唱指導のタイミング情報ファイルを選んでください。",
			allowMultiple: false,
			path: null
		).ConfigureAwait(true);

		var pathes = StorageUtil.GetPathesFromOpenedFiles(selectedLab);
		if (pathes is not { Count: > 0 }) { return; }

		pathes.ToList().ForEach(f => Debug.WriteLine($"path: {f}"));
		OpenedLabPath = pathes[0];
		DefaultLab = Path.GetFileName(pathes[0]);
		CanExport = CheckExportable();
	}

	private async ValueTask SelectWavAsync()
	{
		var selectedWav = await StorageUtil.OpenWavFileAsync(
			title: "歌唱指導の音声ファイルを選んでください。",
			allowMultiple: false,
			path: null
		).ConfigureAwait(true);

		var pathes = StorageUtil
			.GetPathesFromOpenedFiles(selectedWav);
		if (pathes is not { Count: > 0 }) { return; }

		pathes
			.ToList()
			.ForEach(f => Debug.WriteLine($"path: {f}"));
		OpenedWavPath = pathes[0];
		DefaultWav = Path.GetFileName(pathes[0]);
		CanExport = CheckExportable();
	}

	private Func<ValueTask> ExportEvent =>
		async () =>
		{
			var loading = _notify
				.Loading("Now exporting...","変換しています。");

			var path = OpenedCcsPath ?? "";
			var labPath = OpenedLabPath ?? "";
			var wavPath = OpenedWavPath ?? "";
			CanExport = false;

			var saved = await StorageUtil.SaveAsync(
				path: OpenedCcsPath ?? "",
				patterns: ["*.tsrprj"],
				changeExt: ".tstprj",
				targetFileTypes: "VoiSonaTalkプロジェクトファイル"
			).ConfigureAwait(true);

			var saveDir = saved?.Path.LocalPath;
			if (saved is null || saveDir is null)
			{
				_notify.Dismiss(loading!);
				_notify.Error(
					"Save directory is not found!",
					"保存先ディレクトリが見つかりません！");
				CanExport = true;
				return;
			}

			var sw = new Stopwatch();
			sw.Start();

			var loadedSong = await ScoreParser
				.ProcessCcsAsync(
					path,
					labPath,
					wavPath,
					IsUseLabFile,
					IsUseWavFile
				)
				.ConfigureAwait(true);

			var validated = SongDataValidater
				.Validate(loadedSong, IsUseLabFile, IsUseWavFile);
			if(!validated.IsValid)
			{
				DisplayInvalidAndReturn(
					loading, sw, validated,
					path, labPath, wavPath
				);
				return;
			}

			var isSplit = IsSplitNotes;

			try
			{
				await TalkDataConverter
					.GenerateFileAsync(
						loadedSong,
						saveDir,
						TalkCasts?[SelectedCastIndex].Names[0].Display,
						(isSplit, ThretholdSplitNote),
						Styles?.Select(s => s.Rate).ToArray(),
						globalParams: new()
						{
							SpeedRatio = GetParam("Speed"),
							C0Shift = GetParam("Volume"),
							LogF0Shift = GetParam("Pitch"),
							AlphaShift = GetParam("Alpha"),
							LogF0Scale = GetParam("Into."),
							HuskyShift = GetParam("Hus."),
						},
						-ConsonantOffsetSec, //表示と逆
						TalkCastVersions?[SelectedCastVersionIndex] ?? "",
						timeScaleFactor: TimeScaleFactor
					)
					.ConfigureAwait(true);
			}
			catch (System.Exception e)
			{
				_notify.Dismiss(loading!);
				_notify.Error(
					"Export failure! ファイル保存に失敗しました。",
					$"経過時間: {sw.Elapsed.TotalSeconds:F3} sec. \n message: {e.Message}"
				);
				Logger.Fatal(e);
				return;
			}
			finally{
				CanExport = true;
				sw.Stop();
			}

			_notify.Dismiss(loading!);
			_notify.Info(
				"Export success! ファイル保存に成功しました。",
				$"経過時間: {sw.Elapsed.TotalSeconds:F3} sec."
			);

			decimal GetParam(string name)
			{
				return (decimal?)GlobalParams?
					.FirstOrDefault(p => string.Equals(
						p.Name,
						name,
						StringComparison.Ordinal))?
					.Value ?? 0m;
			}
		};

	private void DisplayInvalidAndReturn(
		INotificationMessage loading,
		Stopwatch sw,
		ValidatedResult validated,
		string path,
		string labPath,
		string wavPath
	)
	{
		_notify.Dismiss(loading!);
		var reason = validated.Type switch
		{
			ResultType.BadPhrase => $"'{Path.GetFileName(path)}'のノーツに問題があります（ノーツが空、等）。",
			ResultType.LabNotFound => $"タイミング情報ファイルの指定がないか、データに問題があります。('{Path.GetFileName(labPath)}')",
			ResultType.WavNotFound => $"音声ファイルの指定がないか、データに問題があります。('{Path.GetFileName(wavPath)}')",
			ResultType.TimingDataCountExcept
				=> "タイミング情報ファイルのデータに問題があります。期待された長さと異なります。「音素の置き換え」等が原因の可能性があります。",
			ResultType.PitchDataCountExcept
				=> "音声ファイルから解析したピッチのデータに問題があります。期待された長さと異なります。異なる曲や調声の歌唱データを指定していることなどが原因の可能性があります。",
			ResultType.TimingAndPitchCountExcept
				=> "タイミング情報ファイルと音声ファイルの解析結果の長さが一致しません。別の曲や調声のファイルを指定している事が原因の可能性があります。",
			_ => "データに想定外の問題があります。",
		};
		_notify.Error(
			"Invalid song data is found!",
			$"読み込んだソングデータに問題があります。{reason}");
		CanExport = true;
		sw.Stop();
		_notify.Warn(
			"Export failure...",
			$"経過時間: {sw.Elapsed.TotalSeconds:F3} sec."
		);
	}

	private static Func<ValueTask> CloseEvent =>
		TalkDataConverter
				.DisposeOpenJTalkAsync;

	private Func<ValueTask> ReadyFunc =>
		async () =>
		{
			var loading = _notify
				.Loading("Now awaking...", "起動しています。");

			//load defs

			await LoadCastDataAsync()
				.ConfigureAwait(true);

			SelectedCastIndex =
				SettingManager.SelectedTab;

			try
			{
				await CheckAsync()
					.ConfigureAwait(true);
			}
			catch (System.Exception e)
			{
				_notify.Warn(
			"Update check failure",
			$"更新確認ができませんでした… 理由:{e.Message}"
				);
			}
			finally
			{
				HasUpdate = HasCastDataUpdate || HasAppUpdate;
				_notify.Dismiss(loading!);
			}

			CanExport = CheckExportable();
			IsNotReady = false;
		};

	public async ValueTask LoadCastDataAsync(bool forceReload = false)
	{
		Definitions? defs = default;
		if(forceReload)
		{
			defs = await CastDefManager
				.ReloadCastDefsAsync()
				.ConfigureAwait(true);
		}else
		{
			defs = await CastDefManager
				.GetAllCastDefsAsync()
				.ConfigureAwait(true);
		}

		var targets = defs
			.Casts
			.Where(c => c.Product is Product.VoiSona && c.Category is Category.TextVocal);
		TalkCasts = new(targets);
	}

	public async ValueTask CheckAsync()
	{
		//app update check
		HasAppUpdate = await UpdateChecker
			.Build()
			.IsAvailableAsync()
			.ConfigureAwait(true);
		//def update check
		HasCastDataUpdate = await CastDefManager
			.HasUpdateAsync()
			.ConfigureAwait(true);
	}

	private Func<string, ValueTask> ResetParameterEvent => (paramName) =>
	{
		switch (paramName)
		{
			case nameof(ConsonantOffsetSec):
				ConsonantOffsetSec = SettingManager.DefaultConsonantOffset;
				SettingManager.ConsonantOffset = SettingManager.DefaultConsonantOffset;
				break;
			case nameof(ThretholdSplitNote):
				ThretholdSplitNote = SettingManager.DefaultThretholdSplitNote;
				SettingManager.ThretholdSplitNote = SettingManager.DefaultThretholdSplitNote;
				break;
			default:
				break;
		}
		return default;
	};

	bool CheckExportable()
	{
		if (OpenedCcsPath is null or [])
		{
			return false;
		}
		else if (IsUseLabFile && OpenedLabPath is null or [])
		{
			return false;
		}
		else if (IsUseWavFile && OpenedWavPath is null or [])
		{
			return false;
		}
		return true;
	}

	private enum TabIndex
	{
		ScoreOnly = 0,
		ScoreAndTiming = 1,
		ScoreTimingPitch = 2,
		Setting = 3,
	}

	[PropertyChanged(nameof(ConsonantOffsetSec))]
	[SuppressMessage("","IDE0051")]
	private ValueTask ConsonantOffsetSecChangedAsync(decimal value)
	{
		if (ConsonantOffsetSec == value) return default;

		//ConsonantOffsetSec = value;
		SettingManager.ConsonantOffset = value;
		return default;
	}

	[PropertyChanged(nameof(ThretholdSplitNote))]
	[SuppressMessage("","IDE0051")]
	private ValueTask ThretholdSplitNoteChangedAsync(double value)
	{
		if (ThretholdSplitNote == value) return default;

		ThretholdSplitNote = value;
		SettingManager.ThretholdSplitNote = value;
		return default;
	}

	[PropertyChanged(nameof(SelectedTab))]
	[SuppressMessage("","IDE0051")]
	private ValueTask SelectedTabChangedAsync(int value)
	{
		if(IsNotReady){ return default; }

		//force
		switch (value)
		{
			case (int)TabIndex.ScoreOnly:
				IsUseLabFile = false;
				IsUseWavFile = false;
				break;
			case (int)TabIndex.ScoreAndTiming:
				IsUseLabFile = true;
				IsUseWavFile = false;
				break;
			case (int)TabIndex.ScoreTimingPitch:
				IsUseLabFile = false;
				IsUseWavFile = true;
				break;
			default:
				//throw new ArgumentException("Unsupported value",nameof(value));
				IsUseLabFile = false;
				IsUseWavFile = false;
				break;
		}
		CanExport = CheckExportable();
		SettingManager.SelectedTab = value;
		return default;
	}
	[PropertyChanged(nameof(SelectedCastIndex))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask SelectedCastIndexChangedAsync(int value)
	{
		if (TalkCasts is null || value < 0) return default;

		var def = TalkCasts[value];

		//version
		TalkCastVersions = new(def.Versions.OrderDescending());
		SelectedCastVersionIndex = 0;   //reset

		return default;
	}

	[PropertyChanged(nameof(SelectedCastVersionIndex))]
	[SuppressMessage("","IDE0051")]
	private ValueTask SelectedCastVersionIndexChangedAsync(int value)
	{
		if (TalkCasts is null) return default;
		if (value < 0) return default;
		if (TalkCastVersions is null) return default;

		var def = TalkCasts[SelectedCastIndex];

		//style
		var list = def
			.Emotions
			.Select(e => e.Names.First(n => n.Lang == CevioCasts.Lang.Japanese).Display)
			.Select(e => new StyleRate(e, 0))
			;
		//style order
		if(def.EmotionOrder is not null)
		{
			var version = TalkCastVersions[value];
			var order = def
				.EmotionOrder
				.First(o => string.Equals(o.Version, version, StringComparison.Ordinal))
				.Order;

			list = order
				.Select(i => def.Emotions.First(e => string.Equals(e.Id, i, StringComparison.Ordinal)))
				.Select(e => e!.Names.First(n => n.Lang == CevioCasts.Lang.Japanese).Display)
				.Select(e => new StyleRate(e, rate: 0))
				;
		}

		Styles?.Clear();
		Styles = new(list);
		Styles[0].Rate = 100;
		return default;
	}
}
