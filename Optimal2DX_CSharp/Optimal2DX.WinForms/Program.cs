namespace Optimal2DX.WinForms;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        if (args != null && args.Length > 0 && args[0] == "--test-export")
        {
            string outPath = args.Length > 1 ? args[1] : @"e:\Cut2dx\BaoCao_SoDoCat_Cut2DX.html";
            var form = new Form1();
            form.RunAndExportHtmlHeadless(outPath);
            Console.WriteLine($"SUCCESS: Exported HTML to {outPath}");
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }    
}
