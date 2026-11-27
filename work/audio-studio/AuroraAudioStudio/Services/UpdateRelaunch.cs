using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
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
        if (!Environment.GetCommandLineArgs().Contains(Argument, StringComparer.Ordinal)) return;
        try
        {
            if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0008, out var current)) throw new Win32Exception();
            using (current) if (!IsElevated(current)) return;
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
        if (process.IsInvalid || !OpenProcessToken(process.DangerousGetHandle(), 0x000A, out var shellToken)) throw new Win32Exception();
        using (shellToken)
        {
            if (IsElevated(shellToken)) throw new IOException("The desktop does not have an ordinary-user token.");
            if (!DuplicateTokenEx(shellToken, 0x0001 | 0x0002 | 0x0008, IntPtr.Zero, 2, 1, out var primary)) throw new Win32Exception();
            using (primary)
            {
                if (!CreateEnvironmentBlock(out var environment, primary, false)) throw new Win32Exception();
                try
                {
                    var startup = new StartupInfo { cb = Marshal.SizeOf<StartupInfo>(), desktop = "winsta0\\default" };
                    var command = new StringBuilder("\"" + executable + "\" " + Argument);
                    // Explicit environment and profile prevent alternate-admin APPDATA/HOME inheritance.
                    if (!CreateProcessWithTokenW(primary, 1, executable, command, 0x400, environment,
                        Path.GetDirectoryName(executable)!, ref startup, out var child)) throw new Win32Exception();
                    CloseHandle(child.thread); CloseHandle(child.process);
                }
                finally { DestroyEnvironmentBlock(environment); }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public string? reserved, desktop, title;
        public int x, y, xSize, ySize, xCountChars, yCountChars, fillAttribute, flags;
        public short showWindow, reservedSize;
        public IntPtr reservedBytes, stdInput, stdOutput, stdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public IntPtr process, thread; public uint processId, threadId; }
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern SafeProcessHandle OpenProcess(uint access, bool inherit, uint id);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int informationClass, out int information, int length, out int returned);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DuplicateTokenEx(SafeAccessTokenHandle existing, uint access, IntPtr attributes, int impersonationLevel, int tokenType, out SafeAccessTokenHandle token);
    [DllImport("userenv.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, SafeAccessTokenHandle token, bool inherit);
    [DllImport("userenv.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateProcessWithTokenW(SafeAccessTokenHandle token, uint flags, string application, StringBuilder command,
        uint creationFlags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInformation information);
    [DllImport("kernel32.dll")] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int MessageBox(IntPtr owner, string text, string caption, uint type);
}
