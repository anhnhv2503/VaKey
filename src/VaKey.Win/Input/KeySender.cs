using System.Runtime.InteropServices;
using VaKey.Win.Native;
using static VaKey.Win.Native.NativeMethods;

namespace VaKey.Win.Input;

/// <summary>Gửi Backspace và ký tự Unicode bằng SendInput. Mọi sự kiện mang dấu hiệu riêng của VaKey.</summary>
public sealed class KeySender : IKeySender
{
    /// <summary>"VAKY" — dwExtraInfo để nhận ra phím do chính VaKey gửi.</summary>
    public const nuint Marker = 0x56414B59;

    private const int SlowDelayMs = 4;
    private const ushort BackspaceScan = 0x0E;
    private static readonly int InputSize = Marshal.SizeOf<INPUT>();

    private readonly IKeyboardState _keyboard;
    private INPUT[] _buffer = new INPUT[64];
    private int _count;

    public KeySender(IKeyboardState keyboard)
    {
        _keyboard = keyboard;
    }

    public void Send(int backspaces, string text, SendMode mode)
    {
        switch (mode)
        {
            case SendMode.Slow:
                AddBackspaces(backspaces);
                Flush();
                if (backspaces > 0 && text.Length > 0) Thread.Sleep(SlowDelayMs);
                AddText(text);
                Flush();
                break;

            case SendMode.Selection:
                AddSelectLeft(backspaces);
                AddText(text);
                Flush();
                break;

            case SendMode.Clipboard:
                if (!SendViaClipboard(backspaces, text)) goto default;
                break;

            default:
                AddBackspaces(backspaces);
                AddText(text);
                Flush();
                break;
        }
    }

    public void SendDummyKey()
    {
        AddKey((ushort)VirtualKeys.Unassigned, 0, 0);
        AddKey((ushort)VirtualKeys.Unassigned, 0, KEYEVENTF_KEYUP);
        Flush();
    }

    private void AddBackspaces(int n)
    {
        for (int i = 0; i < n; i++)
        {
            AddKey((ushort)VirtualKeys.Back, BackspaceScan, 0);
            AddKey((ushort)VirtualKeys.Back, BackspaceScan, KEYEVENTF_KEYUP);
        }
    }

    private void AddText(string text)
    {
        foreach (var ch in text)
        {
            AddKey(0, ch, KEYEVENTF_UNICODE);
            AddKey(0, ch, KEYEVENTF_UNICODE | KEYEVENTF_KEYUP);
        }
    }

    private void AddSelectLeft(int n)
    {
        if (n == 0) return;
        bool shiftHeld = _keyboard.IsDown(VirtualKeys.LShift) || _keyboard.IsDown(VirtualKeys.RShift);
        if (!shiftHeld) AddKey((ushort)VirtualKeys.LShift, 0x2A, 0);
        for (int i = 0; i < n; i++)
        {
            AddKey((ushort)VirtualKeys.Left, 0x4B, KEYEVENTF_EXTENDEDKEY);
            AddKey((ushort)VirtualKeys.Left, 0x4B, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP);
        }

        if (!shiftHeld) AddKey((ushort)VirtualKeys.LShift, 0x2A, KEYEVENTF_KEYUP);
    }

    private bool SendViaClipboard(int backspaces, string text)
    {
        string? previous = null;
        try
        {
            if (Clipboard.ContainsText()) previous = Clipboard.GetText();
            if (text.Length > 0) Clipboard.SetText(text);
        }
        catch (ExternalException)
        {
            return false; // clipboard đang bị khóa → dùng cách thường
        }

        AddBackspaces(backspaces);
        if (text.Length > 0)
        {
            bool ctrlHeld = _keyboard.IsDown(VirtualKeys.LControl) || _keyboard.IsDown(VirtualKeys.RControl);
            if (!ctrlHeld) AddKey((ushort)VirtualKeys.LControl, 0x1D, 0);
            AddKey(0x56, 0x2F, 0);
            AddKey(0x56, 0x2F, KEYEVENTF_KEYUP);
            if (!ctrlHeld) AddKey((ushort)VirtualKeys.LControl, 0x1D, KEYEVENTF_KEYUP);
        }

        Flush();

        if (text.Length > 0) RestoreClipboardLater(previous);
        return true;
    }

    private static void RestoreClipboardLater(string? previous)
    {
        var t = new Thread(() =>
        {
            Thread.Sleep(300);
            try
            {
                if (previous is null) Clipboard.Clear();
                else Clipboard.SetText(previous);
            }
            catch (ExternalException)
            {
            }
        })
        {
            IsBackground = true,
            Name = "VaKey.ClipboardRestore",
        };
        t.SetApartmentState(ApartmentState.STA);
        t.Start();
    }

    private void AddKey(ushort vk, ushort scan, uint flags)
    {
        if (_count == _buffer.Length) Array.Resize(ref _buffer, _buffer.Length * 2);
        _buffer[_count++] = new INPUT
        {
            type = INPUT_KEYBOARD,
            U = new InputUnion
            {
                ki = new KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags, dwExtraInfo = Marker },
            },
        };
    }

    private void Flush()
    {
        if (_count == 0) return;
        uint sent = SendInput((uint)_count, _buffer, InputSize);
        if (sent != _count) Diagnostics.Log.Error($"SendInput gửi {sent}/{_count} sự kiện (lỗi {Marshal.GetLastWin32Error()})");
        _count = 0;
    }
}

/// <summary>Đọc trạng thái phím thật từ Windows.</summary>
public sealed class Win32KeyboardState : IKeyboardState
{
    public bool IsDown(uint vk) => (GetAsyncKeyState((int)vk) & 0x8000) != 0;

    public bool CapsLock => (GetKeyState((int)VirtualKeys.Capital) & 1) != 0;
}
