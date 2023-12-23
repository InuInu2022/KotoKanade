// <copyright file="MainWindow.axaml.cs" company="InuInu">
// Copyright (c) InuInu. All rights reserved.
// </copyright>
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KotoKanade.Views;

public sealed partial class MainWindow : Window
{
    public MainWindow() =>
        InitializeComponent();

    private void InitializeComponent() =>
        AvaloniaXamlLoader.Load(this);
}
