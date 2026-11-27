using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

using Microsoft.Win32.SafeHandles;

namespace AuroraAudioStudio.Services;

// Older Aurora clients explicitly elevated Setup. Inno's runasoriginaluser
// cannot undo that. Handle its restart before reading settings or taking the
// single-instance mutex, and never start an elevated model workspace on failure.
public static class UpdateRelaunch
{
    public const string Argument = "--after-update";

    public static void HandleBeforeStartup()
    {
        var args = Environment.GetCommandLineArgs();
        var dispatched = args.Contains("--after-update-desktop", StringComparer.Ordinal);
        if (!dispatched && !args.Contains(Argument, StringComparer.Ordinal)) return;
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008, out var current)) throw new Win32Exception();
            using (current) if (!IsElevated(current)) return;
            if (dispatched) throw new IOException("Explorer did not start an ordinary-user process.");
            StartAsDesktopUser(Environment.ProcessPath ?? throw new IOException("Application path is unavailable."));
            Environment.Exit(0);
        }
        catch (Exception ex)
        {
            // Do not load settings under the administrator's profile just to localize this recovery prompt.
            MessageBox(IntPtr.Zero,
                "Aurora 已更新，但无法以桌面用户身份自动重启。请从开始菜单重新打开 Aurora。\n\nAurora was updated but could not restart as the desktop user. Open it again from the Start menu.\n\n" + ex.Message,
                "Aurora Audio Studio", 0x10);
            Environment.Exit(1);
        }
    }

    internal static bool IsElevated(SafeAccessTokenHandle token)
    {
        if (!GetTokenInformation(token, 20, out var elevation, sizeof(int), out _)) throw new Win32Exception();
        return elevation != 0;
    }

    internal static void StartAsDesktopUser(string executable)
    {
        var shell = GetShellWindow();
        if (shell == IntPtr.Zero) throw new IOException("The interactive desktop is not available.");
        GetWindowThreadProcessId(shell, out var shellPid);
        using var shellProcess = Process.GetProcessById(checked((int)shellPid));
        if (shellProcess.SessionId != Process.GetCurrentProcess().SessionId) throw new IOException("Desktop session mismatch.");
        using var process = OpenProcess(0x1000, false, shellPid);
        if (process.IsInvalid || !OpenProcessToken(process.DangerousGetHandle(), 0x0008, out var shellToken)) throw new Win32Exception();
        using (shellToken) if (IsElevated(shellToken)) throw new IOException("The desktop does not have an ordinary-user token.");

        // Microsoft's ExecInExplorer pattern: the desktop's own ShellDispatch opens
        // this executable. No user-token duplication, profile loading, or privilege adjustment.
        var cls = new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39");
        var iid = typeof(IShellWindows).GUID;
        Marshal.ThrowExceptionForHR(CoCreateInstance(ref cls, IntPtr.Zero, 4, ref iid, out var windows));
        object? location = null, root = null;
        if (windows.FindWindowSW(ref location, ref root, 8, out _, 1, out var desktop) != 0 || desktop is null)
            throw new IOException("Explorer desktop automation is unavailable.");
        var service = new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837");
        var browserId = typeof(IShellBrowser).GUID;
        ((IServices)desktop).QueryService(ref service, ref browserId, out var browser);
        browser.QueryActiveShellView(out var view);
        var dispatchId = new Guid("00020400-0000-0000-C000-000000000046");
        view.GetItemObject(0, ref dispatchId, out var background);
        dynamic shellApp = ((dynamic)background).Application;
        shellApp.ShellExecute(executable, "--after-update-desktop", Path.GetDirectoryName(executable)!, "open", 1);
    }

    // Unused slots retain the order of the Windows SDK interfaces.
    [ComImport, Guid("85CB6900-4D95-11CF-960C-0080C7F4EE85"), InterfaceType(ComInterfaceType.InterfaceIsDual)]
    private interface IShellWindows
    {
        void Count(); void Item(); void NewEnum(); void Register(); void RegisterPending(); void Revoke(); void OnNavigate(); void OnActivated();
        [PreserveSig] int FindWindowSW([In, MarshalAs(UnmanagedType.Struct)] ref object? location, [In, MarshalAs(UnmanagedType.Struct)] ref object? root,
            int windowClass, out int hwnd, int options, [MarshalAs(UnmanagedType.IDispatch)] out object? desktop);
    }
    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IServices { void QueryService(ref Guid sid, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellBrowser browser); }
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellBrowser
    {
        void GetWindow(); void ContextSensitiveHelp(); void InsertMenus(); void SetMenu(); void RemoveMenus(); void SetStatusText(); void EnableModeless();
        void TranslateAccelerator(); void BrowseObject(); void GetViewStateStream(); void GetControlWindow(); void SendControlMsg();
        void QueryActiveShellView([MarshalAs(UnmanagedType.Interface)] out IShellView view);
    }
    [ComImport, Guid("000214E3-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellView
    {
        void GetWindow(); void ContextSensitiveHelp(); void TranslateAccelerator(); void EnableModeless(); void UIActivate(); void Refresh();
        void CreateViewWindow(); void DestroyViewWindow(); void GetCurrentInfo(); void AddPropertySheetPages(); void SaveViewState(); void SelectItem();
        void GetItemObject(uint which, ref Guid iid, [MarshalAs(UnmanagedType.IDispatch)] out object background);
    }
    [DllImport("ole32.dll")] private static extern int CoCreateInstance(ref Guid cls, IntPtr outer, uint context, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellWindows result);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint id);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, out int information, int length, out int returned);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}
