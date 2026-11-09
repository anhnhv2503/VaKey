using System.Media;
using VaKey.Core.Settings;
using VaKey.Win.Config;
using VaKey.Win.Diagnostics;
using VaKey.Win.Input;
using VaKey.Win.Profiles;
using VaKey.Win.UI;

namespace VaKey.Win.App;

/// <summary>Vòng đời ứng dụng: tray icon, menu, bảng điều khiển, cấu hình, luồng hook.</summary>
internal sealed class VaKeyApp : ApplicationContext, IAppController
{
    private readonly ConfigStore _store;
    private readonly WindowsFormsSynchronizationContext _ui;
    private readonly KeyProcessor _processor;
    private readonly HookThread _hook;
    private readonly NotifyIcon _tray;
    private readonly Icon _iconV;
    private readonly Icon _iconE;
    private readonly Icon _appIcon;
    private readonly HashSet<string> _elevatedWarned = new(StringComparer.OrdinalIgnoreCase);
    private readonly System.Windows.Forms.Timer _trimTimer;
    private readonly EventWaitHandle _showSignal;
    private readonly RegisteredWaitHandle _showWait;

    private AppConfig _config;
    private ProfileSet _profiles;
    private ControlPanelForm? _panel;
    private SettingsForm? _settings;

    private readonly ToolStripMenuItem _miPanel = new("Bảng điều khiển...");
    private readonly ToolStripMenuItem _miVietnamese = new();
    private readonly ToolStripMenuItem _miTelex = new("Telex");
    private readonly ToolStripMenuItem _miVni = new("VNI");
    private readonly ToolStripMenuItem _miToneNew = new("Kiểu mới (hòa, thúy)");
    private readonly ToolStripMenuItem _miToneOld = new("Kiểu cũ (hoà, thuý)");
    private readonly ToolStripMenuItem _miSpell = new("Kiểm tra chính tả");
    private readonly ToolStripMenuItem _miStartup = new("Khởi động cùng Windows");
    private readonly ToolStripMenuItem _miExcludeCurrent = new();

    public VaKeyApp(string[] args)
    {
        _ui = new WindowsFormsSynchronizationContext();
        _store = ConfigStore.CreateDefault();
        bool firstRun = !_store.Exists;
        _config = _store.Load();
        if (firstRun)
        {
            // Lần chạy đầu: tôn trọng lựa chọn "Khởi động cùng Windows" của trình cài đặt.
            _config.RunAtStartup = SafeIsRunEnabled();
        }

        Log.Configure(_config.DiagnosticLogging, _store.LogDirectory);
        Log.Info($"VaKey {UpdateChecker.CurrentVersion} khởi động, portable={_store.IsPortable}");

        _profiles = LoadProfiles();

        _appIcon = LoadAppIcon();
        _iconV = TrayIcons.Create("V", TrayIcons.VietnameseColor);
        _iconE = TrayIcons.Create("E", TrayIcons.EnglishColor);

        _tray = new NotifyIcon
        {
            Icon = _config.Vietnamese ? _iconV : _iconE,
            Text = "VaKey",
            ContextMenuStrip = BuildMenu(),
            Visible = true,
        };
        _tray.MouseClick += (_, e) =>
        {
            if (e.Button == MouseButtons.Left) ToggleVietnamese();
        };
        _tray.BalloonTipClicked += (_, _) => ShowPanel();

        var keyboard = new Win32KeyboardState();
        _processor = new KeyProcessor(new KeySender(keyboard), keyboard, _config, _profiles)
        {
            OwnProcess = AppConfig.NormalizeProcessName(Environment.ProcessPath ?? "vakey.exe"),
        };
        _processor.StateChanged += s => _ui.Post(_ => OnStateChanged(s), null);
        _processor.Toggled += on => _ui.Post(_ => OnUserToggled(on), null);

        _hook = new HookThread(_processor);
        _hook.ElevatedAppFocused += name => _ui.Post(_ => WarnElevated(name), null);
        try
        {
            _hook.Start();
        }
        catch (InvalidOperationException ex)
        {
            Log.Error("Không khởi động được hook", ex);
            MessageBox.Show($"Không cài được hook bàn phím: {ex.InnerException?.Message}", "VaKey",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        OnStateChanged(_processor.State);
        SyncStartupRegistration();
        if (firstRun) Save();

        // Lần chạy thứ hai của VaKey.exe (người dùng mở lại từ Start menu) → hiện bảng điều khiển.
        _showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, Program.ShowPanelEventName);
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showSignal,
            (_, _) => _ui.Post(_ => ShowPanel(), null), null, Timeout.Infinite, executeOnlyOnce: false);

        // Giảm bộ nhớ khi nhàn rỗi: trả lại working set sau khi khởi động xong.
        _trimTimer = new System.Windows.Forms.Timer { Interval = 10_000 };
        _trimTimer.Tick += (_, _) =>
        {
            _trimTimer.Stop();
            TrimMemory();
        };
        _trimTimer.Start();

        bool atLogin = args.Contains("--startup");
        var conflicts = ConflictDetector.FindRunning();
        if (conflicts.Count > 0)
        {
            Balloon($"Phát hiện bộ gõ khác đang chạy: {string.Join(", ", conflicts)}. Hãy tắt chúng để tránh gõ đúp dấu.",
                ToolTipIcon.Warning);
        }

        if (!atLogin || _config.ShowPanelAtLogin) ShowPanel();
    }

