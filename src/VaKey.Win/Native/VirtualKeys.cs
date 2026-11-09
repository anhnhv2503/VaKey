namespace VaKey.Win.Native;

internal static class VirtualKeys
{
    public const uint Back = 0x08;
    public const uint Tab = 0x09;
    public const uint Return = 0x0D;
    public const uint Shift = 0x10;
    public const uint Control = 0x11;
    public const uint Menu = 0x12;
    public const uint Pause = 0x13;
    public const uint Capital = 0x14;
    public const uint Escape = 0x1B;
    public const uint Space = 0x20;
    public const uint Left = 0x25;
    public const uint Insert = 0x2D;
    public const uint Delete = 0x2E;
    public const uint LWin = 0x5B;
    public const uint RWin = 0x5C;
    public const uint F1 = 0x70;
    public const uint F24 = 0x87;
    public const uint NumLock = 0x90;
    public const uint Scroll = 0x91;
    public const uint LShift = 0xA0;
    public const uint RShift = 0xA1;
    public const uint LControl = 0xA2;
    public const uint RControl = 0xA3;
    public const uint LMenu = 0xA4;
    public const uint RMenu = 0xA5;
    public const uint Packet = 0xE7;

    /// <summary>Mã phím không gán, dùng làm phím giả để chặn menu khi nuốt Alt/Win + phím.</summary>
    public const uint Unassigned = 0xE8;

    public static bool IsModifier(uint vk) => vk is Shift or Control or Menu or LShift or RShift or LControl
        or RControl or LMenu or RMenu or LWin or RWin;

    /// <summary>Phím không làm thay đổi văn bản, không cần kết thúc từ.</summary>
    public static bool IsNeutral(uint vk) => vk is Capital or NumLock or Scroll or Pause
        || vk is >= 0xA6 and <= 0xB7; // phím trình duyệt / đa phương tiện
}
