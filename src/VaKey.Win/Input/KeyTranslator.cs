namespace VaKey.Win.Input;

/// <summary>Chuyển mã phím ảo thành ký tự cho engine. Chỉ chữ cái và chữ số; phím khác kết thúc từ.</summary>
public static class KeyTranslator
{
    public static char Translate(uint vk, bool shift, bool capsLock)
    {
        if (vk is >= 0x41 and <= 0x5A)
        {
            char c = (char)('a' + (vk - 0x41));
            return shift ^ capsLock ? char.ToUpperInvariant(c) : c;
        }

        if (vk is >= 0x30 and <= 0x39)
        {
            return shift ? '\0' : (char)('0' + (vk - 0x30));
        }

        return '\0';
    }
}
