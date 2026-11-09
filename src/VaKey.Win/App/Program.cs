namespace VaKey.Win.App;

internal static class Program
{
    private const string MutexName = @"Local\VaKey.SingleInstance.7C1E2B9A";

    /// <summary>Bản thứ hai báo cho bản đang chạy hiện bảng điều khiển.</summary>
    internal const string ShowPanelEventName = @"Local\VaKey.ShowPanel.7C1E2B9A";

    [STAThread]
    private static int Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
        if (!created)
        {
            // Bản đang chạy có thể đang tự khởi động lại với quyền admin: chờ nó thoát.
            bool acquired = args.Contains("--restarted") && WaitForMutex(mutex);
            if (!acquired)
            {
                if (!args.Contains("--startup")) SignalRunningInstance();
                return 0;
            }
        }

        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => Diagnostics.Log.Error("Lỗi giao diện", e.Exception);

        using var app = new VaKeyApp(args);
        Application.Run(app);
        return 0;
    }

    private static void SignalRunningInstance()
    {
        if (EventWaitHandle.TryOpenExisting(ShowPanelEventName, out var handle))
        {
            using (handle) handle.Set();
        }
        else
        {
            MessageBox.Show("VaKey đang chạy. Xem biểu tượng V/E ở khay hệ thống.", "VaKey",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static bool WaitForMutex(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(5));
        }
        catch (AbandonedMutexException)
        {
            return true;
        }
    }
}
