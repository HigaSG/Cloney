# Changelog

## v0.1.0 — 2026-11-06 (primeira versão)

Primeira versão funcional do Cloney: lançador de múltiplas instâncias para Windows, para uso pessoal.

### Incluído
- **Gerenciador (WPF):** cria, edita e remove perfis; cada perfil tem nome, ícone (cor/etiqueta) e pasta de dados própria.
- **Launcher (`Cloney.exe --launch <perfil>`):**
  - monta o comando do app com a pasta de dados do perfil (`--user-data-dir`, `-profile`, etc.);
  - aplica um **AppUserModelID** próprio às janelas, separando o botão da barra de tarefas;
  - aplica o ícone do perfil às janelas;
  - ícone na bandeja: Mostrar, Nova janela, Abrir pasta de dados, Encerrar;
  - opções de reabrir o app quando ele fecha e de apagar os dados ao fechar (sessão descartável).
- **Atalhos** em Menu Iniciar › Cloney Apps (opcionalmente na Área de Trabalho).
- **Apps MSIX** (ex.: Claude Desktop) localizados dinamicamente a cada abertura.
- **Presets:** Claude Desktop, Chrome, Edge, Brave, Vivaldi, Firefox, VS Code, Cursor, Windsurf,
  Electron genérico, redirecionamento de APPDATA/HOME e genérico.
- **Instalador** Inno Setup por usuário (sem UAC), em `%LOCALAPPDATA%\Programs\Cloney`.

### Testado
- Claude Desktop rodando duas contas logadas ao mesmo tempo (instância principal + clone).

### Limitações conhecidas
- Claude Desktop: o link de login do navegador abre sempre a instância principal. No clone, entre
  com **código** e depois feche e abra o clone de novo.
- Sem assinatura digital: o SmartScreen avisa na primeira execução (**Mais informações › Executar assim mesmo**).

### Próximos passos planejados
- Web Apps (WebView2).
- Mais presets.
- Quebra de mutex de instância única (opcional).
