using System.Diagnostics;

namespace VaKey.Win.App;

/// <summary>Phát hiện bộ gõ khác đang chạy (gây gõ đúp dấu).</summary>
internal static class ConflictDetector
{
    private static readonly string[] Known =
    [
        "UniKeyNT", "UniKey", "EVKey", "EVKey64", "EVKey32", "OpenKey", "OpenKey64", "OpenKey32", "GoTiengViet", "VietKey",
    ];

    public static IReadOnlyList<string> FindRunning()
    {
        var found = new List<string>();
        foreach (var name in Known)
        {
            Process[] processes;
            try
            {
                processes = Process.GetProcessesByName(name);
            }
            catch (InvalidOperationException)
            {
                continue;
            }

            if (processes.Length > 0) found.Add(name);
            foreach (var p in processes) p.Dispose();
        }

        return found;
    }
}
