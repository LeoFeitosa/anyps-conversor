namespace AnyPS5Conversor;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        Archives.CleanStaleTemp();
        Application.Run(new MainForm(args.FirstOrDefault()));
    }
}
