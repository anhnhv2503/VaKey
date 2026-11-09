namespace VaKey.Core.Engine;

/// <summary>
/// Kết quả xử lý một phím. Nếu <see cref="SwallowKey"/> là false thì phím gốc được chuyển tiếp nguyên vẹn
/// và hai trường còn lại bằng 0/rỗng. Nếu true: gửi <see cref="BackspaceCount"/> Backspace rồi chèn
/// <see cref="TextToInsert"/>, và nuốt phím gốc.
/// </summary>
public sealed class EngineResult
{
    public static readonly EngineResult PassThrough = new();

    public int BackspaceCount { get; init; }

    public string TextToInsert { get; init; } = "";

    public bool SwallowKey { get; init; }

    public override string ToString() =>
        SwallowKey ? $"[bs={BackspaceCount}, insert=\"{TextToInsert}\"]" : "[pass]";
}
