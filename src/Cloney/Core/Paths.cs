using System;
using System.IO;

namespace Cloney.Core;

internal static class Paths
{
    public static string Root { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Cloney");

    public static string ProfilesFile => Path.Combine(Root, "profiles.json");
    public static string DataRoot => Path.Combine(Root, "data");
    public static string IconsDir => Path.Combine(Root, "icons");

    /// <summary>Pasta do Menu Iniciar onde ficam os atalhos dos perfis.</summary>
    public static string StartMenuDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Cloney Apps");

    public static string DesktopDir => Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

    /// <summary>Caminho do próprio Cloney.exe (alvo dos atalhos).</summary>
    public static string SelfExe => Environment.ProcessPath ?? throw new InvalidOperationException("ProcessPath indisponível");

    /// <summary>True se <paramref name="path"/> está dentro da pasta de dados padrão do Cloney.</summary>
    public static bool IsInsideDataRoot(string path)
    {
        var full = Path.GetFullPath(path).TrimEnd('\\') + "\\";
        var root = Path.GetFullPath(DataRoot).TrimEnd('\\') + "\\";
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(full, root, StringComparison.OrdinalIgnoreCase);
    }
}
