using System.ComponentModel;
using System.Diagnostics;
using Microsoft.Win32;

namespace VaKey.Win.App;

/// <summary>Khởi động cùng Windows: khóa Run (quyền thường) hoặc tác vụ lên lịch quyền cao nhất (admin).</summary>
internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "VaKey";
    private const string TaskName = "VaKey (admin)";

    private static string ExePath => Environment.ProcessPath ?? Application.ExecutablePath;

    private static string Command => $"\"{ExePath}\" --startup";

    public static bool IsRunEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string s && s.Contains(ExePath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetRunAtStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) key.SetValue(ValueName, Command);
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Tạo/xóa tác vụ lên lịch khi đăng nhập với quyền cao nhất. Cần xác nhận UAC.</summary>
    public static bool SetAdminTask(bool enabled)
    {
        string args = enabled
            ? $"/Create /F /TN \"{TaskName}\" /SC ONLOGON /RL HIGHEST /TR \"\\\"{ExePath}\\\" --startup\""
            : $"/Delete /F /TN \"{TaskName}\"";
        return RunElevated("schtasks.exe", args, wait: true);
    }

    /// <summary>Khởi động lại VaKey với quyền admin.</summary>
    public static bool RestartAsAdmin() => RunElevated(ExePath, "--restarted", wait: false);

    private static bool RunElevated(string file, string args, bool wait)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden,
            });
            if (wait && p is not null)
            {
                p.WaitForExit(15000);
                return p.HasExited && p.ExitCode == 0;
            }

            return p is not null;
        }
        catch (Win32Exception)
        {
            return false; // người dùng từ chối UAC
        }
    }
}
