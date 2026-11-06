using System;
using Cloney.Launcher;
using Cloney.UI;

namespace Cloney;

internal static class Program
{
    // Uso:
    //   Cloney.exe                    -> abre o gerenciador de perfis
    //   Cloney.exe --launch <perfil>  -> inicia a instância do perfil (usado pelos atalhos)
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length >= 2 && string.Equals(args[0], "--launch", StringComparison.OrdinalIgnoreCase))
            return LauncherHost.Run(args[1]);

        var app = new System.Windows.Application
        {
            ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose
        };
        return app.Run(new MainWindow());
    }
}
