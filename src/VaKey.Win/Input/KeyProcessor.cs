using VaKey.Core.Engine;
using VaKey.Core.Settings;
using VaKey.Win.Config;
using VaKey.Win.Native;
using VaKey.Win.Profiles;

namespace VaKey.Win.Input;

/// <summary>Trạng thái hiển thị cho tray.</summary>
public readonly record struct InputState(bool Vietnamese, bool Effective, string? Process, bool Excluded);

/// <summary>
/// Bộ não của luồng hook: quyết định chuyển tiếp hay nuốt phím, gọi engine và KeySender.
/// Chỉ được gọi trên luồng hook (trừ các phương thức có ghi chú khác).
/// </summary>
public sealed class KeyProcessor
{
    private readonly InputSession _session = new();
    private readonly IKeySender _sender;
    private readonly IKeyboardState _keyboard;
    private readonly HashSet<uint> _swallowedDown = [];
    private readonly Dictionary<string, bool> _appStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _appStatesGate = new();

    private HotkeyDefinition _hotkey = HotkeyDefinition.Default;
    private HashSet<string> _excludedApps = new(StringComparer.OrdinalIgnoreCase);
    private ProfileSet _profiles = ProfileSet.Empty;
    private bool _rememberPerApp = true;
    private volatile bool _vietnamese;
    private volatile bool _excluded;
    private SendMode _sendMode = SendMode.Standard;
    private string? _process;
    private bool _armed;

    public KeyProcessor(IKeySender sender, IKeyboardState keyboard, AppConfig config, ProfileSet profiles)
    {
        _sender = sender;
        _keyboard = keyboard;
        _vietnamese = config.Vietnamese;
        foreach (var (k, v) in config.AppStates) _appStates[k] = v;
        Apply(config, profiles);
    }

    /// <summary>Phát trên luồng hook khi trạng thái thay đổi.</summary>
    public event Action<InputState>? StateChanged;

    /// <summary>Phát khi người dùng bấm phím tắt (để kêu bíp / thông báo).</summary>
    public event Action<bool>? Toggled;

    public bool Vietnamese => _vietnamese;

    public bool Effective => _vietnamese && !_excluded;

    public InputState State => new(_vietnamese, Effective, _process, _excluded);

    public EngineOptions EngineOptions => _session.Options;

    internal InputSession Session => _session;

    public void Apply(AppConfig config, ProfileSet profiles)
    {
        _session.Options = config.ToEngineOptions();
        _hotkey = HotkeyDefinition.ParseOrDefault(config.ToggleHotkey);
        _excludedApps = new HashSet<string>(config.ExcludedApps.Select(AppConfig.NormalizeProcessName), StringComparer.OrdinalIgnoreCase);
        _profiles = profiles;
        _rememberPerApp = config.RememberPerApp;
        UpdateForProcess(_process, applyRemembered: false);
        RaiseState();
    }

    /// <summary>Có thể gọi từ luồng bất kỳ.</summary>
    public Dictionary<string, bool> SnapshotAppStates()
    {
        lock (_appStatesGate) return new Dictionary<string, bool>(_appStates, StringComparer.OrdinalIgnoreCase);
    }

    public void SetVietnamese(bool on, bool fromUser)
    {
        _vietnamese = on;
        _session.Reset();
        if (_rememberPerApp && _process is not null)
        {
            lock (_appStatesGate) _appStates[_process] = on;
        }

        RaiseState();
        if (fromUser) Toggled?.Invoke(on);
    }

    public void Toggle() => SetVietnamese(!_vietnamese, fromUser: true);

    public void OnMouseDown()
    {
        _session.Reset();
        _armed = false;
    }

    /// <summary>Tên tiến trình của chính VaKey (bảng điều khiển, menu khay).</summary>
    public string? OwnProcess { get; init; }

    public void OnForegroundChanged(string? process)
    {
        _session.Reset();
        _armed = false;
        _swallowedDown.Clear();

        // Cửa sổ của VaKey không phải "ứng dụng đích": giữ nguyên trạng thái của ứng dụng trước đó,
        // để bật/tắt từ bảng điều khiển hay menu khay áp dụng cho ứng dụng người dùng đang gõ.
        if (process is not null && string.Equals(process, OwnProcess, StringComparison.OrdinalIgnoreCase)) return;

        _process = process;
        UpdateForProcess(process, applyRemembered: true);
        RaiseState();
    }

