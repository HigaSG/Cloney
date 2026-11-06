using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Cloney.Native;

namespace Cloney.Core;

/// <summary>Extrai o ícone do app e gera o .ico do perfil (com cor e etiqueta opcionais).</summary>
internal static class IconBuilder
{
    private const int Size = 256;
    private static readonly int[] IcoSizes = [256, 64, 48, 32, 24, 16];
    public static readonly Color DefaultBadgeColor = Color.FromArgb(0xD9, 0x48, 0x0F);

    /// <summary>Carrega uma imagem de .exe/.dll (ícone embutido), .ico, .png, .jpg ou .bmp.</summary>
    public static Bitmap? LoadSource(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        path = Environment.ExpandEnvironmentVariables(path.Trim().Trim('"'));
        if (!File.Exists(path)) return null;

        try
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif":
                    using (var img = Image.FromFile(path)) return new Bitmap(img);
                default:
                    return ExtractIcon(path);
            }
        }
        catch
        {
            return null;
        }
    }

    private static Bitmap? ExtractIcon(string path)
    {
        var handles = new IntPtr[1];
        var ids = new int[1];
        if (NativeMethods.PrivateExtractIcons(path, 0, Size, Size, handles, ids, 1, 0) > 0 && handles[0] != IntPtr.Zero)
        {
            try { return HIconToBitmap(handles[0]); }
            finally { NativeMethods.DestroyIcon(handles[0]); }
        }
        using var assoc = Icon.ExtractAssociatedIcon(path);
        return assoc == null ? null : HIconToBitmap(assoc.Handle);
    }

    /// <summary>Converte um HICON em Bitmap preservando a transparência (Icon.ToBitmap nem sempre preserva).</summary>
    private static Bitmap? HIconToBitmap(IntPtr hIcon)
    {
        if (!NativeMethods.GetIconInfo(hIcon, out var info)) return null;
        try
        {
            if (info.hbmColor == IntPtr.Zero) return null;
            NativeMethods.GetObject(info.hbmColor, Marshal.SizeOf<NativeMethods.BITMAP>(), out var bm);
            int w = bm.bmWidth, h = bm.bmHeight;

            byte[] color = ReadBits(info.hbmColor, w, h);
            bool hasAlpha = false;
            for (int i = 3; i < color.Length; i += 4)
                if (color[i] != 0) { hasAlpha = true; break; }

            if (!hasAlpha && info.hbmMask != IntPtr.Zero)
            {
                // Ícone antigo sem canal alfa: a máscara diz o que é transparente (bit 1 = transparente).
                byte[] mask = ReadBits(info.hbmMask, w, h);
                for (int i = 0; i < color.Length; i += 4)
                    color[i + 3] = mask[i] == 0 ? (byte)255 : (byte)0;
            }

            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(color, y * w * 4, data.Scan0 + y * data.Stride, w * 4);
            }
            finally { bmp.UnlockBits(data); }
            return bmp;
        }
        finally
        {
            if (info.hbmColor != IntPtr.Zero) NativeMethods.DeleteObject(info.hbmColor);
            if (info.hbmMask != IntPtr.Zero) NativeMethods.DeleteObject(info.hbmMask);
        }
    }

    private static byte[] ReadBits(IntPtr hbm, int w, int h)
    {
        var bmi = new NativeMethods.BITMAPINFO
        {
            bmiHeader = new NativeMethods.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<NativeMethods.BITMAPINFOHEADER>(),
                biWidth = w,
                biHeight = -h, // de cima para baixo
                biPlanes = 1,
                biBitCount = 32,
            },
            bmiColors = new uint[256],
        };
        var buffer = new byte[w * h * 4];
        IntPtr hdc = NativeMethods.GetDC(IntPtr.Zero);
        try { NativeMethods.GetDIBits(hdc, hbm, 0, (uint)h, buffer, ref bmi, 0); }
        finally { NativeMethods.ReleaseDC(IntPtr.Zero, hdc); }
        return buffer;
    }

    public static Color? ParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 6 && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        return null;
    }

    /// <summary>Gera a imagem final 256x256 do perfil.</summary>
    public static Bitmap Compose(Bitmap? source, string? tintHex, string? badge, string profileName)
    {
        var canvas = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(canvas);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

        var tint = ParseColor(tintHex);
        var full = new Rectangle(0, 0, Size, Size);

        if (source != null)
        {
            if (tint is Color c)
            {
                using var attrs = new ImageAttributes();
                attrs.SetColorMatrix(TintMatrix(c, 0.6f));
                g.DrawImage(source, full, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
            }
            else
            {
                g.DrawImage(source, full);
            }
        }
        else
        {
            DrawPlaceholder(g, tint ?? Color.FromArgb(0x49, 0x50, 0x57), profileName);
        }

        if (!string.IsNullOrWhiteSpace(badge))
            DrawBadge(g, badge.Trim(), tint ?? DefaultBadgeColor);

        return canvas;
    }

    /// <summary>Mistura a cor original com a luminância pintada na cor escolhida.</summary>
    private static ColorMatrix TintMatrix(Color c, float s)
    {
        float r = c.R / 255f, gr = c.G / 255f, b = c.B / 255f;
        float k = 1 - s;
        return new ColorMatrix(new[]
        {
            new[] { k + s * 0.299f * r, s * 0.299f * gr,     s * 0.299f * b,     0f, 0f },
            new[] { s * 0.587f * r,     k + s * 0.587f * gr, s * 0.587f * b,     0f, 0f },
            new[] { s * 0.114f * r,     s * 0.114f * gr,     k + s * 0.114f * b, 0f, 0f },
            new[] { 0f, 0f, 0f, 1f, 0f },
            new[] { 0f, 0f, 0f, 0f, 1f },
        });
    }

    private static void DrawPlaceholder(Graphics g, Color color, string name)
    {
        using var path = RoundedRect(new RectangleF(16, 16, 224, 224), 48);
        using var fill = new SolidBrush(color);
        g.FillPath(fill, path);
        var letter = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim()[..1].ToUpperInvariant();
        using var font = new Font("Segoe UI", 130, FontStyle.Bold, GraphicsUnit.Pixel);
        using var text = new SolidBrush(ReadableOn(color));
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(letter, font, text, new RectangleF(16, 22, 224, 224), fmt);
    }

    private static void DrawBadge(Graphics g, string text, Color color)
    {
        if (text.Length > 3) text = text[..3];
        float fontPx = text.Length switch { 1 => 96, 2 => 80, _ => 64 };
        using var font = new Font("Segoe UI", fontPx, FontStyle.Bold, GraphicsUnit.Pixel);
        var measured = g.MeasureString(text, font);

        float height = 120;
        float width = Math.Max(height, measured.Width + 36);
        var rect = new RectangleF(Size - width - 2, Size - height - 2, width, height);

        using var path = RoundedRect(rect, height / 2);
        using var fill = new SolidBrush(color);
        using var border = new Pen(Color.White, 10);
        g.FillPath(fill, path);
        g.DrawPath(border, path);

        using var brush = new SolidBrush(ReadableOn(color));
        using var fmt = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString(text, font, brush, new RectangleF(rect.X, rect.Y + 4, rect.Width, rect.Height), fmt);
    }

    private static Color ReadableOn(Color bg) =>
        (0.299 * bg.R + 0.587 * bg.G + 0.114 * bg.B) / 255 > 0.7 ? Color.Black : Color.White;

    private static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    private static Bitmap Resize(Bitmap src, int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using var g = Graphics.FromImage(bmp);
        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawImage(src, new Rectangle(0, 0, size, size));
        return bmp;
    }

    /// <summary>Salva um .ico com vários tamanhos, todos comprimidos em PNG.</summary>
    public static void SaveIco(Bitmap image, string path)
    {
        var frames = IcoSizes.Select(size =>
        {
            using var bmp = Resize(image, size);
            // Todos os tamanhos em PNG: o Explorer não exibia atalhos com quadros BMP gerados aqui.
            return (size, data: PngBytes(bmp));
        }).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var fs = File.Create(path);
        using var w = new BinaryWriter(fs);
        w.Write((short)0);           // reservado
        w.Write((short)1);           // tipo: ícone
        w.Write((short)frames.Count);

        int offset = 6 + 16 * frames.Count;
        foreach (var (size, data) in frames)
        {
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);        // paleta
            w.Write((byte)0);        // reservado
            w.Write((short)1);       // planos
            w.Write((short)32);      // bits por pixel
            w.Write(data.Length);
            w.Write(offset);
            offset += data.Length;
        }
        foreach (var (_, data) in frames) w.Write(data);
    }

    private static byte[] PngBytes(Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, ImageFormat.Png);
        return ms.ToArray();
    }

    /// <summary>Gera o .ico do perfil num arquivo novo (evita cache de ícones do Explorer) e apaga os antigos.</summary>
    public static string BuildProfileIcon(Profile profile, string? resolvedExe)
    {
        var sourcePath = !string.IsNullOrWhiteSpace(profile.IconSource) ? profile.IconSource : resolvedExe;
        using var source = LoadSource(sourcePath);
        using var composed = Compose(source, profile.TintColor, profile.BadgeText, profile.Name);

        Directory.CreateDirectory(Paths.IconsDir);
        var file = Path.Combine(Paths.IconsDir, $"{profile.Id}-{DateTime.Now:yyyyMMddHHmmssfff}.ico");
        SaveIco(composed, file);

        foreach (var old in Directory.GetFiles(Paths.IconsDir, profile.Id + "-*.ico"))
        {
            if (string.Equals(old, file, StringComparison.OrdinalIgnoreCase)) continue;
            try { File.Delete(old); } catch { /* em uso por um launcher aberto; fica para a próxima */ }
        }
        return file;
    }
}
