using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using AuroraAudioStudio.Services;

internal static class ProcessLaunchRegression
{
    public static void RunFixture()
    {
        Console.OutputEncoding = Encoding.UTF8;
        try
        {
            // Python subprocess duplicates inherited stdin in the same way when SoX starts.
            var current = GetCurrentProcess();
            if (!DuplicateHandle(current, GetStdHandle(-10), current, out var duplicate, 0, true, 2))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            CloseHandle(duplicate);
            if (Console.OpenStandardInput().ReadByte() != -1) throw new IOException("Expected immediate EOF.");
            Console.WriteLine("STDIN_VALID_EOF Ω");
            Console.Error.WriteLine("STDERR_PRESERVED Ω");
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 23; }
    }

    public static async Task RunAsync()
    {
        if (!OperatingSystem.IsWindows()) return;
        var passed = 0;
        var settings = new SettingsService(Path.Combine(Path.GetTempPath(), "Aurora-process-test-" + Guid.NewGuid().ToString("N")));
        var updater = new ModelUpdateService(new ModelCatalogService(settings), settings);
        var original = GetStdHandle(-10);
        foreach (var inherited in new[] { new IntPtr(0x12345678), IntPtr.Zero, original })
        foreach (var runner in new[] { typeof(BackendService), typeof(ModelUpdateService) })
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!)
            {
                UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            };
            if (Path.GetFileNameWithoutExtension(info.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            info.ArgumentList.Add("--stdin-fixture");
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            Task<(int ExitCode, string Output, string Error)> running;
            try
            {
                if (!SetStdHandle(-10, inherited)) throw new Win32Exception(Marshal.GetLastWin32Error());
                var method = runner.GetMethod("RunProcessAsync", BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)!;
                object?[] arguments = runner == typeof(BackendService) ? [info, deadline.Token] : [info, deadline.Token, null];
                running = (Task<(int, string, string)>)method.Invoke(runner == typeof(ModelUpdateService) ? updater : null, arguments)!;
            }
            finally { SetStdHandle(-10, original); }
            var result = await running;
            if (result.ExitCode != 0 || !result.Output.Contains("STDIN_VALID_EOF Ω") || !result.Error.Contains("STDERR_PRESERVED Ω"))
                throw new Exception($"{runner.Name}: invalid/null/normal inherited stdin must become a valid EOF pipe. {result}");
            passed++;
            Console.WriteLine($"PASS {runner.Name} valid EOF stdin and preserved output (parent handle {inherited})");
        }
        Console.WriteLine($"Background process checks passed: {passed}.");
    }

    [DllImport("kernel32.dll")] private static extern IntPtr GetStdHandle(int kind);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetStdHandle(int kind, IntPtr handle);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool DuplicateHandle(IntPtr sourceProcess, IntPtr source, IntPtr targetProcess, out IntPtr target, uint access, bool inherit, uint options);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
