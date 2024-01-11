using Avalonia.Controls;
using Epoxy;

namespace KotoKanade.ViewModels;

[ViewModel]
public sealed class SliderViewModel
{
	public Command Ready { get; set; }
	public Pile<Slider>? ConsonantSlider { get; private set; }

	public Command? ConsonantSliderWheelEvent { get; set; }

	public SliderViewModel()
	{
		Ready = Epoxy.Command.Factory.Create(async () =>
		{
			ConsonantSlider = Pile.Factory.Create<Slider>();

			if (ConsonantSlider is null) return;

			await ConsonantSlider.RentAsync(slider =>
			{
				AddSliderEvent(slider);
				return default;
			}).ConfigureAwait(true);
		});
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
