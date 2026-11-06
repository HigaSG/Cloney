using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Cloney.Core;
using Cloney.Native;

namespace Cloney.Launcher;

/// <summary>
/// Fica vivo enquanto o app do perfil roda: acompanha a árvore de processos, dá às janelas
/// a identidade do perfil na barra de tarefas, mostra o ícone na bandeja e cuida de
/// reiniciar/limpar quando o app fecha.
/// </summary>
internal sealed class InstanceContext : ApplicationContext
{
    private static readonly TimeSpan ForgetDeadAfter = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan QuickExit = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan KillAfter = TimeSpan.FromSeconds(6);

    private readonly Profile _profile;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 500 };
    private readonly Dictionary<int, DateTime> _tracked = new();   // PID -> última vez visto vivo
    private readonly HashSet<IntPtr> _tagged = new();
    private readonly string _relaunchCommand;
    private readonly string? _iconResource;

    private NotifyIcon? _tray;
    private Icon? _trayIcon;
    private IntPtr _bigIcon, _smallIcon;
    private DateTime _startedAt;
    private int _quickExits;
    private bool _quitting;
    private DateTime? _killAt;

    public InstanceContext(Profile profile)
    {
        _profile = profile;
        _relaunchCommand = $"\"{Paths.SelfExe}\" --launch {profile.Id}";
        _iconResource = profile.IconFile is { } ico && File.Exists(ico) ? ico : null;
        _timer.Tick += (_, _) => Tick();
        LoadIcons();
    }

    public bool Start(ProcessStartInfo psi)
    {
        if (!StartTarget(psi)) return false;
        if (_profile.ShowTrayIcon) CreateTray();
        _timer.Start();
        return true;
    }

    private bool StartTarget(ProcessStartInfo psi)
    {
        try
        {
            using var process = Process.Start(psi);
            if (process == null) return false;
            _tracked[process.Id] = DateTime.UtcNow;
            _startedAt = DateTime.UtcNow;
            return true;
        }
        catch (Exception ex)
        {
            LauncherHost.ShowError("Não foi possível abrir o app:\n" + ex.Message);
            return false;
        }
    }

    // ---------------- Acompanhamento ----------------

    private void Tick()
    {
        var now = DateTime.UtcNow;
        var parents = NativeMethods.ProcessParents();

        // Atualiza os vivos e adota filhos/netos (inclusive de processos que já terminaram há pouco).
        foreach (var pid in _tracked.Keys.ToList())
            if (parents.ContainsKey(pid)) _tracked[pid] = now;

        bool added;
        do
        {
            added = false;
            foreach (var (pid, ppid) in parents)
            {
                if (!_tracked.ContainsKey(pid) && _tracked.ContainsKey(ppid))
                {
                    _tracked[pid] = now;
                    added = true;
                }
            }
        } while (added);

        // Esquece PIDs mortos há algum tempo (evita confundir com PIDs reaproveitados pelo Windows).
        foreach (var (pid, seen) in _tracked.ToList())
            if (now - seen > ForgetDeadAfter) _tracked.Remove(pid);

        var alive = _tracked.Keys.Where(parents.ContainsKey).ToHashSet();

        if (alive.Count > 0)
        {
            TagWindows(alive);
            if (_killAt is { } killAt && now >= killAt) KillAll(alive);
            return;
        }

        OnAppExited();
    }

    private void TagWindows(HashSet<int> alive)
    {
        _tagged.RemoveWhere(h => !NativeMethods.IsWindow(h));

        foreach (var hwnd in NativeMethods.TopLevelWindows())
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
            if (!alive.Contains((int)pid)) continue;
            if (!NativeMethods.IsWindowVisible(hwnd)) continue;
            if (NativeMethods.GetWindow(hwnd, NativeMethods.GW_OWNER) != IntPtr.Zero) continue;

            if (_tagged.Add(hwnd))
                AppUserModel.ApplyToWindow(hwnd, _profile.AppUserModelId, _relaunchCommand, _profile.Name, _iconResource);

            if (_profile.HasCustomLook) EnsureWindowIcon(hwnd);
        }
    }

    /// <summary>Coloca o ícone do perfil na janela (e recoloca se o app trocar).</summary>
    private void EnsureWindowIcon(IntPtr hwnd)
    {
        if (_bigIcon == IntPtr.Zero) return;
        NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_GETICON, (IntPtr)NativeMethods.ICON_BIG, IntPtr.Zero,
            NativeMethods.SMTO_ABORTIFHUNG, 200, out var current);
        if (current == _bigIcon) return;

        NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_SETICON, (IntPtr)NativeMethods.ICON_BIG, _bigIcon,
            NativeMethods.SMTO_ABORTIFHUNG, 200, out _);
        NativeMethods.SendMessageTimeout(hwnd, NativeMethods.WM_SETICON, (IntPtr)NativeMethods.ICON_SMALL, _smallIcon,
            NativeMethods.SMTO_ABORTIFHUNG, 200, out _);
    }

    private void OnAppExited()
    {
        _timer.Stop();
        _tagged.Clear();
        _killAt = null;

        if (_profile.WipeDataOnExit) WipeData();

        if (!_quitting && _profile.RestartOnExit)
        {
            _quickExits = DateTime.UtcNow - _startedAt < QuickExit ? _quickExits + 1 : 0;
            if (_quickExits < 3)
            {
                try
                {
                    // Monta de novo: o app pode ter sido atualizado e mudado de pasta (MSIX).
                    if (StartTarget(LaunchBuilder.Build(_profile)))
                    {
                        _timer.Start();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LauncherHost.ShowError(ex.Message);
                }
            }
            else
            {
                _tray?.ShowBalloonTip(5000, _profile.Name,
                    "O app fechou 3 vezes seguidas logo após abrir. Reinício automático interrompido.",
                    ToolTipIcon.Warning);
            }
        }

        ExitThread();
    }

    private void WipeData()
    {
        var data = _profile.EffectiveDataDir;
        if (!Paths.IsInsideDataRoot(data)) return; // por segurança, só apaga dentro da pasta padrão do Cloney
        for (int attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (Directory.Exists(data)) Directory.Delete(data, recursive: true);
                return;
            }
            catch (IOException) { System.Threading.Thread.Sleep(400); }
            catch (UnauthorizedAccessException) { System.Threading.Thread.Sleep(400); }
        }
    }

    // ---------------- Ações ----------------

    private void OpenOrFocus()
    {
        var visible = _tagged.FirstOrDefault(h => NativeMethods.IsWindow(h) && NativeMethods.IsWindowVisible(h));
        if (visible != IntPtr.Zero)
        {
            if (NativeMethods.IsIconic(visible)) NativeMethods.ShowWindow(visible, NativeMethods.SW_RESTORE);
            NativeMethods.SetForegroundWindow(visible);
            return;
        }
        NewWindow(); // ex.: Claude escondido na bandeja -> a segunda abertura mostra a janela
    }

    private void NewWindow()
    {
        try { Process.Start(LaunchBuilder.Build(_profile))?.Dispose(); }
        catch (Exception ex) { LauncherHost.ShowError(ex.Message); }
    }

    private void OpenDataFolder()
    {
        var dir = _profile.EffectiveDataDir;
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true })?.Dispose();
    }

    private void Quit()
    {
        _quitting = true;
        foreach (var hwnd in _tagged.Where(NativeMethods.IsWindow))
            NativeMethods.PostMessage(hwnd, NativeMethods.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
        // Apps que só se escondem na bandeja ao fechar (como o Claude) são encerrados depois de alguns segundos.
        _killAt = DateTime.UtcNow + KillAfter;
    }

    private static void KillAll(IEnumerable<int> pids)
    {
        foreach (var pid in pids)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.Kill();
            }
            catch { /* já fechou ou sem permissão */ }
        }
    }

    // ---------------- Bandeja e ícones ----------------

    private void LoadIcons()
    {
        var ico = _profile.IconFile;
        if (ico == null || !File.Exists(ico)) return;

        int big = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXICON);
        int small = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSMICON);
        _bigIcon = NativeMethods.LoadImage(IntPtr.Zero, ico, NativeMethods.IMAGE_ICON, big, big, NativeMethods.LR_LOADFROMFILE);
        _smallIcon = NativeMethods.LoadImage(IntPtr.Zero, ico, NativeMethods.IMAGE_ICON, small, small, NativeMethods.LR_LOADFROMFILE);
        try { _trayIcon = new Icon(ico, SystemInformation.SmallIconSize); } catch { _trayIcon = null; }
    }

    private void CreateTray()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem(_profile.Name) { Enabled = false, Font = new Font(menu.Font, FontStyle.Bold) });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Mostrar", null, (_, _) => OpenOrFocus());
        menu.Items.Add("Nova janela", null, (_, _) => NewWindow());
        menu.Items.Add("Abrir pasta de dados", null, (_, _) => OpenDataFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Encerrar esta instância", null, (_, _) => Quit());

        _tray = new NotifyIcon
        {
            Icon = _trayIcon ?? SystemIcons.Application,
            Text = Truncate("Cloney: " + _profile.Name, 63),
            ContextMenuStrip = menu,
            Visible = true,
        };
        _tray.MouseClick += (_, e) => { if (e.Button == MouseButtons.Left) OpenOrFocus(); };
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.ContextMenuStrip?.Dispose();
                _tray.Dispose();
            }
            _trayIcon?.Dispose();
        }
        if (_bigIcon != IntPtr.Zero) NativeMethods.DestroyIcon(_bigIcon);
        if (_smallIcon != IntPtr.Zero) NativeMethods.DestroyIcon(_smallIcon);
        _bigIcon = _smallIcon = IntPtr.Zero;
        base.Dispose(disposing);
    }
}
