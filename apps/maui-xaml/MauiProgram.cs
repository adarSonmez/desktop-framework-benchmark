using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.Maui.LifecycleEvents;

namespace DesktopBench.MauiXaml;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureLifecycleEvents(ev => ev.AddWindows(w => w.OnWindowCreated(WindowSizing.Apply)));
        return builder.Build();
    }
}

/// <summary>Sizes the client area to 1280×800 DIPs and centers the window (spec/APP_SPEC.md).</summary>
static class WindowSizing
{
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(nint hwnd);

    public static void Apply(Microsoft.UI.Xaml.Window win)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(win);
        var scale = GetDpiForWindow(hwnd) / 96.0;
        var appWindow = win.AppWindow;
        appWindow.ResizeClient(new Windows.Graphics.SizeInt32((int)(1280 * scale), (int)(800 * scale)));
        var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(appWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
        appWindow.Move(new Windows.Graphics.PointInt32(area.X + (area.Width - appWindow.Size.Width) / 2, area.Y + (area.Height - appWindow.Size.Height) / 2));
    }
}
