using System;
using System.IO;
using System.Linq;
using Cloney.Native;

namespace Cloney.Core;

internal static class Shortcuts
{
    public static string SafeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Where(c => !invalid.Contains(c)).ToArray()).Trim().TrimEnd('.');
        return string.IsNullOrEmpty(clean) ? "Perfil" : clean;
    }

    /// <summary>Recria os atalhos do perfil (Menu Iniciar e, se marcado, Área de Trabalho).</summary>
    public static void Sync(Profile profile)
    {
        Remove(profile);

        var name = SafeFileName(profile.Name);
        Directory.CreateDirectory(Paths.StartMenuDir);
        Create(Path.Combine(Paths.StartMenuDir, name + ".lnk"), profile);
        if (profile.DesktopShortcut)
            Create(Path.Combine(Paths.DesktopDir, name + ".lnk"), profile);

        profile.ShortcutName = name;
    }

    public static void Remove(Profile profile)
    {
        if (string.IsNullOrEmpty(profile.ShortcutName)) return;
        foreach (var dir in new[] { Paths.StartMenuDir, Paths.DesktopDir })
        {
            var lnk = Path.Combine(dir, profile.ShortcutName + ".lnk");
            if (File.Exists(lnk) && IsOurs(lnk, profile))
                File.Delete(lnk);
        }
    }

    private static bool IsOurs(string lnk, Profile profile) =>
        ShellLink.ReadArguments(lnk)?.Contains("--launch " + profile.Id, StringComparison.OrdinalIgnoreCase) == true;

    private static void Create(string lnkPath, Profile profile)
    {
        var exe = Paths.SelfExe;
        ShellLink.Create(
            lnkPath,
            target: exe,
            arguments: "--launch " + profile.Id,
            workingDir: Path.GetDirectoryName(exe)!,
            iconPath: profile.IconFile is { } ico && File.Exists(ico) ? ico : exe,
            description: "Cloney: " + profile.Name,
            appUserModelId: profile.AppUserModelId);
    }
}
