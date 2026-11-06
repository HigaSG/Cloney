using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Cloney.Core;
using Microsoft.Win32;

namespace Cloney.UI;

public partial class MainWindow : Window
{
    private static readonly string[] SwatchColors =
        ["#D9480F", "#F08C00", "#2F9E44", "#0CA678", "#1971C2", "#5F3DC4", "#C2255C", "#495057"];

    private sealed class ProfileItem
    {
        public required Profile Profile { get; init; }
        public string Name => Profile.Name;
        public string PresetName => Presets.Get(Profile.PresetId).Name;
        public ImageSource? Icon { get; init; }
    }

    private List<Profile> _profiles = new();
    private Profile? _current;
    private bool _loading;
    private string? _autoExe;
    private string? _autoName;
    private readonly Dictionary<string, System.Drawing.Bitmap?> _sourceCache = new(StringComparer.OrdinalIgnoreCase);

    public MainWindow()
    {
        InitializeComponent();
        PresetCombo.ItemsSource = Presets.All;
        BuildSwatches();
        Reload(null);
        if (_profiles.Count == 0) StartNew();
    }

    private Preset SelectedPreset => PresetCombo.SelectedItem as Preset ?? Presets.Get(null);

    // ---------------- Lista ----------------

    private void Reload(string? selectId)
    {
        _profiles = ProfileStore.Load();
        var items = _profiles.Select(p => new ProfileItem { Profile = p, Icon = LoadIco(p.IconFile) }).ToList();
        ProfileList.ItemsSource = items;
        var selected = items.FirstOrDefault(i => i.Profile.Id == selectId) ?? items.FirstOrDefault();
        ProfileList.SelectedItem = selected;
        if (selected != null) Show(selected.Profile.Clone());
    }

