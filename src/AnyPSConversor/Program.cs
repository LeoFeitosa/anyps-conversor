namespace AnyPSConversor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        // Uso interno do CI: gera a imagem do README a cada release, sem tocar nas configurações do usuário.
        // Um jogo opcional mostra o cartão dele, para conferir a tela sem abrir o programa de verdade.
        if (args is ["--screenshot", var language, var file, .. var rest])
        {
            Strings.Set(language == "pt" ? Language.Portuguese : Language.English, save: false);
            MainForm.SaveScreenshot(file, rest.FirstOrDefault());
            return;
        }
        Settings.MigrateLegacyFolders();
        Archives.CleanStaleTemp(Settings.Output, Settings.DefaultOutput, Settings.LegacyDefaultOutput);
        Converter.CleanLocalData();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
