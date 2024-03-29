﻿// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Notification;
using CevioCasts;
using Epoxy;
using FluentAvalonia.UI.Controls;
using KotoKanade.Core.Models;

namespace KotoKanade.ViewModels;

[ViewModel]
public sealed class MainViewModel
{
	public Command Ready { get; }
	public Command Close { get; }
	public string Title { get; private set; } = "test";

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

	public decimal ConsonantOffsetSec { get; set; }
		= SettingManager.ConsonantOffset;
	public Command? ResetConsonantOffset { get; }
	public double TimeScaleFactor { get; set; } = 0.030;

	public Command ExportFile { get; set; }
	public bool CanExport { get; set; }

	public INotificationMessageManager Manager { get; }
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

		//ResetConsonantOffset = Command.Factory.Create(ResetConsonantEvent);

		ResetConsonantOffset = Command.Factory.Create(ResetParameterEvent);
		ResetThretholdSplitNote = Command.Factory.Create(ResetParameterEvent);

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

		pathes.ToList().ForEach(f => Debug.WriteLine($"path: {f}"));
		OpenedCcsPath = pathes[0];
		DefaultCcs = Path.GetFileName(pathes[0]);

		//auto file select (same name file only)
		if (IsUseWavFile)
		{
			AutoSearchFile("wav");
		}
		if (IsUseLabFile || IsUseWavFile){
			AutoSearchFile("lab");
		}

		void AutoSearchFile(string extension)
		{
			var search = Path.ChangeExtension(OpenedCcsPath, extension);
			var isExists = File.Exists(search);
			if(isExists){
				if(string.Equals(extension, "wav", StringComparison.Ordinal))
				{
					OpenedWavPath = search;
					DefaultWav = Path.GetFileName(search);
				}
				else if(string.Equals(extension, "lab", StringComparison.Ordinal))
				{
					OpenedLabPath = search;
					DefaultLab = Path.GetFileName(search);
				}
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

			if (saved is null)
			{
				_notify.Dismiss(loading!);
				_notify.Warn(
					"Save directory is not found!",
					"保存先ディレクトリが見つかりません！");
				CanExport = true;
				return;
			}
			var saveDir = saved.Path.LocalPath;
			if (saveDir is null)
			{
				_notify.Dismiss(loading!);
				_notify.Warn(
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

			var isSplit = IsSplitNotes;

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

			CanExport = true;
			_notify.Dismiss(loading!);
			sw.Stop();
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

	private static Func<ValueTask> CloseEvent =>
		TalkDataConverter
				.DisposeOpenJTalkAsync;

	private Func<ValueTask> ReadyFunc =>
		async () =>
		{
			var loading = _notify
				.Loading("Now awaking...","起動しています。");

			var defs = await CastDefManager
				.GetAllCastDefsAsync()
				.ConfigureAwait(true);
			var targets = defs
				.Casts
				.Where(c => c.Product is Product.VoiSona && c.Category is Category.TextVocal);
			TalkCasts = new(targets);

			SelectedCastIndex = 0;

			_notify.Dismiss(loading!);
			CanExport = true;
		};

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
		//if (SelectedTab == value) return default;

		SettingManager.SelectedTab = value;
		return default;
	}
	[PropertyChanged(nameof(SelectedCastIndex))]
	[SuppressMessage("", "IDE0051")]
	private ValueTask SelectedCastIndexChangedAsync(int value)
	{
		if (TalkCasts is null) return default;

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
