using VaKey.Core.Data;

namespace VaKey.Core.Engine;

public enum KeyActionKind : byte
{
    None,
    Tone,
    RemoveTone,

    /// <summary>Dấu mũ. Telex chỉ định chữ đích (aa, ee, oo); VNI '6' tự tìm.</summary>
    Circumflex,
    Breve,
    Horn,

    /// <summary>Telex 'w': móc cho o/u, trăng cho a, hoặc 'ư' khi đứng riêng.</summary>
    HornOrBreve,
    Stroke,
}

/// <summary>Hành động mà một phím có thể gây ra trong một kiểu gõ.</summary>
public readonly record struct KeyAction(KeyActionKind Kind, Tone Tone = Tone.None, char Target = '\0')
{
    public static readonly KeyAction None = new(KeyActionKind.None);
}

public interface ITypingMethod
{
    /// <summary>Trả về true nếu phím này là phím biến đổi (dấu, mũ, ...).</summary>
    bool IsTransformKey(char key);

    /// <summary>Phím có thuộc về một từ đang gõ hay không (chữ cái; với VNI cả chữ số).</summary>
    bool IsWordKey(char key);

    KeyAction GetAction(char key);
}

public static class TypingMethods
{
    public static ITypingMethod For(Settings.InputMethodKind kind) => kind switch
    {
        Settings.InputMethodKind.Vni => VniMethod.Instance,
        _ => TelexMethod.Instance,
    };

    internal static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
}
