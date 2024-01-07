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

namespace KotoKanade.ViewModels;

[ViewModel]
public sealed class MainViewModel
{
	public Command Ready { get; }
	public Command? ConsonantSliderWheelEvent { get; set; }

	public Command? TabSelectionChanged { get; }

	public Pile<Slider>? ConsonantSlider { get; set; }
	public string Title { get; private set; } = "test";

	public string DefaultCcs { get; set; } = string.Empty;
	public Command SelectCcs { get; }
	public string DefaultLab { get; set; } = string.Empty;
	public Command SelectLab { get; }
	public string DefaultWav { get; set; } = string.Empty;
	public Command SelectWav { get; }

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

		TabSelectionChanged = Command.Factory.Create<SelectionChangedEventArgs>( (e) => {
			Console.WriteLine("...");
			if(e.Source is not TabControl tabCtrl){
				return default;
			}
			if(tabCtrl.SelectedContent is not UserControl control){
				return default;
			}
			//force update
			control.UpdateLayout();
			return default;
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
