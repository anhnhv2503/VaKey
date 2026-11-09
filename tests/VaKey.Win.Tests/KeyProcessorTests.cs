using System.Text;
using VaKey.Win.Config;
using VaKey.Win.Input;
using VaKey.Win.Native;
using VaKey.Win.Profiles;

namespace VaKey.Win.Tests;

internal sealed class FakeKeyboard : IKeyboardState
{
    public HashSet<uint> Down { get; } = [];

    public bool IsDown(uint vk) => Down.Contains(vk);

    public bool CapsLock { get; set; }
}

/// <summary>Ô soạn thảo giả: nhận phím chuyển tiếp và kết quả SendInput.</summary>
internal sealed class FakeEditor : IKeySender
{
    public StringBuilder Text { get; } = new();

    public List<SendMode> Modes { get; } = [];

    public int DummyKeys { get; private set; }

    public void Send(int backspaces, string text, SendMode mode)
    {
        Modes.Add(mode);
        Text.Length -= backspaces;
        Text.Append(text);
    }

    public void SendDummyKey() => DummyKeys++;
}

internal sealed class Harness
{
    public FakeKeyboard Keyboard { get; } = new();
    public FakeEditor Editor { get; } = new();
    public KeyProcessor Processor { get; }

    public Harness(AppConfig? config = null, ProfileSet? profiles = null, string? ownProcess = null)
    {
        Processor = new KeyProcessor(Editor, Keyboard, config ?? new AppConfig(), profiles ?? ProfileSet.Empty)
        {
            OwnProcess = ownProcess,
        };
    }

    /// <summary>Nhấn và nhả một phím; nếu không bị nuốt thì "ứng dụng" nhận ký tự.</summary>
    public void Tap(uint vk, char? produced = null)
    {
        bool swallowed = Processor.OnKey(vk, down: true);
        if (!swallowed)
        {
            if (vk == VirtualKeys.Back) { if (Editor.Text.Length > 0) Editor.Text.Length--; }
            else if (produced is { } c) Editor.Text.Append(c);
        }

        Processor.OnKey(vk, down: false);
    }

    public void Type(string text)
    {
        foreach (var ch in text)
        {
            if (ch == '\b') { Tap(VirtualKeys.Back); continue; }
            if (ch == ' ') { Tap(VirtualKeys.Space, ' '); continue; }
            if (ch == '.') { Tap(0xBE, '.'); continue; }

            bool upper = char.IsUpper(ch);
            if (upper) Keyboard.Down.Add(VirtualKeys.LShift);
            uint vk = char.IsDigit(ch) ? ch : char.ToUpperInvariant(ch);
            Tap(vk, KeyTranslator.Translate(vk, upper, Keyboard.CapsLock));
            if (upper) Keyboard.Down.Remove(VirtualKeys.LShift);
        }
    }

    public void Press(uint vk)
    {
        Processor.OnKey(vk, true);
        Keyboard.Down.Add(vk);
    }

    public void Release(uint vk)
    {
        Processor.OnKey(vk, false);
        Keyboard.Down.Remove(vk);
    }
}

public class KeyProcessorTests
{
    [Fact]
    public void TypesVietnameseThroughHook()
    {
        var h = new Harness();
        h.Type("Vieetj Nam. Nguwowif ");
        Assert.Equal("Việt Nam. Người ", h.Editor.Text.ToString());
    }

    [Fact]
    public void CapsLockProducesUppercase()
    {
        var h = new Harness();
        h.Keyboard.CapsLock = true;
        h.Type("vieetj");
        Assert.Equal("VIỆT", h.Editor.Text.ToString());
    }

    [Fact]
    public void BackspaceIsHandled()
    {
        var h = new Harness();
        h.Type("vieet\bns");
        Assert.Equal("viến", h.Editor.Text.ToString());
    }

    [Fact]
    public void CtrlComboResetsAndPassesThrough()
    {
        var h = new Harness();
        h.Type("a");
        h.Keyboard.Down.Add(VirtualKeys.LControl);
        Assert.False(h.Processor.OnKey('A', true));
        h.Keyboard.Down.Remove(VirtualKeys.LControl);
        Assert.Equal("", h.Processor.Session.CurrentWord);
    }

