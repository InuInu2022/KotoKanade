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
				SliderEventManager.AddSliderEvent(slider);
				return default;
			}).ConfigureAwait(true);
		});
	}
}
