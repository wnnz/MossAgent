using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using MossAgent.App.Controls;
using SkiaSharp;

namespace MossAgent.App.Visual.Tests;

internal sealed class SelectorFocusRenderFixture : IDisposable
{
    public SelectorFocusRenderFixture(bool dark, string kind, double scale)
    {
        Kind = kind;
        Scale = scale;
        Selector = kind == "pill" ? new AppPillComboBox() : new AppComboBox();
        if (kind == "compact") Selector.Classes.Add("compact-selector");
        Selector.ItemsSource = new[] { "low", "medium", "high" };
        Selector.SelectedIndex = 1;
        Selector.MinWidth = 200;
        Window = new AppWindow
        {
            Width = 380,
            Height = 300,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel
            {
                Margin = new Thickness(30),
                Spacing = 24,
                Children = { new AppText { Text = "思考强度" }, Selector, new AppButton { Content = "其他操作" } }
            }
        };
        Window.Show();
        Window.SetRenderScaling(scale);
        Flush();
        Window.MouseMove(new Point(360, 280));
    }

    public string Kind { get; }
    public double Scale { get; }
    public AppComboBox Selector { get; }
    public AppWindow Window { get; }

    public void Flush()
    {
        Window.UpdateLayout();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public void Key(Key key, PhysicalKey physical)
    {
        Window.KeyPress(key, RawInputModifiers.None, physical, null);
        Window.KeyRelease(key, RawInputModifiers.None, physical, null);
        Flush();
    }

    public ComboBoxItem OpenAndNavigate()
    {
        if (!Selector.Focus(NavigationMethod.Tab)) throw new InvalidOperationException("Selector cannot receive focus.");
        Key(Avalonia.Input.Key.F4, PhysicalKey.F4);
        Key(Avalonia.Input.Key.Down, PhysicalKey.ArrowDown);
        return Selector.ContainerFromIndex(2) as ComboBoxItem
            ?? throw new InvalidOperationException("Dropdown item is unavailable.");
    }

    public SKBitmap Capture(string state)
    {
        Flush();
        using var frame = Window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No rendered frame.");
        using var stream = new MemoryStream();
        frame.Save(stream, PngBitmapEncoderOptions.Default);
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
            frame.Save(Path.Combine(directory, $"selector-focus-{Kind}-{Window.ActualThemeVariant}-{Scale}-{state}.png"), PngBitmapEncoderOptions.Default);
        }
        return SKBitmap.Decode(stream.ToArray());
    }

    public SKColor Pixel(SKBitmap bitmap, ComboBoxItem item, Point offset)
    {
        var point = item.TranslatePoint(offset, Window)!.Value;
        return bitmap.GetPixel((int)(point.X * Scale), (int)(point.Y * Scale));
    }

    public void Dispose() => Window.Close();
}
