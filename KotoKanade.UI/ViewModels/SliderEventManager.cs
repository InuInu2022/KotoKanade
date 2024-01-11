using Avalonia.Controls;

namespace KotoKanade.ViewModels;

public static class SliderEventManager
{
	public static void AddSliderEvent(Slider slider, double tick = 0.01)
	{
		slider.PointerWheelChanged += (sender, e) =>
		{
			// マウスホイールが動かされたときの処理
			var delta = e.Delta.Y; // ホイールの移動量を取得（上方向の場合は正、下方向の場合は負）

			if (sender is not Slider sl)
			{
				return;
			}

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
