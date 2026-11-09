using VaKey.Core.Settings;

namespace VaKey.Win.Config;

/// <summary>Cấu hình lưu tại %AppData%\VaKey\config.json (hoặc thư mục Data cạnh exe ở bản portable).</summary>
public sealed class AppConfig
{
    public const int CurrentVersion = 1;

    public int Version { get; set; } = CurrentVersion;

    public InputMethodKind InputMethod { get; set; } = InputMethodKind.Telex;

    /// <summary>Bản 1.0 chỉ hỗ trợ Unicode dựng sẵn.</summary>
    public string Encoding { get; set; } = "Unicode";

    public ToneStyle ToneStyle { get; set; } = ToneStyle.New;

    public bool SpellCheck { get; set; } = true;

    public bool WStandaloneToU { get; set; } = true;

    /// <summary>Trạng thái khi khởi động: true = tiếng Việt.</summary>
    public bool Vietnamese { get; set; } = true;

    public string ToggleHotkey { get; set; } = "Ctrl+Shift";

    public bool RunAtStartup { get; set; }

    /// <summary>Khởi động cùng Windows bằng tác vụ lên lịch quyền cao nhất (để gõ được trong app admin).</summary>
    public bool RunAsAdmin { get; set; }

    public bool RememberPerApp { get; set; } = true;

    public bool BeepOnToggle { get; set; }

    public bool NotifyOnToggle { get; set; }

    public bool DiagnosticLogging { get; set; }

    /// <summary>Hiện bảng điều khiển cả khi VaKey tự khởi động cùng Windows.</summary>
    public bool ShowPanelAtLogin { get; set; }

    /// <summary>Tên tiến trình (vd. "game.exe") luôn tắt bộ gõ.</summary>
    public List<string> ExcludedApps { get; set; } = [];

    /// <summary>Từ tiếng Anh không bao giờ bị biến đổi.</summary>
    public List<string> EnglishExceptions { get; set; } = [];

    /// <summary>Trạng thái Việt/Anh đã nhớ theo tiến trình.</summary>
    public Dictionary<string, bool> AppStates { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public EngineOptions ToEngineOptions() => new()
    {
        Method = InputMethod,
        ToneStyle = ToneStyle,
        SpellCheck = SpellCheck,
        WStandaloneToU = WStandaloneToU,
        EnglishExceptions = EnglishExceptions.ToArray(),
    };

    public AppConfig Clone()
    {
        var copy = (AppConfig)MemberwiseClone();
        copy.ExcludedApps = [.. ExcludedApps];
        copy.EnglishExceptions = [.. EnglishExceptions];
        copy.AppStates = new Dictionary<string, bool>(AppStates, StringComparer.OrdinalIgnoreCase);
        return copy;
    }

    /// <summary>Chuẩn hóa tên tiến trình: chữ thường, có đuôi .exe.</summary>
    public static string NormalizeProcessName(string name)
    {
        name = Path.GetFileName(name.Trim()).ToLowerInvariant();
        if (name.Length > 0 && !name.EndsWith(".exe", StringComparison.Ordinal)) name += ".exe";
        return name;
    }
}
