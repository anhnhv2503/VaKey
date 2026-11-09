namespace VaKey.Core.Settings;

public enum InputMethodKind
{
    Telex,
    Vni,
}

/// <summary>Kiểu đặt dấu cho "oa", "oe", "uy" khi không có âm cuối.</summary>
public enum ToneStyle
{
    /// <summary>hòa, thúy, khỏe</summary>
    New,

    /// <summary>hoà, thuý, khoẻ</summary>
    Old,
}

public sealed record EngineOptions
{
    public InputMethodKind Method { get; init; } = InputMethodKind.Telex;

    public ToneStyle ToneStyle { get; init; } = ToneStyle.New;

    /// <summary>Không biến đổi khi từ không thể là âm tiết tiếng Việt.</summary>
    public bool SpellCheck { get; init; } = true;

    /// <summary>Telex: phím 'w' đứng riêng thành 'ư'.</summary>
    public bool WStandaloneToU { get; init; } = true;

    /// <summary>Từ (theo chuỗi phím gõ) không bao giờ bị biến đổi, ví dụ "tax", "if".</summary>
    public IReadOnlyCollection<string> EnglishExceptions { get; init; } = [];

    public static EngineOptions Default { get; } = new();
}
