using System;
using System.IO;
using Microsoft.Win32;

namespace Cloney.Core;

internal static class AppResolver
{
    private const string PackagesKey =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    /// <summary>Executável que o perfil deve abrir, ou null se não for encontrado.</summary>
    public static string? ResolveExe(Profile profile)
    {
        var preset = Presets.Get(profile.PresetId);

        // Apps MSIX mudam de pasta a cada atualização, então são sempre localizados de novo.
        if (preset.IsMsix)
            return FindMsixExe(preset);

        if (!string.IsNullOrWhiteSpace(profile.ExePath))
        {
            var exe = Environment.ExpandEnvironmentVariables(profile.ExePath.Trim().Trim('"'));
            if (File.Exists(exe)) return exe;
        }

        return FindPresetExe(preset);
    }

    public static string? FindPresetExe(Preset preset)
    {
        if (preset.IsMsix) return FindMsixExe(preset);
        foreach (var candidate in preset.ExeCandidates)
        {
            var exe = Environment.ExpandEnvironmentVariables(candidate);
            if (File.Exists(exe)) return exe;
        }
        return null;
    }

    public static string? FindMsixExe(Preset preset)
    {
        var root = FindMsixRoot(preset.MsixName!);
        if (root == null) return null;
        var exe = Path.Combine(root, preset.MsixExe!);
        return File.Exists(exe) ? exe : null;
    }

    /// <summary>
    /// Pasta de instalação da versão mais nova de um pacote MSIX instalado para o usuário atual.
    /// O nome completo do pacote tem o formato Nome_Versão_Arquitetura_Recurso_Editor.
    /// </summary>
    public static string? FindMsixRoot(string packageName)
    {
        using var key = Registry.CurrentUser.OpenSubKey(PackagesKey);
        if (key == null) return null;

        string? best = null;
        Version? bestVersion = null;
        foreach (var fullName in key.GetSubKeyNames())
        {
            var parts = fullName.Split('_');
            if (parts.Length < 5 || !parts[0].Equals(packageName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!Version.TryParse(parts[1], out var version)) continue;
            if (parts[2].Equals("x86", StringComparison.OrdinalIgnoreCase) && Environment.Is64BitOperatingSystem) continue;

            using var sub = key.OpenSubKey(fullName);
            if (sub?.GetValue("PackageRootFolder") is not string folder || !Directory.Exists(folder)) continue;

            if (bestVersion == null || version > bestVersion)
            {
                bestVersion = version;
                best = folder;
            }
        }
        return best;
    }
}