    private void ProfileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProfileList.SelectedItem is ProfileItem item && item.Profile.Id != _current?.Id)
            Show(item.Profile.Clone());
    }

    private void ProfileList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ProfileList.SelectedItem is ProfileItem item) Launch(item.Profile.Id);
    }

    private void NewButton_Click(object sender, RoutedEventArgs e) => StartNew();

    private void StartNew()
    {
        ProfileList.SelectedItem = null;
        _autoExe = _autoName = null;
        Show(new Profile { PresetId = "claude" });
        ApplyPresetDefaults(SelectedPreset);
        NameBox.Focus();
        NameBox.SelectAll();
    }

    // ---------------- Formulário ----------------

    private void Show(Profile p)
    {
        _current = p;
        _loading = true;
        NameBox.Text = p.Name;
        PresetCombo.SelectedItem = Presets.Get(p.PresetId);
        ExeBox.Text = p.ExePath ?? "";
        DataBox.Text = p.DataDir ?? "";
        ArgsBox.Text = p.ExtraArgs;
        EnvBox.Text = string.Join(Environment.NewLine, p.Env.Select(kv => $"{kv.Key}={kv.Value}"));
        WorkDirBox.Text = p.WorkingDir ?? "";
        TrayCheck.IsChecked = p.ShowTrayIcon;
        RestartCheck.IsChecked = p.RestartOnExit;
        WipeCheck.IsChecked = p.WipeDataOnExit;
        DesktopCheck.IsChecked = p.DesktopShortcut;
        IconBox.Text = p.IconSource ?? "";
        BadgeBox.Text = p.BadgeText ?? "";
        ColorBox.Text = p.TintColor ?? "";
        _loading = false;

        StatusText.Text = "";
        RefreshAll();
    }

    private bool IsSaved => _current != null && _profiles.Any(p => p.Id == _current.Id);

    /// <summary>Perfil com os valores atuais do formulário (sem salvar).</summary>
    private Profile FormProfile()
    {
        var p = (_current ?? new Profile()).Clone();
        var preset = SelectedPreset;
        p.Name = NameBox.Text.Trim();
        p.PresetId = preset.Id;
        p.ExePath = preset.IsMsix ? null : Blank(ExeBox.Text);
        p.DataDir = Blank(DataBox.Text);
        p.ExtraArgs = ArgsBox.Text.Trim();
        p.Env = ParseEnv(EnvBox.Text);
        p.WorkingDir = Blank(WorkDirBox.Text);
        p.ShowTrayIcon = TrayCheck.IsChecked == true;
        p.RestartOnExit = RestartCheck.IsChecked == true;
        p.WipeDataOnExit = WipeCheck.IsChecked == true && WipeCheck.IsEnabled;
        p.DesktopShortcut = DesktopCheck.IsChecked == true;
        p.IconSource = Blank(IconBox.Text);
        p.BadgeText = Blank(BadgeBox.Text);
        p.TintColor = Blank(ColorBox.Text);
        return p;
    }

    private static string? Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static Dictionary<string, string> ParseEnv(string text)
    {
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in text.Split('\n'))
        {
            var idx = line.IndexOf('=');
            if (idx <= 0) continue;
            var key = line[..idx].Trim();
            if (key.Length > 0) env[key] = line[(idx + 1)..].Trim();
        }
        return env;
    }

    private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || _current == null) return;
        ApplyPresetDefaults(SelectedPreset);
    }

    /// <summary>Preenche executável e nome sugeridos, sem apagar o que o usuário digitou.</summary>
    private void ApplyPresetDefaults(Preset preset)
    {
        _loading = true;
        if (!preset.IsMsix && (string.IsNullOrWhiteSpace(ExeBox.Text) || ExeBox.Text == _autoExe))
        {
            _autoExe = AppResolver.FindPresetExe(preset) ?? "";
            ExeBox.Text = _autoExe;
        }
        if (string.IsNullOrWhiteSpace(NameBox.Text) || NameBox.Text == _autoName)
        {
            _autoName = SuggestName(preset);
            NameBox.Text = _autoName;
        }
        _loading = false;
        RefreshAll();
    }

    private string SuggestName(Preset preset)
    {
        if (preset.Id is "generic" or "home" or "electron") return "";
        int n = _profiles.Count(p => p.PresetId == preset.Id) + 2;
        return $"{preset.Name} {n}";
    }

    private void LookChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _current == null) return;
        RefreshAll();
    }

    private void DataBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading || _current == null) return;
        UpdateDataHint();
    }

    private void RefreshAll()
    {
        UpdatePresetUi();
        UpdateDataHint();
        UpdatePreview();
    }

    private void UpdatePresetUi()
    {
        var preset = SelectedPreset;
        var p = FormProfile();

        HeaderText.Text = string.IsNullOrWhiteSpace(p.Name) ? "Novo perfil" : p.Name;
        HeaderSub.Text = IsSaved ? preset.Name : preset.Name + " · ainda não salvo";

        NoteText.Text = preset.Note ?? "";
        NoteBorder.Visibility = preset.Note == null ? Visibility.Collapsed : Visibility.Visible;

        ExeBox.IsEnabled = BrowseExeButton.IsEnabled = !preset.IsMsix;
        var exe = AppResolver.ResolveExe(p);
        string hint;
        if (preset.IsMsix)
        {
            if (!string.IsNullOrEmpty(ExeBox.Text)) { _loading = true; ExeBox.Text = ""; _loading = false; }
            hint = exe != null
                ? "Localizado automaticamente (app MSIX/Microsoft Store): " + exe
                : "Não encontrei este app instalado para o seu usuário.";
        }
        else if (string.IsNullOrWhiteSpace(ExeBox.Text))
        {
            hint = exe != null ? "Detectado: " + exe : "Escolha o .exe do aplicativo.";
        }
        else
        {
            hint = exe != null ? "" : "Arquivo não encontrado.";
        }
        if (!string.IsNullOrEmpty(preset.ArgsTemplate))
            hint += (hint.Length > 0 ? "\n" : "") + "Argumentos automáticos: " + preset.ArgsTemplate;
        ExeHint.Text = hint;
        ExeHint.Visibility = hint.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateDataHint()
    {
        var p = FormProfile();
        string effective;
        try { effective = p.EffectiveDataDir; }
        catch { DataHint.Text = "Caminho inválido."; return; }

        DataHint.Text = string.IsNullOrWhiteSpace(DataBox.Text) ? "Vazio = pasta padrão: " + effective : "";
        DataHint.Visibility = DataHint.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        bool canWipe = Paths.IsInsideDataRoot(effective);
        WipeCheck.IsEnabled = canWipe;
        WipeCheck.ToolTip = canWipe ? null : "Por segurança, só funciona com a pasta de dados padrão do Cloney.";
    }

    private void UpdatePreview()
    {
        var p = FormProfile();
        var source = p.IconSource ?? AppResolver.ResolveExe(p);
        var bmp = source == null ? null : CachedSource(source);
        using var composed = IconBuilder.Compose(bmp, p.TintColor, p.BadgeText, p.Name);
        IconPreview.Source = ToImageSource(composed);
    }

    private System.Drawing.Bitmap? CachedSource(string path)
    {
        if (!_sourceCache.TryGetValue(path, out var bmp))
            _sourceCache[path] = bmp = IconBuilder.LoadSource(path);
        return bmp;
    }

    private void BuildSwatches()
    {
        foreach (var hex in SwatchColors)
        {
            var b = new Button
            {
                Width = 20, Height = 20, Margin = new Thickness(2), Padding = new Thickness(0),
                Background = (Brush)new BrushConverter().ConvertFromString(hex)!,
                BorderBrush = Brushes.White, ToolTip = hex,
            };
            b.Click += (_, _) => ColorBox.Text = hex;
            Swatches.Children.Add(b);
        }
        var clear = new Button { Content = "✕", Width = 22, Height = 20, Margin = new Thickness(4, 2, 2, 2), Padding = new Thickness(0), ToolTip = "Sem cor" };
        clear.Click += (_, _) => ColorBox.Text = "";
        Swatches.Children.Add(clear);
    }

    // ---------------- Salvar / abrir / excluir ----------------

    private bool Save()
    {
        var p = FormProfile();
        if (string.IsNullOrWhiteSpace(p.Name)) return Fail("Dê um nome ao perfil.");
        if (p.TintColor != null && IconBuilder.ParseColor(p.TintColor) == null)
            return Fail("Cor inválida. Use o formato #RRGGBB, por exemplo #1971C2.");

        var exe = AppResolver.ResolveExe(p);
        if (exe == null) return Fail("Não encontrei o executável. Verifique se o app está instalado ou escolha o .exe.");

        try
        {
            p.IconFile = IconBuilder.BuildProfileIcon(p, exe);
            Shortcuts.Sync(p);

            var list = ProfileStore.Load();
            var idx = list.FindIndex(x => x.Id == p.Id);
            if (idx >= 0) list[idx] = p; else list.Add(p);
            ProfileStore.Save(list);

            _current = null; // força Reload a mostrar a versão salva
            Reload(p.Id);
            StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0x2F, 0x7A, 0x3E));
            StatusText.Text = "Salvo. Atalho criado em Menu Iniciar › Cloney Apps" +
                              (p.DesktopShortcut ? " e na Área de Trabalho" : "") +
                              ". Se a instância já estiver aberta, as mudanças valem quando ela abrir de novo.";
            return true;
        }
        catch (Exception ex)
        {
            return Fail("Erro ao salvar: " + ex.Message);
        }
    }

    private bool Fail(string message)
    {
        StatusText.Foreground = new SolidColorBrush(Color.FromRgb(0xC9, 0x2A, 0x2A));
        StatusText.Text = message;
        return false;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => Save();

    private void SaveAndLaunch_Click(object sender, RoutedEventArgs e)
    {
        if (Save() && _current != null) Launch(_current.Id);
    }

    private static void Launch(string profileId)
    {
        try
        {
            Process.Start(new ProcessStartInfo(Paths.SelfExe, "--launch " + profileId) { UseShellExecute = false })?.Dispose();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Cloney", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_current == null) return;
        var stored = _profiles.FirstOrDefault(p => p.Id == _current.Id);
        if (stored == null) { _current = null; Reload(null); if (_profiles.Count == 0) StartNew(); return; }

        if (MessageBox.Show($"Excluir o perfil \"{stored.Name}\"? Os atalhos dele serão removidos.", "Cloney",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var data = stored.EffectiveDataDir;
        bool deleteData = false;
        if (Directory.Exists(data))
        {
            if (Paths.IsInsideDataRoot(data))
                deleteData = MessageBox.Show(
                    $"Apagar também os dados deste perfil (contas logadas, histórico, configurações)?\n\n{data}",
                    "Cloney", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
            else
                MessageBox.Show($"A pasta de dados personalizada não será apagada:\n\n{data}", "Cloney",
                    MessageBoxButton.OK, MessageBoxImage.Information);
        }

        try
        {
            Shortcuts.Remove(stored);
            if (Directory.Exists(Paths.IconsDir))
                foreach (var ico in Directory.GetFiles(Paths.IconsDir, stored.Id + "-*.ico"))
                    try { File.Delete(ico); } catch { /* em uso */ }

            var list = ProfileStore.Load();
            list.RemoveAll(p => p.Id == stored.Id);
            ProfileStore.Save(list);

            if (deleteData) Directory.Delete(data, recursive: true);
        }
        catch (Exception ex)
        {
            MessageBox.Show("O perfil foi excluído, mas algo não pôde ser apagado (a instância está aberta?):\n" + ex.Message,
                "Cloney", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        _current = null;
        Reload(null);
        if (_profiles.Count == 0) StartNew();
    }

    // ---------------- Procurar ----------------

    private void BrowseExe_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Programas (*.exe)|*.exe|Todos os arquivos (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        ExeBox.Text = dlg.FileName;
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            var info = FileVersionInfo.GetVersionInfo(dlg.FileName);
            NameBox.Text = (string.IsNullOrWhiteSpace(info.ProductName)
                ? Path.GetFileNameWithoutExtension(dlg.FileName)
                : info.ProductName.Trim()) + " 2";
        }
    }

    private void BrowseData_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Pasta de dados do perfil" };
        if (dlg.ShowDialog(this) == true) DataBox.Text = dlg.FolderName;
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Ícones e imagens|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.exe;*.dll|Todos os arquivos (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) == true) IconBox.Text = dlg.FileName;
    }

    private void OpenData_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dir = FormProfile().EffectiveDataDir;
            Directory.CreateDirectory(dir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex)
        {
            Fail(ex.Message);
        }
    }

    // ---------------- Imagens ----------------

    private static ImageSource? LoadIco(string? path)
    {
        if (path == null || !File.Exists(path)) return null;
        try
        {
            var img = new BitmapImage();
            img.BeginInit();
            img.UriSource = new Uri(path);
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            img.EndInit();
            img.Freeze();
            return img;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource ToImageSource(System.Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        ms.Position = 0;
        var img = new BitmapImage();
        img.BeginInit();
        img.StreamSource = ms;
        img.CacheOption = BitmapCacheOption.OnLoad;
        img.EndInit();
        img.Freeze();
        return img;
    }
}
