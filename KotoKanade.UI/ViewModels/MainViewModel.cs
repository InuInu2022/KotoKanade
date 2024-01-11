// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Epoxy;
using FluentAvalonia.UI.Controls;
using KotoKanade.Core.Models;

namespace KotoKanade.ViewModels;

[ViewModel]
public sealed class MainViewModel
{
	public Command Ready { get; }
	public string Title { get; private set; } = "test";

	public string DefaultCcs { get; set; } = string.Empty;
	public string? OpenedCcsPath { get; set; }
	public Command SelectCcs { get; }
	public string DefaultLab { get; set; } = string.Empty;
	public Command SelectLab { get; }
	public string DefaultWav { get; set; } = string.Empty;
	public Command SelectWav { get; }

	public FAComboBoxItem? SelectedCastItem { get; set; }
	public int SelectedCastIndex { get; set; }
	public ObservableCollection<StyleRate>? Styles { get; set; }
	public bool IsSplitNotes {get;set;} = true;
	public double ThretholdSplitNote { get; set; } = 250;
	public decimal ConsonantOffsetSec { get; set; } = -0.05m;

	public Command ExportFile { get; set; }

	public MainViewModel()
	{
		SelectCcs = Command.Factory.Create(async () =>
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
		});
		SelectLab = Command.Factory.Create(() =>
		{
			return default;
		});
		SelectWav = Command.Factory.Create(() =>
		{
			return default;
		});

		// A handler for window loaded
		Ready = Command.Factory.Create(ReadyFunc);

		ExportFile = Command.Factory.Create(ExportEvent);

		Styles = [
			new("Normal", 12.3),
			new("Fine", 22.3),
			new("Sad", 32.3),
		];
	}

	private Func<ValueTask> ExportEvent =>
		async () =>
		{
			var path = OpenedCcsPath ?? "";

			var saved = await StorageUtil.SaveAsync(
				path: OpenedCcsPath ?? "",
				patterns: ["*.tsrprj"],
				changeExt: ".tstprj",
				targetFileTypes: "VoiSonaTalkプロジェクトファイル"
			).ConfigureAwait(true);

			if (saved is null)
			{
				//_notify?.Dismiss(loading!);
				return;
			}
			var saveDir = saved.Path.LocalPath;
			if (saveDir is null)
			{
				return;
			}

			var loadedSong = await ScoreParser
				.ProcessCcsAsync(path)
				.ConfigureAwait(true);

			await TalkDataConverter
				.GenerateFileAsync(
					loadedSong,
					saveDir,
					SelectedCastItem?.Content?.ToString(),
					(IsSplitNotes, ThretholdSplitNote),
					Styles?.Select(s => s.Rate).ToArray(),
					-ConsonantOffsetSec //表示と逆
				)
				.ConfigureAwait(false);
		};

	private Func<ValueTask> ReadyFunc =>
		() =>
		{
			SelectedCastIndex = 0;
			/*
			ConsonantSlider = Pile.Factory.Create<Slider>();

			if (ConsonantSlider is null) return;

			await ConsonantSlider.RentAsync(slider =>
			{
				AddSliderEvent(slider);
				return default;
			}).ConfigureAwait(true);
			*/
			return default;
		};



	[PropertyChanged(nameof(SelectedCastItem))]
	[SuppressMessage("", "IDE0051")]
	private async ValueTask SelectedCastItemChangedAsync(FAComboBoxItem? value)
	{
		if (value is null) return;

		if (value.Content is not string castName) return;

		var def = await TalkDataConverter
			.GetCastDefAsync(castName)
			.ConfigureAwait(true);

		var list = def
			.Emotions
			.Select(e => e.Names.First(n => n.Lang == CevioCasts.Lang.Japanese).Display)
			.Select(e => new StyleRate(e, 0))
			;
		Styles = new(list);
		Styles[0].Rate = 100;
	}
}