    private void UpdateForProcess(string? process, bool applyRemembered)
    {
        _excluded = process is not null && (_excludedApps.Contains(process) || _profiles.IsDisabled(process));
        _sendMode = _profiles.GetSendMode(process);
        if (applyRemembered && _rememberPerApp && process is not null)
        {
            lock (_appStatesGate)
            {
                if (_appStates.TryGetValue(process, out var remembered)) _vietnamese = remembered;
            }
        }
    }

    /// <summary>Xử lý một sự kiện phím vật lý. Trả về true nếu phím bị nuốt.</summary>
    public bool OnKey(uint vk, bool down)
    {
        if (VirtualKeys.IsModifier(vk))
        {
            if (down) OnModifierDown(vk);
            else OnModifierUp(vk);
            return false;
        }

        if (!down) return _swallowedDown.Remove(vk);

        _armed = false;
        var mods = CurrentModifiers(0, false);

        if (!_hotkey.IsModifierOnly && vk == _hotkey.Vk && mods == _hotkey.Modifiers)
        {
            if ((mods & (Modifiers.Alt | Modifiers.Win)) != 0) _sender.SendDummyKey();
            Toggle();
            _swallowedDown.Add(vk);
            return true;
        }

        if (!Effective) return false;

        if ((mods & (Modifiers.Ctrl | Modifiers.Alt | Modifiers.Win)) != 0)
        {
            _session.Reset();
            return false;
        }

        if (vk == VirtualKeys.Back) return Emit(_session.Backspace(), vk);
        if (VirtualKeys.IsNeutral(vk)) return false;

        char c = KeyTranslator.Translate(vk, (mods & Modifiers.Shift) != 0, _keyboard.CapsLock);
        if (c == '\0')
        {
            _session.Reset();
            return false;
        }

        return Emit(_session.Process(c), vk);
    }

    private bool Emit(EngineResult result, uint vk)
    {
        if (!result.SwallowKey) return false;
        _sender.Send(result.BackspaceCount, result.TextToInsert, _sendMode);
        _swallowedDown.Add(vk);
        return true;
    }

    private void OnModifierDown(uint vk)
    {
        var mods = CurrentModifiers(vk, true);
        if (_hotkey.IsModifierOnly && mods == _hotkey.Modifiers) _armed = true;
        else if ((mods & ~_hotkey.Modifiers) != 0) _armed = false;
    }

    private void OnModifierUp(uint vk)
    {
        var before = CurrentModifiers(vk, true);
        if (_armed && _hotkey.IsModifierOnly && before == _hotkey.Modifiers)
        {
            _armed = false;
            Toggle();
        }
        else if (CurrentModifiers(vk, false) == Modifiers.None)
        {
            _armed = false;
        }
    }

    /// <summary>Trạng thái phím bổ trợ; <paramref name="overrideVk"/> là phím của sự kiện hiện tại (chưa cập nhật vào async state).</summary>
    private Modifiers CurrentModifiers(uint overrideVk, bool overrideDown)
    {
        overrideVk = overrideVk switch
        {
            VirtualKeys.Shift => VirtualKeys.LShift,
            VirtualKeys.Control => VirtualKeys.LControl,
            VirtualKeys.Menu => VirtualKeys.LMenu,
            _ => overrideVk,
        };

        bool Down(uint k) => k == overrideVk ? overrideDown : _keyboard.IsDown(k);

        var m = Modifiers.None;
        if (Down(VirtualKeys.LControl) || Down(VirtualKeys.RControl)) m |= Modifiers.Ctrl;
        if (Down(VirtualKeys.LShift) || Down(VirtualKeys.RShift)) m |= Modifiers.Shift;
        if (Down(VirtualKeys.LMenu) || Down(VirtualKeys.RMenu)) m |= Modifiers.Alt;
        if (Down(VirtualKeys.LWin) || Down(VirtualKeys.RWin)) m |= Modifiers.Win;
        return m;
    }

    private void RaiseState() => StateChanged?.Invoke(State);
}
