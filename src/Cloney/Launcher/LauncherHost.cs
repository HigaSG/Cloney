using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;
using Cloney.Core;

namespace Cloney.Launcher;

/// <summary>Ponto de entrada do modo "--launch &lt;perfil&gt;", chamado pelos atalhos.</summary>
internal static class LauncherHost
{
    public static int Run(string profileId)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var profile = ProfileStore.Find(profileId);
        if (profile == null)
        {
            ShowError($"O perfil \"{profileId}\" não existe mais. Abra o Cloney e recrie o atalho.");
            return 2;
        }

        ProcessStartInfo psi;
        try { psi = LaunchBuilder.Build(profile); }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            return 3;
        }

        // Um launcher por perfil. Se já existe um, só repassamos a abertura para o app
        // (apps de instância única, como Electron e Chrome, focam ou abrem uma nova janela).
        using var mutex = new Mutex(true, @"Local\Cloney.Launcher." + profile.Id, out bool isFirst);
        if (!isFirst)
        {
            try { Process.Start(psi)?.Dispose(); }
            catch (Exception ex) { ShowError(ex.Message); return 3; }
            return 0;
        }

        using var context = new InstanceContext(profile);
        if (!context.Start(psi)) return 4;
        Application.Run(context);
        GC.KeepAlive(mutex);
        return 0;
    }

    public static void ShowError(string message) =>
        MessageBox.Show(message, "Cloney", MessageBoxButtons.OK, MessageBoxIcon.Error);
}