    // ------------------------------------------------------------------ IAppController

    public AppConfig Config => _config;

    public InputState State => _processor.State;

    public ConfigStore Store => _store;

    public Icon AppIcon => _appIcon;

    public event Action<InputState>? StateChanged;

    public event Action? ConfigChanged;

    public void ToggleVietnamese() => _hook.Invoke(() => _processor.Toggle());

    /// <summary>Thay đổi cấu hình, lưu và áp dụng cho luồng hook.</summary>
    public void ApplyConfig(AppConfig updated)
    {
        bool adminChanged = updated.RunAsAdmin != _config.RunAsAdmin;
        updated.AppStates = _processor.SnapshotAppStates();
        updated.Vietnamese = _processor.Vietnamese;
        _config = updated;

        Log.Configure(_config.DiagnosticLogging, _store.LogDirectory);
        _profiles = LoadProfiles();
        var snapshot = _config.Clone();
        var profiles = _profiles;
        _hook.Invoke(() => _processor.Apply(snapshot, profiles));

        if (adminChanged && !StartupManager.SetAdminTask(_config.RunAsAdmin))
        {
            _config.RunAsAdmin = !_config.RunAsAdmin;
            Balloon("Không thay đổi được tác vụ khởi động quyền Admin (cần xác nhận UAC).", ToolTipIcon.Warning);
        }

        SyncStartupRegistration();
        Save();
        RefreshMenu();
        ConfigChanged?.Invoke();
    }

    public void ShowAdvancedSettings(IWin32Window owner)
    {
        if (_settings is { IsDisposed: false })
        {
            _settings.Activate();
            return;
        }

        using var form = new SettingsForm(_config.Clone(), _store, ApplyConfig, _appIcon);
        _settings = form;
        form.StartPosition = FormStartPosition.CenterParent;
        form.ShowDialog(owner);
        _settings = null;
    }

    public void ShowHelp(IWin32Window? owner)
    {
        using var help = new HelpForm(_appIcon, HotkeyDefinition.ParseOrDefault(_config.ToggleHotkey).ToString());
        if (owner is null) help.StartPosition = FormStartPosition.CenterScreen;
        help.ShowDialog(owner);
    }

