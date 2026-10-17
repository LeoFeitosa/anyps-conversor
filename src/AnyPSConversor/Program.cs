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
        // Em segundo plano: uma extração abandonada pode ter dezenas de GB e a janela não pode esperar por ela.
        var output = Settings.Output;
        _ = Task.Run(() => Archives.CleanStaleTemp(output, Settings.DefaultOutput, Settings.LegacyDefaultOutput));
        Converter.CleanLocalData();
        Application.Run(new MainForm(args.FirstOrDefault()));
        // Janela fechada = programa encerrado, mesmo com extração ou conversão ainda rodando em segundo plano.
        Environment.Exit(0);
    }
}
