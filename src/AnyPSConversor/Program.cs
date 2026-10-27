namespace AnyPSConversor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // Internal CI use: generates the README image on every release, without touching the user's settings.
        // An optional game shows its card, to check the screen without actually opening the program.
        if (args is ["--screenshot", var language, var file, .. var rest])
        {
            Strings.Set(language == "pt" ? Language.Portuguese : Language.English, save: false);
            MainForm.SaveScreenshot(file, rest.FirstOrDefault());
            return;
        }
        Settings.MigrateLegacyFolders();
        // In the background: an abandoned extraction can be tens of GB and the window cannot wait for it.
        var output = Settings.Output;
        _ = Task.Run(() => Archives.CleanStaleTemp(output, Settings.DefaultOutput, Settings.LegacyDefaultOutput));
        Converter.CleanLocalData();
        Application.Run(new MainForm(args.FirstOrDefault()));
        // Window closed = program exited, even with extraction or conversion still running in the background.
        Environment.Exit(0);
    }
}
