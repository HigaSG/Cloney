using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Cloney.Native;

internal static class NativeMethods
{
    // ---------- Janelas ----------
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint flags, uint timeout, out IntPtr result);

    public const uint GW_OWNER = 4;
    public const int SW_RESTORE = 9;
    public const uint WM_CLOSE = 0x0010;
    public const uint WM_SETICON = 0x0080;
    public const uint WM_GETICON = 0x007F;
    public const int ICON_SMALL = 0;
    public const int ICON_BIG = 1;
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    public static List<IntPtr> TopLevelWindows()
    {
        var list = new List<IntPtr>();
        EnumWindows((h, _) => { list.Add(h); return true; }, IntPtr.Zero);
        return list;
    }

    // ---------- Ícones ----------
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int PrivateExtractIcons(string szFileName, int nIconIndex, int cxIcon, int cyIcon,
        IntPtr[] phicon, int[] piconid, int nIcons, int flags);

    [DllImport("user32.dll")]
    public static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

    public const uint IMAGE_ICON = 1;
    public const uint LR_LOADFROMFILE = 0x0010;

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);
    public const int SM_CXICON = 11;
    public const int SM_CXSMICON = 49;

    [StructLayout(LayoutKind.Sequential)]
    public struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [DllImport("user32.dll")]
    public static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public int bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct BITMAPINFO
    {
        public BITMAPINFOHEADER bmiHeader;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public uint[] bmiColors;
    }

    [DllImport("gdi32.dll")]
    public static extern int GetObject(IntPtr h, int c, out BITMAP pv);

    [DllImport("gdi32.dll")]
    public static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[]? lpvBits,
        ref BITMAPINFO lpbmi, uint usage);

    [DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr ho);

    [DllImport("user32.dll")]
    public static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    // ---------- Processos ----------
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr CreateToolhelp32Snapshot(uint dwFlags, uint th32ProcessID);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool Process32FirstW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern bool Process32NextW(IntPtr hSnapshot, ref PROCESSENTRY32W lppe);

    [DllImport("kernel32.dll")]
    public static extern bool CloseHandle(IntPtr hObject);

    public const uint TH32CS_SNAPPROCESS = 0x00000002;

    /// <summary>Retorna o mapa PID -> PID do processo pai de todos os processos em execução.</summary>
    public static Dictionary<int, int> ProcessParents()
    {
        var map = new Dictionary<int, int>();
        IntPtr snap = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return map;
        try
        {
            var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
            if (Process32FirstW(snap, ref e))
            {
                do { map[(int)e.th32ProcessID] = (int)e.th32ParentProcessID; }
                while (Process32NextW(snap, ref e));
            }
        }
        finally { CloseHandle(snap); }
        return map;
    }
}
