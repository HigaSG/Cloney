using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

namespace Cloney.Native;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct PropertyKey
{
    public Guid FormatId;
    public uint PropertyId;

    public PropertyKey(Guid formatId, uint propertyId)
    {
        FormatId = formatId;
        PropertyId = propertyId;
    }
}

/// <summary>PROPVARIANT mínimo: só strings (VT_LPWSTR) e booleanos (VT_BOOL).</summary>
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PropVariant
{
    [FieldOffset(0)] public ushort VarType;
    [FieldOffset(8)] public IntPtr Pointer;
    [FieldOffset(8)] public short BoolValue;

    public static PropVariant FromString(string value) =>
        new() { VarType = 31 /* VT_LPWSTR */, Pointer = Marshal.StringToCoTaskMemUni(value) };

    public static PropVariant FromBool(bool value) =>
        new() { VarType = 11 /* VT_BOOL */, BoolValue = (short)(value ? -1 : 0) };

    public void Clear() => PropVariantClear(ref this);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);
}

[ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint cProps);
    [PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
    [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
    [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
    [PreserveSig] int Commit();
}

[ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cch, IntPtr pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cch, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
internal class CShellLink
{
}

internal static class AppUserModel
{
    private static readonly Guid FormatId = new("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");
    public static PropertyKey RelaunchCommand => new(FormatId, 2);
    public static PropertyKey RelaunchIconResource => new(FormatId, 3);
    public static PropertyKey RelaunchDisplayNameResource => new(FormatId, 4);
    public static PropertyKey Id => new(FormatId, 5);

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(IntPtr hwnd, ref Guid riid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore propertyStore);

    private static void Set(IPropertyStore store, PropertyKey key, string value)
    {
        var pv = PropVariant.FromString(value);
        try { Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref pv)); }
        finally { pv.Clear(); }
    }

    /// <summary>
    /// Dá à janela (de qualquer processo) uma identidade própria na barra de tarefas:
    /// agrupamento separado e "fixar" apontando para o nosso atalho.
    /// </summary>
    public static bool ApplyToWindow(IntPtr hwnd, string appId, string relaunchCommand,
        string displayName, string? iconResource)
    {
        Guid iid = typeof(IPropertyStore).GUID;
        if (SHGetPropertyStoreForWindow(hwnd, ref iid, out IPropertyStore store) < 0 || store == null)
            return false;
        try
        {
            // As propriedades de "relaunch" precisam estar definidas antes do ID.
            Set(store, RelaunchCommand, relaunchCommand);
            Set(store, RelaunchDisplayNameResource, displayName);
            if (!string.IsNullOrEmpty(iconResource))
                Set(store, RelaunchIconResource, iconResource);
            Set(store, Id, appId);
            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            Marshal.ReleaseComObject(store);
        }
    }
}

internal static class ShellLink
{
    public static void Create(string lnkPath, string target, string arguments, string workingDir,
        string iconPath, string description, string appUserModelId)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            link.SetPath(target);
            link.SetArguments(arguments);
            link.SetWorkingDirectory(workingDir);
            link.SetIconLocation(iconPath, 0);
            link.SetDescription(description);

            var store = (IPropertyStore)link;
            var key = AppUserModel.Id;
            var pv = PropVariant.FromString(appUserModelId);
            try
            {
                Marshal.ThrowExceptionForHR(store.SetValue(ref key, ref pv));
                Marshal.ThrowExceptionForHR(store.Commit());
            }
            finally { pv.Clear(); }

            ((IPersistFile)link).Save(lnkPath, true);
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }

    /// <summary>Lê os argumentos de um .lnk (para confirmar que o atalho é nosso antes de apagar).</summary>
    public static string? ReadArguments(string lnkPath)
    {
        var link = (IShellLinkW)new CShellLink();
        try
        {
            ((IPersistFile)link).Load(lnkPath, 0);
            var sb = new StringBuilder(1024);
            link.GetArguments(sb, sb.Capacity);
            return sb.ToString();
        }
        catch
        {
            return null;
        }
        finally
        {
            Marshal.ReleaseComObject(link);
        }
    }
}
