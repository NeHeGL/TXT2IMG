using Microsoft.UI.Xaml;

namespace TXT2IMG;

public partial class App : Application
{
    public App()
    {
        this.UnhandledException += (s, e) =>
        {
            e.Handled = true;
            CrashLog.ReportFatal(e.Exception);
        };
        this.InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        try
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
        catch (Exception ex)
        {
            CrashLog.ReportFatal(ex);
            throw;
        }
    }

    private Window? m_window;
}
