using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cloney.Core;

/// <summary>Uma "instância clonada": qual app abrir, com quais dados e com qual aparência.</summary>
internal sealed class Profile
{
    public string Id { get; set; } = NewId();
    public string Name { get; set; } = "";
    public string PresetId { get; set; } = "generic";

    /// <summary>Executável escolhido pelo usuário (ignorado em presets MSIX, que são localizados sozinhos).</summary>
    public string? ExePath { get; set; }

    /// <summary>Pasta de dados; vazio = %LOCALAPPDATA%\Cloney\data\&lt;id&gt;.</summary>
    public string? DataDir { get; set; }

    public string ExtraArgs { get; set; } = "";
    public Dictionary<string, string> Env { get; set; } = new();
    public string? WorkingDir { get; set; }

    public bool ShowTrayIcon { get; set; } = true;
    public bool RestartOnExit { get; set; }
    public bool WipeDataOnExit { get; set; }
    public bool DesktopShortcut { get; set; }

    public string? IconSource { get; set; }
    public string? BadgeText { get; set; }
    public string? TintColor { get; set; }

    /// <summary>.ico gerado para o perfil (atalho, bandeja e janelas).</summary>
    public string? IconFile { get; set; }

    /// <summary>Nome de arquivo usado no último atalho criado (para removê-lo ao renomear).</summary>
    public string? ShortcutName { get; set; }

    [JsonIgnore]
    public string AppUserModelId => "Cloney.Profile." + Id;

    [JsonIgnore]
    public string EffectiveDataDir => string.IsNullOrWhiteSpace(DataDir)
        ? Path.Combine(Paths.DataRoot, Id)
        : Path.GetFullPath(Environment.ExpandEnvironmentVariables(DataDir));

    [JsonIgnore]
    public bool HasCustomLook =>
        !string.IsNullOrWhiteSpace(IconSource) || !string.IsNullOrWhiteSpace(BadgeText) ||
        !string.IsNullOrWhiteSpace(TintColor);

    public Profile Clone() => JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(this))!;

    public static string NewId() => Guid.NewGuid().ToString("N")[..8];
}
