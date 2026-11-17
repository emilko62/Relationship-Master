using RelationshipMaster.Services;

namespace RelationshipMaster;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Report(e.Exception, fatal: false);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Report(e.ExceptionObject as Exception, fatal: true);

        ApplicationConfiguration.Initialize();
        Application.Run(new UI.MainForm());
    }

    /// <summary>Logs to %AppData%\RelationshipMaster\error.log and tells the user instead of crashing silently.</summary>
    private static void Report(Exception? ex, bool fatal)
    {
        try
        {
            Directory.CreateDirectory(DataStore.Folder);
            File.AppendAllText(Path.Combine(DataStore.Folder, "error.log"), $"[{DateTime.Now:O}] {ex}\n\n");
        }
        catch { /* logging must never throw */ }
        MessageBox.Show(
            $"Something went wrong{(fatal ? " and the app has to close" : "")}:\n\n{ex?.Message}\n\nDetails were written to error.log in {DataStore.Folder}.",
            "Relationship Master", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
