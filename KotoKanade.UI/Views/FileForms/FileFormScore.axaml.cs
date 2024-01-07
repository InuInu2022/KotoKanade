using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KotoKanade.Views;

public partial class FileFormScore : UserControl
{
	public static readonly StyledProperty<string> DefaultFileNameProperty =
        AvaloniaProperty.Register<FileFormScore, string>(
			nameof(DefaultFileName)
		);

    public string DefaultFileName
    {
		get => GetValue(DefaultFileNameProperty);
        set => SetValue(DefaultFileNameProperty, value);
    }
    public FileFormScore()
    {
        InitializeComponent();
    }
}