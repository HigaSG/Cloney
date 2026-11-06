# Cloney

Lançador de múltiplas instâncias para Windows (uso pessoal), inspirado no conceito do Parall para macOS.
Cria "perfis": cada um abre o mesmo app com **dados próprios**, **nome e ícone próprios** e **botão
separado na barra de tarefas**, sem copiar nem modificar o app original.

## Como funciona

- Cada perfil vira um atalho em **Menu Iniciar › Cloney Apps** (e, se quiser, na Área de Trabalho).
- O atalho roda `Cloney.exe --launch <perfil>`. Esse launcher:
  - monta o comando do app com a pasta de dados do perfil (`--user-data-dir`, `-profile`, etc.);
  - dá às janelas do app um **AppUserModelID** próprio, que separa o botão da barra de tarefas;
  - aplica o ícone do perfil (cor/etiqueta) nas janelas;
  - mostra um ícone na bandeja (Mostrar, Nova janela, Abrir pasta de dados, Encerrar);
  - pode **reabrir** o app quando ele fecha e **apagar os dados** ao fechar (sessão descartável).
- Apps MSIX (como o Claude Desktop) são localizados de novo a cada abertura, porque a pasta muda a cada atualização.

## Presets incluídos

Claude Desktop, Chrome, Edge, Brave, Vivaldi, Firefox, VS Code, Cursor, Windsurf, Electron genérico,
redirecionamento de APPDATA/HOME e genérico (sem separar dados).

**Claude Desktop:** o link de login do navegador sempre abre a instância principal. Na janela do clone,
entre com **código**, depois feche e abra o clone de novo.

## Onde ficam as coisas

| O quê | Onde |
|---|---|
| Programa | `%LOCALAPPDATA%\Programs\Cloney` |
| Perfis | `%LOCALAPPDATA%\Cloney\profiles.json` |
| Dados dos perfis | `%LOCALAPPDATA%\Cloney\data\<id>` (ou a pasta que você escolher) |
| Ícones gerados | `%LOCALAPPDATA%\Cloney\icons` |
| Atalhos | Menu Iniciar › Cloney Apps |

Desinstalar remove o programa e os atalhos, mas **mantém** perfis e dados.

## Compilar

Requisitos: .NET SDK 10 e Inno Setup 6 (`winget install Microsoft.DotNet.SDK.10 JRSoftware.InnoSetup`).

```powershell
powershell -ExecutionPolicy Bypass -File build.ps1
```

Gera `dist\Cloney-Setup-0.1.0.exe`. Sem assinatura digital: na primeira execução o SmartScreen avisa;
use **Mais informações › Executar assim mesmo**.

## Estrutura

```
src/Cloney/
  Program.cs            entrada: gerenciador (sem argumentos) ou launcher (--launch)
  Core/                 perfis, presets, localização de apps, montagem do comando, ícones, atalhos
  Launcher/             modo launcher (árvore de processos, AUMID, bandeja, reinício)
  Native/               P/Invoke e COM (IShellLink, IPropertyStore, toolhelp)
  UI/                   janela WPF do gerenciador
installer/Cloney.iss    script do Inno Setup
tools/make-app-icon.ps1 gera o ícone do app
```
