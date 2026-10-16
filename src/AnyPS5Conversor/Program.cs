namespace AnyPS5Conversor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // Uso interno do CI: gera a imagem do README a cada release, sem tocar nas configurações do usuário.
        if (args is ["--screenshot", var language, var file])
        {
            Strings.Set(language == "pt" ? Language.Portuguese : Language.English, save: false);
            MainForm.SaveScreenshot(file);
            return;
        }
        Archives.CleanStaleTemp(Settings.Output, Settings.DefaultOutput);
        Converter.CleanLocalData();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