    [Fact]
    public void CtrlShiftTogglesOnRelease()
    {
        var h = new Harness();
        Assert.True(h.Processor.Vietnamese);
        bool? toggled = null;
        h.Processor.Toggled += on => toggled = on;

        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LShift);
        h.Release(VirtualKeys.LShift);
        h.Release(VirtualKeys.LControl);

        Assert.False(h.Processor.Vietnamese);
        Assert.False(toggled);

        h.Type("as");
        Assert.Equal("as", h.Editor.Text.ToString());
    }

    [Fact]
    public void CtrlShiftWithOtherKeyDoesNotToggle()
    {
        var h = new Harness();
        h.Press(VirtualKeys.LControl);
        h.Press(VirtualKeys.LShift);
        h.Processor.OnKey('T', true); // Ctrl+Shift+T
        h.Processor.OnKey('T', false);
        h.Release(VirtualKeys.LShift);
        h.Release(VirtualKeys.LControl);
        Assert.True(h.Processor.Vietnamese);
    }

    [Fact]
    public void KeyHotkeyIsSwallowedAndSuppressesMenu()
    {
        var h = new Harness(new AppConfig { ToggleHotkey = "Alt+Z" });
        h.Keyboard.Down.Add(VirtualKeys.LMenu);
        Assert.True(h.Processor.OnKey('Z', true));
        Assert.True(h.Processor.OnKey('Z', false));
        Assert.False(h.Processor.Vietnamese);
        Assert.Equal(1, h.Editor.DummyKeys);
    }

    [Fact]
    public void ExcludedAppDisablesTyping()
    {
        var h = new Harness(new AppConfig { ExcludedApps = ["Game"] });
        h.Processor.OnForegroundChanged("game.exe");
        Assert.False(h.Processor.Effective);
        h.Type("as");
        Assert.Equal("as", h.Editor.Text.ToString());
    }

    [Fact]
    public void RemembersStatePerApp()
    {
        var h = new Harness();
        h.Processor.OnForegroundChanged("code.exe");
        h.Processor.Toggle(); // English trong VS Code
        h.Processor.OnForegroundChanged("zalo.exe");
        h.Processor.SetVietnamese(true, fromUser: true);
        h.Processor.OnForegroundChanged("code.exe");
        Assert.False(h.Processor.Vietnamese);
        h.Processor.OnForegroundChanged("zalo.exe");
        Assert.True(h.Processor.Vietnamese);

        var snapshot = h.Processor.SnapshotAppStates();
        Assert.False(snapshot["code.exe"]);
    }

    [Fact]
    public void OwnWindowKeepsPreviousAppState()
    {
        var h = new Harness(ownProcess: "vakey.exe");
        h.Processor.OnForegroundChanged("code.exe");
        h.Processor.OnForegroundChanged("vakey.exe"); // mở bảng điều khiển
        Assert.Equal("code.exe", h.Processor.State.Process);
        h.Processor.Toggle();                        // bấm nút V/E trên bảng
        h.Processor.OnForegroundChanged("notepad.exe");
        h.Processor.OnForegroundChanged("code.exe");
        Assert.False(h.Processor.Vietnamese);
        Assert.DoesNotContain("vakey.exe", h.Processor.SnapshotAppStates().Keys);
    }

    [Fact]
    public void ProfileSelectsSendMode()
    {
        var profiles = new ProfileSet([new AppProfile { Process = "mstsc", SendMode = SendMode.Slow }]);
        var h = new Harness(profiles: profiles);
        h.Processor.OnForegroundChanged("mstsc.exe");
        h.Type("aa");
        Assert.Equal([SendMode.Slow], h.Editor.Modes);
    }

    [Fact]
    public void MouseClickResetsWord()
    {
        var h = new Harness();
        h.Type("a");
        h.Processor.OnMouseDown();
        h.Type("s");
        Assert.Equal("as", h.Editor.Text.ToString());
    }

    [Fact]
    public void StateChangedIsRaised()
    {
        var h = new Harness();
        InputState? last = null;
        h.Processor.StateChanged += s => last = s;
        h.Processor.OnForegroundChanged("notepad.exe");
        Assert.Equal(new InputState(true, true, "notepad.exe", false), last);
    }
}
