// <copyright file="MainView.axaml.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace KotoKanade.Views;

public sealed partial class MainView : UserControl
{
	public MainView()
	{
		InitializeComponent();
	}

	private void InitializeComponent() =>
        AvaloniaXamlLoader.Load(this);

	private void Slider_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
	{
		// マウスホイールが動かされたときの処理
		var delta = e.Delta.Y; // ホイールの移動量を取得（上方向の場合は正、下方向の場合は負）

		if (sender is not Slider slider)
		{
			return;
		}

		const double tick = 0.01;

		// スライダーの値を変更
		if (delta > 0)
		{
			slider.Value += tick; // マウスホイールが上向きに動いた場合、値を増加させる
		}
		else if (delta < 0)
		{
			slider.Value -= tick; // マウスホイールが下向きに動いた場合、値を減少させる
		}
	}
}
