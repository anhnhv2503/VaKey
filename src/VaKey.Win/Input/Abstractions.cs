namespace VaKey.Win.Input;

/// <summary>Cách gửi ký tự tới ứng dụng (chọn theo hồ sơ tiến trình).</summary>
public enum SendMode
{
    /// <summary>Backspace + Unicode trong một lần SendInput (mặc định).</summary>
    Standard,

    /// <summary>Thêm độ trễ nhỏ giữa Backspace và ký tự (app chậm, Remote Desktop, máy ảo).</summary>
    Slow,

    /// <summary>Chọn ký tự bằng Shift+Left rồi ghi đè (ô địa chỉ có tự hoàn thành).</summary>
    Selection,

    /// <summary>Dán qua clipboard rồi khôi phục (phương án cuối).</summary>
    Clipboard,
}

public interface IKeySender
{
    void Send(int backspaces, string text, SendMode mode);

    /// <summary>Gửi một phím giả để ứng dụng không mở menu khi Alt/Win được nhả sau một tổ hợp bị nuốt.</summary>
    void SendDummyKey();
}

public interface IKeyboardState
{
    bool IsDown(uint vk);

    bool CapsLock { get; }
}

[Flags]
public enum Modifiers
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8,
}
