using System.Diagnostics.CodeAnalysis;
using VaKey.Win.Native;

namespace VaKey.Win.Input;

/// <summary>
/// Phím tắt chuyển Việt/Anh. Hoặc chỉ gồm phím bổ trợ (Ctrl+Shift: kích hoạt khi nhả, nếu không có phím nào
/// khác được nhấn xen giữa), hoặc phím bổ trợ + một phím (Alt+Z, Ctrl+Space: kích hoạt khi nhấn).
/// </summary>
public sealed record HotkeyDefinition(Modifiers Modifiers, uint Vk)
{
    public static readonly HotkeyDefinition Default = new(Modifiers.Ctrl | Modifiers.Shift, 0);

    public static readonly string[] Presets = ["Ctrl+Shift", "Alt+Z", "Ctrl+Space", "Alt+Shift", "Ctrl+Alt+V"];

    public bool IsModifierOnly => Vk == 0;

    public static HotkeyDefinition ParseOrDefault(string? text) => TryParse(text, out var h) ? h : Default;

    public static bool TryParse(string? text, [NotNullWhen(true)] out HotkeyDefinition? hotkey)
    {
        hotkey = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var mods = Modifiers.None;
        uint vk = 0;
        foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.ToUpperInvariant();
            switch (token)
            {
                case "CTRL" or "CONTROL": mods |= Modifiers.Ctrl; continue;
                case "SHIFT": mods |= Modifiers.Shift; continue;
                case "ALT": mods |= Modifiers.Alt; continue;
                case "WIN" or "WINDOWS": mods |= Modifiers.Win; continue;
            }

            if (vk != 0) return false; // chỉ cho phép một phím thường
            vk = ParseKey(token);
            if (vk == 0) return false;
        }

        int modCount = System.Numerics.BitOperations.PopCount((uint)mods);
        if (vk == 0 && modCount < 2) return false;
        if (vk != 0 && modCount < 1) return false;

        hotkey = new HotkeyDefinition(mods, vk);
        return true;
    }

    private static uint ParseKey(string token)
    {
        if (token.Length == 1 && token[0] is >= 'A' and <= 'Z' or >= '0' and <= '9') return token[0];
        if (token == "SPACE") return VirtualKeys.Space;
        if (token == "`" || token == "TILDE") return 0xC0;
        if (token.Length is 2 or 3 && token[0] == 'F' && int.TryParse(token.AsSpan(1), out int f) && f is >= 1 and <= 12)
        {
            return (uint)(VirtualKeys.F1 + f - 1);
        }

        return 0;
    }

    public override string ToString()
    {
        var parts = new List<string>(4);
        if (Modifiers.HasFlag(Modifiers.Ctrl)) parts.Add("Ctrl");
        if (Modifiers.HasFlag(Modifiers.Alt)) parts.Add("Alt");
        if (Modifiers.HasFlag(Modifiers.Shift)) parts.Add("Shift");
        if (Modifiers.HasFlag(Modifiers.Win)) parts.Add("Win");
        if (Vk != 0) parts.Add(KeyName(Vk));
        return string.Join("+", parts);
    }

    private static string KeyName(uint vk) => vk switch
    {
        VirtualKeys.Space => "Space",
        0xC0 => "`",
        >= VirtualKeys.F1 and <= VirtualKeys.F1 + 11 => "F" + (vk - VirtualKeys.F1 + 1),
        _ => ((char)vk).ToString(),
    };
}
