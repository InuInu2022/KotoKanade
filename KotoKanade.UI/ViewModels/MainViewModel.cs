// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System;
using System.Diagnostics;
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
	public Command? ConsonantSliderWheelEvent { get; set; }

	public Pile<Slider>? ConsonantSlider { get; set; }
	public string Title { get; private set; } = "test";

	public string DefaultCcs { get; set; } = string.Empty;
	public string? OpenedCcsPath { get; set; }
	public Command SelectCcs { get; }
	public string DefaultLab { get; set; } = string.Empty;
	public Command SelectLab { get; }
	public string DefaultWav { get; set; } = string.Empty;
	public Command SelectWav { get; }

	public FAComboBoxItem? SelectedCastItem { get; set; }
	public bool IsSplitNotes {get;set;} = true;
	public double ThretholdSplitNote { get; set; } = 250;
	public decimal ConsonantOffsetSec { get; set; } = -0.05m;

	public Command ExportFile { get; set; }

	public MainViewModel()
	{
		SelectCcs = Command.Factory.Create(async () => {
			var songCcs = await StorageUtil.OpenCevioFileAsync(
				title:"ソングデータを含むccsファイルを選んでください",
				allowMultiple: false,
				path:null
			).ConfigureAwait(true);

			var pathes = StorageUtil
				.GetPathesFromOpenedFiles(songCcs);
			if(pathes is not {Count: >0}){ return; }

			pathes.ToList().ForEach(f => Debug.WriteLine($"path: {f}"));
			OpenedCcsPath = pathes[0];
			DefaultCcs = Path.GetFileName(pathes[0]);
		});
		SelectLab = Command.Factory.Create(() => {
			return default;
		});
		SelectWav = Command.Factory.Create(() => {
			return default;
		});

		// A handler for window loaded
		Ready = Command.Factory.Create(ReadyFunc());

		ExportFile = Command.Factory.Create(async ()=>{
			var path = OpenedCcsPath ?? "";

			var saved = await StorageUtil.SaveAsync(
				path: OpenedCcsPath ?? "",
				patterns: ["*.tsrprj"],
				changeExt: ".tstprj",
				targetFileTypes: "VoiSonaTalkプロジェクトファイル"
			).ConfigureAwait(true);

			if(saved is null){
				//_notify?.Dismiss(loading!);
				return;
			}
			var saveDir = saved.Path.LocalPath;
			if(saveDir is null){
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
					null,
					- ConsonantOffsetSec	//表示と逆
				)
				.ConfigureAwait(false);
		});
	}

	private Func<ValueTask> ReadyFunc()
	{
		return () =>
		{
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
	}

	private static void AddSliderEvent(Slider slider)
	{
		slider.PointerWheelChanged += (sender, e) =>
		{
			// マウスホイールが動かされたときの処理
			var delta = e.Delta.Y; // ホイールの移動量を取得（上方向の場合は正、下方向の場合は負）

			if (sender is not Slider sl)
			{
				return;
			}

			const double tick = 0.01;

			// スライダーの値を変更
			if (delta > 0)
			{
				sl.Value += tick; // マウスホイールが上向きに動いた場合、値を増加させる
			}
			else if (delta < 0)
			{
				sl.Value -= tick; // マウスホイールが下向きに動いた場合、値を減少させる
			}
		};
	}
}
