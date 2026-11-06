using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Cloney.Core;

internal static class LaunchBuilder
{
    /// <summary>Monta o comando que abre o app do perfil (executável, argumentos, ambiente e pasta de trabalho).</summary>
    public static ProcessStartInfo Build(Profile profile)
    {
        var exe = AppResolver.ResolveExe(profile)
                  ?? throw new FileNotFoundException(
                      $"Não encontrei o executável do perfil \"{profile.Name}\". " +
                      "Verifique se o app está instalado ou escolha o .exe no Cloney.");

        var preset = Presets.Get(profile.PresetId);
        var data = profile.EffectiveDataDir;
        Directory.CreateDirectory(data);

        string Expand(string s) => Environment.ExpandEnvironmentVariables(
            s.Replace("{data}", data).Replace("{id}", profile.Id).Replace("{name}", profile.Name));

        var args = string.Join(" ", new[] { preset.ArgsTemplate, profile.ExtraArgs }
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => Expand(a.Trim())));

        var psi = new ProcessStartInfo(exe, args)
        {
            UseShellExecute = false,
            WorkingDirectory = string.IsNullOrWhiteSpace(profile.WorkingDir)
                ? Path.GetDirectoryName(exe)!
                : Expand(profile.WorkingDir),
        };

        if (preset.HomeOverride)
        {
            var roaming = Path.Combine(data, "AppData", "Roaming");
            var local = Path.Combine(data, "AppData", "Local");
            Directory.CreateDirectory(roaming);
            Directory.CreateDirectory(local);
            psi.Environment["APPDATA"] = roaming;
            psi.Environment["LOCALAPPDATA"] = local;
            psi.Environment["USERPROFILE"] = data;
            psi.Environment["HOME"] = data;
        }

        foreach (var (key, value) in profile.Env)
            psi.Environment[key] = Expand(value);

        return psi;
    }
}
