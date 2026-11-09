using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using VaKey.Win.Native;
using static VaKey.Win.Native.NativeMethods;

namespace VaKey.Win.Input;

/// <summary>
/// Luồng riêng có message loop, giữ hook bàn phím (WH_KEYBOARD_LL), hook chuột (WH_MOUSE_LL) và
/// hook đổi cửa sổ (SetWinEventHook). Mọi lời gọi tới <see cref="KeyProcessor"/> đều diễn ra trên luồng này.
/// </summary>
public sealed class HookThread : IDisposable
{
    private const uint WM_INVOKE = WM_APP + 1;
    private const uint HealthCheckMs = 5000;

    private readonly KeyProcessor _processor;
    private readonly ConcurrentQueue<Action> _queue = new();
    private readonly ManualResetEventSlim _ready = new();
    private readonly ProcessInfoCache _processes = new();

    // Giữ tham chiếu delegate để GC không thu hồi khi Windows còn gọi vào.
    private readonly LowLevelProc _keyboardProc;
    private readonly LowLevelProc _mouseProc;
    private readonly WinEventProc _winEventProc;

    private Thread? _thread;
    private uint _threadId;
    private IntPtr _keyboardHook;
    private IntPtr _mouseHook;
    private IntPtr _winEventHook;
    private volatile int _lastHookTick;
    private Exception? _startError;

    public HookThread(KeyProcessor processor)
    {
        _processor = processor;
        _keyboardProc = KeyboardProc;
        _mouseProc = MouseProc;
        _winEventProc = WinEventCallback;
    }

    /// <summary>Phát (trên luồng hook) khi ứng dụng foreground chạy quyền admin còn VaKey thì không.</summary>
    public event Action<string>? ElevatedAppFocused;

    public void Start()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "VaKey.Hook",
            Priority = ThreadPriority.AboveNormal,
        };
        _thread.SetApartmentState(ApartmentState.STA); // cần cho chế độ gửi qua Clipboard
        _thread.Start();
        _ready.Wait();
        if (_startError is not null) throw new InvalidOperationException("Không cài được hook bàn phím", _startError);
    }

    /// <summary>Chạy một hành động trên luồng hook (an toàn từ luồng UI).</summary>
    public void Invoke(Action action)
    {
        _queue.Enqueue(action);
        PostThreadMessageW(_threadId, WM_INVOKE, 0, 0);
    }

    public void Dispose()
    {
        if (_thread is null) return;
        PostThreadMessageW(_threadId, WM_QUIT, 0, 0);
        _thread.Join(2000);
        _thread = null;
        _ready.Dispose();
    }

    private void Run()
    {
        _threadId = GetCurrentThreadId();
        try
        {
            InstallInputHooks();
            _winEventHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero,
                _winEventProc, 0, 0, WINEVENT_OUTOFCONTEXT);
            HandleForeground(GetForegroundWindow());
        }
        catch (Exception ex)
        {
            _startError = ex;
            _ready.Set();
            return;
        }

        var timer = SetTimer(IntPtr.Zero, 0, HealthCheckMs, IntPtr.Zero);
        _ready.Set();

        while (GetMessageW(out var msg, IntPtr.Zero, 0, 0) > 0)
        {
            if (msg.hwnd == IntPtr.Zero && msg.message == WM_TIMER)
            {
                CheckHookHealth();
                continue;
            }

            if (msg.hwnd == IntPtr.Zero && msg.message == WM_INVOKE)
            {
                DrainQueue();
                continue;
            }

            TranslateMessage(ref msg);
            DispatchMessageW(ref msg);
        }

        KillTimer(IntPtr.Zero, timer);
        UninstallInputHooks();
        if (_winEventHook != IntPtr.Zero) UnhookWinEvent(_winEventHook);
        Diagnostics.Log.Info("Đã gỡ hook");
    }

    private void DrainQueue()
    {
        while (_queue.TryDequeue(out var action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Error("Lỗi khi xử lý lệnh trên luồng hook", ex);
            }
        }
    }

    private void InstallInputHooks()
    {
        var module = GetModuleHandleW(null);
        var kb = SetWindowsHookExW(WH_KEYBOARD_LL, _keyboardProc, module, 0);
        if (kb == IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        var mouse = SetWindowsHookExW(WH_MOUSE_LL, _mouseProc, module, 0);

        // Cài hook mới trước rồi mới gỡ hook cũ để không có khoảng hở.
        var oldKb = _keyboardHook;
        var oldMouse = _mouseHook;
        _keyboardHook = kb;
        _mouseHook = mouse;
        if (oldKb != IntPtr.Zero) UnhookWindowsHookEx(oldKb);
        if (oldMouse != IntPtr.Zero) UnhookWindowsHookEx(oldMouse);
        _lastHookTick = Environment.TickCount;
        Diagnostics.Log.Info("Đã cài hook bàn phím/chuột");
    }

    private void UninstallInputHooks()
    {
        if (_keyboardHook != IntPtr.Zero) UnhookWindowsHookEx(_keyboardHook);
        if (_mouseHook != IntPtr.Zero) UnhookWindowsHookEx(_mouseHook);
        _keyboardHook = _mouseHook = IntPtr.Zero;
    }

    /// <summary>
    /// Windows âm thầm gỡ hook LL nếu callback quá chậm. Nếu hệ thống có input mới mà hook không nhận được
    /// gì trong một khoảng thời gian, cài lại hook.
    /// </summary>
    private void CheckHookHealth()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        if (!GetLastInputInfo(ref info)) return;
        int sinceHook = (int)info.dwTime - _lastHookTick;
        if (sinceHook > 2000)
        {
            Diagnostics.Log.Info("Hook có vẻ đã bị gỡ, cài lại");
            try
            {
                InstallInputHooks();
            }
            catch (Exception ex)
            {
                Diagnostics.Log.Error("Cài lại hook thất bại", ex);
            }
        }
    }

    private unsafe IntPtr KeyboardProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            _lastHookTick = Environment.TickCount;
            var data = (KBDLLHOOKSTRUCT*)lParam;
            bool injected = (data->flags & LLKHF_INJECTED) != 0 || data->dwExtraInfo == KeySender.Marker;
            if (!injected)
            {
                int msg = (int)wParam;
                bool down = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
                bool swallow;
                try
                {
                    swallow = _processor.OnKey(data->vkCode, down);
                }
                catch (Exception ex)
                {
                    // Không bao giờ để lỗi làm treo bàn phím: chuyển tiếp phím.
                    Diagnostics.Log.Error("Lỗi xử lý phím", ex);
                    _processor.Session.Reset();
                    swallow = false;
                }

                if (swallow) return 1;
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private unsafe IntPtr MouseProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            _lastHookTick = Environment.TickCount;
            int msg = (int)wParam;
            if (msg is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN or WM_XBUTTONDOWN or WM_NCLBUTTONDOWN)
            {
                var data = (MSLLHOOKSTRUCT*)lParam;
                if ((data->flags & LLMHF_INJECTED) == 0) _processor.OnMouseDown();
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private void WinEventCallback(IntPtr hook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint thread, uint time)
    {
        try
        {
            HandleForeground(hwnd);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Error("Lỗi khi đổi cửa sổ", ex);
        }
    }

    private void HandleForeground(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            _processor.OnForegroundChanged(null);
            return;
        }

        GetWindowThreadProcessId(hwnd, out uint pid);
        var info = _processes.Get(pid);
        _processor.OnForegroundChanged(info.Name);
        if (info.Elevated && !ProcessInfoCache.SelfElevated && info.Name is not null)
        {
            ElevatedAppFocused?.Invoke(info.Name);
        }
    }
}
