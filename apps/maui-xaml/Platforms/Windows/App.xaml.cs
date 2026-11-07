using DesktopBench.Core;

namespace DesktopBench.MauiXaml.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        BenchEnv.StartTs = BenchEnv.NowMs();
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
