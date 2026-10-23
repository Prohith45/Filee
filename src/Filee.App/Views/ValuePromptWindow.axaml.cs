// Copyright (c) KnifeLemon. Licensed under the MIT License.

using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Filee.App.Services;
using Filee.App.ViewModels;
using Filee.Core.Presets;

namespace Filee.App.Views;

public partial class ValuePromptWindow : Window
{
    public ValuePromptWindow()
    {
        InitializeComponent();
        Motion.Track(this);
        Opened += (_, _) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (TargetSizeInput.IsVisible)
                {
                    TargetSizeInput.Focus();
                    TargetSizeInput.SelectAll();
                }
            }, DispatcherPriority.Input);
        };
    }

    public PromptAnswer? Result { get; private set; }

    public void PositionOnScreen(PixelPoint? near = null)
    {
        var screen = (near.HasValue ? Screens.ScreenFromPoint(near.Value) : null)
            ?? Screens.ScreenFromPoint(Screens.Primary?.Bounds.Center ?? default)
            ?? Screens.Primary;

        if (screen is not null)
        {
            var scaling = screen.Scaling;
            var area = screen.WorkingArea;
            var winW = (int)(Width * scaling);
            var winH = (int)(Height * scaling);
            if (winH <= 0) winH = (int)(220 * scaling);
            var x = near.HasValue
                ? Math.Clamp(near.Value.X - winW / 2, area.X, area.Right - winW)
                : area.X + Math.Max(0, (area.Width - winW) / 2);
            var y = near.HasValue
                ? Math.Clamp(near.Value.Y - winH / 2, area.Y, area.Bottom - winH)
                : area.Y + Math.Max(0, (area.Height - winH) / 2);
            Position = new PixelPoint(x, y);
        }
    }

    public void Confirm()
    {
        if (DataContext is not ValuePromptViewModel vm || !vm.IsValid) return;
        Result = vm.BuildAnswer();
        Close();
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Confirm();

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        Result = null;
        Close();
    }
}
