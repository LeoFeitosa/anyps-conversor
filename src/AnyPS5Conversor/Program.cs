namespace AnyPS5Conversor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Archives.CleanStaleTemp();
        Converter.CleanLocalData();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
