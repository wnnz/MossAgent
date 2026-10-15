using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;

namespace MossAgent.App.Tests;

internal sealed class SettingsDialogFocusFixture : IDisposable
{
    public SettingsDialogFocusFixture(bool dark = false)
    {
        Dialog.Content = new StackPanel { Children = { First, Second } };
        Dialog.CloseCommand = new RelayCommand(() => Dialog.IsOpen = false);
        Dialog.Footer = new AppButton { Content = "取消", Command = Dialog.CloseCommand };
        Page.Children.Add(Dialog);
        Grid.SetColumn(Page, 1);
        Window = new AppWindow
        {
            Width = 980,
            Height = 700,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Children = { Navigation, Page }
            }
        };
        Window.Show();
        Flush();
    }

    public AppWindow Window { get; }
    public AppButton Navigation { get; } = new() { Content = "AI 提供商" };
    public Grid Page { get; } = new();
    public AppSettingsDialog Dialog { get; } = new() { Title = "配置模型" };
    public AppInput First { get; } = new() { Text = "模型 ID 草稿" };
    public AppInput Second { get; } = new() { Text = "显示名称草稿" };

    public void Open()
    {
        Navigation.Focus(NavigationMethod.Tab);
        Dialog.IsOpen = true;
        Flush();
    }

    public void HidePage()
    {
        Page.IsVisible = false;
        Navigation.Focus(NavigationMethod.Tab);
        Flush();
    }

    public void ShowPage()
    {
        Page.IsVisible = true;
        Flush();
    }

    public void PressKey(Key key)
    {
        Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Flush();
    }

    public void Flush()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    public void Dispose()
    {
        Dialog.IsOpen = false;
        Window.Close();
    }
}
