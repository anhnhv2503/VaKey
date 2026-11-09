namespace VaKey.Win.Diagnostics;

/// <summary>
/// Log chẩn đoán. Tắt mặc định. Chỉ ghi sự kiện hệ thống (cài hook, đổi chế độ, lỗi),
/// KHÔNG BAO GIỜ ghi nội dung gõ hay mã phím.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static string? _path;

    public static bool Enabled { get; private set; }

    public static void Configure(bool enabled, string directory)
    {
        lock (Gate)
        {
            Enabled = enabled;
            _path = enabled ? Path.Combine(directory, $"vakey-{DateTime.Now:yyyyMMdd}.log") : null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string message)
    {
        if (!Enabled) return;
        lock (Gate)
        {
            if (_path is null) return;
            try
            {
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.AppendAllText(_path, $"{DateTime.Now:HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
