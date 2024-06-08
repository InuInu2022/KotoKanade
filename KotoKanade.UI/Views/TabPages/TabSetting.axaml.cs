using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using KotoKanade.ViewModels;

namespace KotoKanade.Views;

public partial class TabSetting : UserControl
{
	public TabSetting()
	{
		InitializeComponent();
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
	{
		var vm = DataContext as TabSettingsViewModel;
		if(vm is not null)
		{
			var parent = this.FindAncestorOfType<MainView>();
			vm.MainViewModel = parent?.DataContext as MainViewModel;
		}
	}
}