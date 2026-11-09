using VaKey.Core.Data;

namespace VaKey.Core.Engine;

public sealed class TelexMethod : ITypingMethod
{
    public static readonly TelexMethod Instance = new();

    public bool IsTransformKey(char key) => GetAction(key).Kind != KeyActionKind.None;

    public bool IsWordKey(char key) => TypingMethods.IsAsciiLetter(key);

    public KeyAction GetAction(char key) => char.ToLowerInvariant(key) switch
    {
        's' => new(KeyActionKind.Tone, Tone.Acute),
        'f' => new(KeyActionKind.Tone, Tone.Grave),
        'r' => new(KeyActionKind.Tone, Tone.Hook),
        'x' => new(KeyActionKind.Tone, Tone.Tilde),
        'j' => new(KeyActionKind.Tone, Tone.Dot),
        'z' => new(KeyActionKind.RemoveTone),
        'a' => new(KeyActionKind.Circumflex, Target: 'a'),
        'e' => new(KeyActionKind.Circumflex, Target: 'e'),
        'o' => new(KeyActionKind.Circumflex, Target: 'o'),
        'w' => new(KeyActionKind.HornOrBreve),
        'd' => new(KeyActionKind.Stroke),
        _ => KeyAction.None,
    };
}
