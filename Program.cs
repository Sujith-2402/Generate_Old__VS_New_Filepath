using Genarate_OldVsNew_Filepaths.UI;

namespace Genarate_OldVsNew_Filepaths;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
