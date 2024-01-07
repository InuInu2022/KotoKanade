// <copyright file="MainViewModel.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using System;
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
		SelectCcs = Command.Factory.Create(() => {
			return default;
		});
		SelectLab = Command.Factory.Create(() => {
			return default;
		});
		SelectWav = Command.Factory.Create(() => {
			return default;
		});

		// A handler for window loaded
		Ready = Command.Factory.Create(ReadyFunc());
	}

	private Func<ValueTask> ReadyFunc()
	{
		return async () =>
		{
			ConsonantSlider = Pile.Factory.Create<Slider>();

			await ConsonantSlider.RentAsync(slider =>
			{
				AddSliderEvent(slider);
				return default;
			}).ConfigureAwait(true);
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
