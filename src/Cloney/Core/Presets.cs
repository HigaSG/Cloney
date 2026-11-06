using System.Collections.Generic;
using System.Linq;

namespace Cloney.Core;

/// <summary>
/// Regras de compatibilidade por tipo de app. <c>{data}</c> nos argumentos vira a pasta de dados do perfil.
/// </summary>
internal sealed record Preset(
    string Id,
    string Name,
    string ArgsTemplate,
    string[] ExeCandidates,
    string? MsixName = null,
    string? MsixExe = null,
    bool HomeOverride = false,
    string? Note = null)
{
    public bool IsMsix => MsixName != null;
    public override string ToString() => Name;
}

internal static class Presets
{
    private const string ChromiumArgs = "--user-data-dir=\"{data}\"";
    private const string VsCodeArgs = "--user-data-dir \"{data}\\user-data\" --extensions-dir \"{data}\\extensions\"";

    public static readonly IReadOnlyList<Preset> All = new[]
    {
        new Preset("claude", "Claude Desktop", ChromiumArgs, [],
            MsixName: "Claude", MsixExe: @"app\claude.exe",
            Note: "Login: o link enviado pelo navegador abre a instância principal. Na janela do clone, " +
                  "escolha entrar com código e digite o código recebido; depois feche e abra o clone de novo."),

        new Preset("chrome", "Google Chrome", ChromiumArgs,
        [
            @"%ProgramFiles%\Google\Chrome\Application\chrome.exe",
            @"%ProgramFiles(x86)%\Google\Chrome\Application\chrome.exe",
            @"%LOCALAPPDATA%\Google\Chrome\Application\chrome.exe",
        ]),

        new Preset("edge", "Microsoft Edge", ChromiumArgs,
        [
            @"%ProgramFiles(x86)%\Microsoft\Edge\Application\msedge.exe",
            @"%ProgramFiles%\Microsoft\Edge\Application\msedge.exe",
        ]),

        new Preset("brave", "Brave", ChromiumArgs,
        [
            @"%ProgramFiles%\BraveSoftware\Brave-Browser\Application\brave.exe",
            @"%LOCALAPPDATA%\BraveSoftware\Brave-Browser\Application\brave.exe",
        ]),

        new Preset("vivaldi", "Vivaldi", ChromiumArgs,
        [
            @"%LOCALAPPDATA%\Vivaldi\Application\vivaldi.exe",
            @"%ProgramFiles%\Vivaldi\Application\vivaldi.exe",
        ]),

        new Preset("firefox", "Mozilla Firefox", "-profile \"{data}\" -no-remote",
        [
            @"%ProgramFiles%\Mozilla Firefox\firefox.exe",
            @"%ProgramFiles(x86)%\Mozilla Firefox\firefox.exe",
        ]),

        new Preset("vscode", "Visual Studio Code", VsCodeArgs,
        [
            @"%LOCALAPPDATA%\Programs\Microsoft VS Code\Code.exe",
            @"%ProgramFiles%\Microsoft VS Code\Code.exe",
        ]),

        new Preset("cursor", "Cursor", VsCodeArgs,
        [
            @"%LOCALAPPDATA%\Programs\cursor\Cursor.exe",
        ]),

        new Preset("windsurf", "Windsurf", VsCodeArgs,
        [
            @"%LOCALAPPDATA%\Programs\Windsurf\Windsurf.exe",
        ]),

        new Preset("electron", "App Electron (genérico)", ChromiumArgs, [],
            Note: "Funciona com a maioria dos apps feitos em Electron (Slack, Notion, Obsidian…). " +
                  "Escolha o .exe principal do app."),

        new Preset("home", "Outro app (redirecionar APPDATA/HOME)", "", [], HomeOverride: true,
            Note: "Troca APPDATA, LOCALAPPDATA, USERPROFILE e HOME para dentro da pasta de dados. " +
                  "Só separa apps que usam essas variáveis; muitos apps nativos ignoram."),

        new Preset("generic", "Outro app (sem separar dados)", "", [],
            Note: "Apenas abre o app com nome, ícone e argumentos próprios. Os dados continuam compartilhados."),
    };

    public static Preset Get(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All.Last();
}