    public void ShowAbout(IWin32Window? owner)
    {
        MessageBox.Show(owner,
            $"VaKey {UpdateChecker.CurrentVersion.ToString(3)}\n\n" +
            "Bộ gõ tiếng Việt miễn phí, mã nguồn mở cho Windows.\n" +
            "Telex, VNI - Unicode dựng sẵn.\n\n" +
            "Không lưu nội dung gõ, không kết nối mạng ngầm.\n\n" +
            $"Cấu hình: {_store.Directory}\n" +
            $"Trang dự án: {UpdateChecker.ReleasesUrl}",
            "Thông tin VaKey", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    public void Exit() => ExitThread();

    // ------------------------------------------------------------------ panel

    private void ShowPanel(bool expanded = false)
    {
        if (_panel is { IsDisposed: false })
        {
            if (_panel.WindowState == FormWindowState.Minimized) _panel.WindowState = FormWindowState.Normal;
            _panel.Activate();
            return;
        }

        _panel = new ControlPanelForm(this, expanded);
        _panel.FormClosed += (_, _) =>
        {
            _panel = null;
            TrimMemory();
        };
        _panel.Show();
        _panel.Activate();
    }

    // ------------------------------------------------------------------ menu

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        _miPanel.Font = new Font(_miPanel.Font, FontStyle.Bold);
        _miPanel.Click += (_, _) => ShowPanel();
        _miVietnamese.Click += (_, _) => ToggleVietnamese();

        _miTelex.Click += (_, _) => Update(c => c.InputMethod = InputMethodKind.Telex);
        _miVni.Click += (_, _) => Update(c => c.InputMethod = InputMethodKind.Vni);
        _miToneNew.Click += (_, _) => Update(c => c.ToneStyle = ToneStyle.New);
        _miToneOld.Click += (_, _) => Update(c => c.ToneStyle = ToneStyle.Old);
        _miSpell.Click += (_, _) => Update(c => c.SpellCheck = !c.SpellCheck);
        _miStartup.Click += (_, _) => Update(c => c.RunAtStartup = !c.RunAtStartup);
        _miExcludeCurrent.Click += (_, _) => ToggleExcludeCurrent();

        var tone = new ToolStripMenuItem("Đặt dấu");
        tone.DropDownItems.AddRange([_miToneNew, _miToneOld]);

        var restartAdmin = new ToolStripMenuItem("Chạy lại với quyền Admin", null, (_, _) => RestartAsAdmin())
        {
            Visible = !ProcessInfoCache.SelfElevated,
        };

        menu.Items.AddRange(
        [
            _miPanel,
            _miVietnamese,
            new ToolStripSeparator(),
            _miTelex,
            _miVni,
            tone,
            _miSpell,
            new ToolStripSeparator(),
            _miExcludeCurrent,
            _miStartup,
            restartAdmin,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Hướng dẫn", null, (_, _) => ShowHelp(null)),
            new ToolStripMenuItem("Kiểm tra cập nhật", null, async (_, _) => await CheckUpdatesAsync()),
            new ToolStripMenuItem("Thông tin", null, (_, _) => ShowAbout(null)),
            new ToolStripMenuItem("Kết thúc", null, (_, _) => ExitThread()),
        ]);

        menu.Opening += (_, _) => RefreshMenu();
        RefreshMenu();
        return menu;
    }

    private void RefreshMenu()
    {
        var state = _processor?.State ?? new InputState(_config.Vietnamese, _config.Vietnamese, null, false);
        _miVietnamese.Text = $"Tiếng Việt ({_config.ToggleHotkey})";
        _miVietnamese.Checked = state.Vietnamese;
        _miTelex.Checked = _config.InputMethod == InputMethodKind.Telex;
        _miVni.Checked = _config.InputMethod == InputMethodKind.Vni;
        _miToneNew.Checked = _config.ToneStyle == ToneStyle.New;
        _miToneOld.Checked = _config.ToneStyle == ToneStyle.Old;
        _miSpell.Checked = _config.SpellCheck;
        _miStartup.Checked = _config.RunAtStartup;

        var process = state.Process;
        _miExcludeCurrent.Visible = process is not null;
        _miExcludeCurrent.Text = $"Luôn tắt trong {process}";
        _miExcludeCurrent.Checked = state.Excluded;
    }

    private void ToggleExcludeCurrent()
    {
        var process = _processor.State.Process;
        if (process is null) return;
        Update(c =>
        {
            if (c.ExcludedApps.RemoveAll(p => AppConfig.NormalizeProcessName(p) == process) == 0) c.ExcludedApps.Add(process);
        });
    }

    private void Update(Action<AppConfig> change)
    {
        var updated = _config.Clone();
        change(updated);
        ApplyConfig(updated);
    }

    // ------------------------------------------------------------------ state

    private void OnStateChanged(InputState s)
    {
        _tray.Icon = s.Effective ? _iconV : _iconE;
        string mode = s.Effective
            ? $"Tiếng Việt ({(_config.InputMethod == InputMethodKind.Telex ? "Telex" : "VNI")})"
            : s.Excluded ? "Tắt trong ứng dụng này" : "English";
        _tray.Text = Truncate($"VaKey - {mode}", 127);
        StateChanged?.Invoke(s);
    }

    private void OnUserToggled(bool on)
    {
        if (_config.BeepOnToggle)
        {
            (on ? SystemSounds.Asterisk : SystemSounds.Beep).Play();
        }

        if (_config.NotifyOnToggle)
        {
            Balloon(on ? "Tiếng Việt" : "English", ToolTipIcon.None, 800);
        }

        _config.Vietnamese = on;
    }

    private void WarnElevated(string process)
    {
        if (!_elevatedWarned.Add(process)) return;
        Balloon($"{process} đang chạy với quyền Admin nên VaKey không gõ được trong đó. " +
                "Chọn \"Chạy lại với quyền Admin\" trong menu VaKey.", ToolTipIcon.Warning);
    }

    private void SyncStartupRegistration()
    {
        try
        {
            // Khi dùng tác vụ admin thì không đăng ký Run để tránh chạy hai lần.
            bool wantRun = _config.RunAtStartup && !_config.RunAsAdmin;
            if (wantRun != StartupManager.IsRunEnabled()) StartupManager.SetRunAtStartup(wantRun);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Log.Error("Không ghi được khóa Run", ex);
        }
    }

    private static bool SafeIsRunEnabled()
    {
        try
        {
            return StartupManager.IsRunEnabled();
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private void Save()
    {
        try
        {
            _config.AppStates = _processor.SnapshotAppStates();
            _config.Vietnamese = _processor.Vietnamese;
            _store.Save(_config);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Error("Không lưu được cấu hình", ex);
        }
    }

    private ProfileSet LoadProfiles() =>
        ProfileSet.Load(_store.ProfilesPath, Path.Combine(AppContext.BaseDirectory, "profiles.default.json"));

    private static Icon LoadAppIcon()
    {
        using var stream = typeof(VaKeyApp).Assembly.GetManifestResourceStream("VaKey.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }

    // ------------------------------------------------------------------ misc

    private async Task CheckUpdatesAsync()
    {
        try
        {
            var latest = await UpdateChecker.GetLatestVersionAsync();
            if (latest is null)
            {
                Balloon("Không kiểm tra được phiên bản mới.", ToolTipIcon.Warning);
            }
            else if (latest > UpdateChecker.CurrentVersion)
            {
                if (MessageBox.Show($"Đã có VaKey {latest}. Mở trang tải về?", "VaKey", MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(UpdateChecker.ReleasesUrl)
                    {
                        UseShellExecute = true,
                    });
                }
            }
            else
            {
                Balloon("Bạn đang dùng phiên bản mới nhất.", ToolTipIcon.Info);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException)
        {
            Balloon("Không kết nối được để kiểm tra cập nhật.", ToolTipIcon.Warning);
        }
    }

    private void RestartAsAdmin()
    {
        Save();
        if (StartupManager.RestartAsAdmin()) ExitThread();
    }

    private void Balloon(string text, ToolTipIcon icon, int timeout = 4000)
    {
        _tray.BalloonTipTitle = "VaKey";
        _tray.BalloonTipText = text;
        _tray.BalloonTipIcon = icon;
        _tray.ShowBalloonTip(timeout);
    }

    private static void TrimMemory()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        Native.NativeMethods.SetProcessWorkingSetSize(Native.NativeMethods.GetCurrentProcess(), -1, -1);
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];

    protected override void ExitThreadCore()
    {
        Save();
        _showWait.Unregister(null);
        _hook.Dispose();
        _tray.Visible = false;
        _panel?.Close();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tray.Dispose();
            _iconV.Dispose();
            _iconE.Dispose();
            _appIcon.Dispose();
            _trimTimer.Dispose();
            _showSignal.Dispose();
        }

        base.Dispose(disposing);
    }
}
