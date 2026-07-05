using SkkoaStudio.Core.Settings;

namespace SkkoaStudio;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ShowFatalError(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception exception)
            {
                ShowFatalError(exception);
            }
        };

        try
        {
            Application.Run(new MainForm(new SkkoaSettingsService(), args));
        }
        catch (Exception ex)
        {
            ShowFatalError(ex);
        }
    }

    private static void ShowFatalError(Exception exception)
    {
        try
        {
            MessageBox.Show(
                exception.ToString(),
                "SKKOA Studio 오류",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
        }
    }
}
