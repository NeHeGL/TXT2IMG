using Microsoft.Windows.ApplicationModel.DynamicDependency;

namespace TXT2IMG;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) => CrashLog.ReportFatal(e.ExceptionObject as Exception);

        var initResult = Bootstrap.TryInitialize(0x00020002, out var hresult);
        if (!initResult)
        {
            CrashLog.ReportFatal(new InvalidOperationException($"Windows App SDK Bootstrap.TryInitialize failed: 0x{hresult:X8}"));
            Environment.Exit(hresult);
            return;
        }

        try
        {
            global::WinRT.ComWrappersSupport.InitializeComWrappers();
            global::Microsoft.UI.Xaml.Application.Start(p =>
            {
                var context = new global::Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(global::Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
                global::System.Threading.SynchronizationContext.SetSynchronizationContext(context);
                new App();
            });
        }
        catch (Exception ex)
        {
            CrashLog.ReportFatal(ex);
            throw;
        }
        finally
        {
            Bootstrap.Shutdown();
        }
    }
}
