using VaKey.Core.Data;

namespace VaKey.Core.Engine;

public sealed class VniMethod : ITypingMethod
{
    public static readonly VniMethod Instance = new();

    public bool IsTransformKey(char key) => GetAction(key).Kind != KeyActionKind.None;

    public bool IsWordKey(char key) => TypingMethods.IsAsciiLetter(key) || key is >= '0' and <= '9';

    public KeyAction GetAction(char key) => key switch
    {
        '1' => new(KeyActionKind.Tone, Tone.Acute),
        '2' => new(KeyActionKind.Tone, Tone.Grave),
        '3' => new(KeyActionKind.Tone, Tone.Hook),
        '4' => new(KeyActionKind.Tone, Tone.Tilde),
        '5' => new(KeyActionKind.Tone, Tone.Dot),
        '6' => new(KeyActionKind.Circumflex),
        '7' => new(KeyActionKind.Horn),
        '8' => new(KeyActionKind.Breve),
        '9' => new(KeyActionKind.Stroke),
        '0' => new(KeyActionKind.RemoveTone),
        _ => KeyAction.None,
    };
}
