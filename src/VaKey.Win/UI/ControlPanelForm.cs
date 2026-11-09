using VaKey.Core.Settings;
using VaKey.Win.App;
using VaKey.Win.Config;
using VaKey.Win.Input;

namespace VaKey.Win.UI;

/// <summary>
/// Bảng điều khiển chính: chọn bảng mã, kiểu gõ, phím chuyển; phần "Mở rộng" chứa các tùy chọn khác.
/// Mọi thay đổi được áp dụng ngay.
/// </summary>
internal sealed class ControlPanelForm : Form
{
    private static readonly Color VColor = TrayIcons.VietnameseColor;
    private static readonly Color EColor = TrayIcons.EnglishColor;

    private readonly IAppController _app;
    private bool _loading;

    // Trạng thái
    private readonly Button _status = new()
    {
        FlatStyle = FlatStyle.Flat,
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 12f, FontStyle.Bold),
        Height = 46,
        Dock = DockStyle.Fill,
        Cursor = Cursors.Hand,
        Margin = new Padding(0, 0, 0, 2),
    };

    private readonly Label _statusHint = new()
    {
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(0, 0, 0, 8),
        Anchor = AnchorStyles.None,
    };

    // Điều khiển
    private readonly ComboBox _encoding = Combo();
    private readonly ComboBox _method = Combo();
    private readonly RadioButton _hkCtrlShift = Radio("Ctrl + Shift");
    private readonly RadioButton _hkAltZ = Radio("Alt + Z");
    private readonly RadioButton _hkCustom = Radio("Khác:");
    private readonly TextBox _hkText = new() { Width = 110, Margin = new Padding(0, 2, 0, 0) };

    // Mở rộng
    private readonly CheckBox _toneNew = Check("Đặt dấu kiểu mới (hòa, thúy)");
    private readonly CheckBox _spell = Check("Kiểm tra chính tả");
    private readonly CheckBox _wToU = Check("Telex: w đứng riêng thành ư");
    private readonly CheckBox _remember = Check("Nhớ Việt/Anh theo ứng dụng");
    private readonly CheckBox _startup = Check("Khởi động cùng Windows");
    private readonly CheckBox _admin = Check("Khởi động với quyền Admin");
    private readonly CheckBox _beep = Check("Âm báo khi chuyển chế độ");
    private readonly CheckBox _notify = Check("Thông báo khi chuyển chế độ");
    private readonly CheckBox _showAtLogin = Check("Hiện bảng này khi khởi động");
    private readonly GroupBox _extended = new() { Text = "Tùy chọn", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly Button _expand = Btn("Mở rộng ▾");
    private readonly ToolTip _tips = new();

    public ControlPanelForm(IAppController app, bool expanded = false)
    {
        _app = app;

        Text = $"VaKey {UpdateChecker.CurrentVersion.ToString(2)} - Bảng điều khiển";
        Icon = app.AppIcon;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12, 10, 12, 10);
        Font = new Font("Segoe UI", 9f);
        KeyPreview = true;

        _encoding.Items.Add("Unicode dựng sẵn");
        _encoding.Enabled = false; // bản 1.0 chỉ có Unicode
        _method.Items.AddRange(["Telex", "VNI"]);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill, Width = 460 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 460));
        root.Controls.Add(_status);
        root.Controls.Add(_statusHint);
        root.Controls.Add(BuildControlGroup());
        root.Controls.Add(BuildExtendedGroup());
        root.Controls.Add(BuildButtons());
        Controls.Add(root);

        _status.Click += (_, _) => _app.ToggleVietnamese();
        _method.SelectedIndexChanged += (_, _) => Commit();
        _hkCtrlShift.CheckedChanged += (_, _) => { if (_hkCtrlShift.Checked) Commit(); };
        _hkAltZ.CheckedChanged += (_, _) => { if (_hkAltZ.Checked) Commit(); };
        _hkCustom.CheckedChanged += (_, _) =>
        {
            _hkText.Enabled = _hkCustom.Checked;
            if (_hkCustom.Checked)
            {
                _hkText.Focus();
                Commit();
            }
        };
        _hkText.Leave += (_, _) => Commit();
        _hkText.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Commit();
            }
        };
        foreach (var cb in new[] { _toneNew, _spell, _wToU, _remember, _startup, _admin, _beep, _notify, _showAtLogin })
        {
            cb.CheckedChanged += (_, _) => Commit();
        }

        _tips.SetToolTip(_spell, "Không biến đổi khi từ không thể là tiếng Việt (window, class, password...)");
        _tips.SetToolTip(_admin, "Cần để gõ được trong các ứng dụng chạy quyền Admin. Sẽ hỏi xác nhận UAC.");
        _tips.SetToolTip(_hkText, "Ví dụ: Ctrl+Space, Alt+Shift, Ctrl+Alt+V. Nhấn Enter để áp dụng.");
        _tips.SetToolTip(_status, "Nhấn để chuyển Việt/Anh");

        _app.StateChanged += OnStateChanged;
        _app.ConfigChanged += LoadValues;

        SetExpanded(expanded);
        LoadValues();
        OnStateChanged(_app.State);
    }

    private GroupBox BuildControlGroup()
    {
        var group = new GroupBox { Text = "Điều khiển", AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(8) };
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        AddRow(grid, "Bảng mã:", _encoding);
        AddRow(grid, "Kiểu gõ:", _method);

        var hk = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        hk.Controls.AddRange([_hkCtrlShift, _hkAltZ, _hkCustom, _hkText]);
        AddRow(grid, "Phím chuyển:", hk);

        group.Controls.Add(grid);
        return group;
    }

    private GroupBox BuildExtendedGroup()
    {
        var grid = new TableLayoutPanel { ColumnCount = 2, AutoSize = true, Dock = DockStyle.Fill };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        grid.Controls.AddRange([_toneNew, _spell, _wToU, _remember, _startup, _admin, _beep, _notify, _showAtLogin]);

        var advanced = new LinkLabel { Text = "Ứng dụng loại trừ, từ tiếng Anh, log...", AutoSize = true, Margin = new Padding(3, 6, 3, 3) };
        advanced.LinkClicked += (_, _) =>
        {
            _app.ShowAdvancedSettings(this);
            LoadValues();
        };
        grid.Controls.Add(advanced);

        _extended.Controls.Add(grid);
        return _extended;
    }

    private FlowLayoutPanel BuildButtons()
    {
        var close = Btn("Đóng");
        var exit = Btn("Kết thúc");
        var help = Btn("Hướng dẫn");
        var about = Btn("Thông tin");

        close.Click += (_, _) => Close();
        exit.Click += (_, _) => _app.Exit();
        _expand.Click += (_, _) => SetExpanded(!_extended.Visible);
        help.Click += (_, _) => _app.ShowHelp(this);
        about.Click += (_, _) => _app.ShowAbout(this);

        _tips.SetToolTip(close, "Ẩn bảng điều khiển, VaKey vẫn chạy ở khay hệ thống");
        _tips.SetToolTip(exit, "Thoát hẳn VaKey");

        AcceptButton = close;
        CancelButton = close;

        var panel = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 8, 0, 0),
        };
        panel.Controls.AddRange([close, exit, _expand, help, about]);
        return panel;
    }

    private void SetExpanded(bool expanded)
    {
        _extended.Visible = expanded;
        _expand.Text = expanded ? "Thu gọn ▴" : "Mở rộng ▾";
    }

    private void LoadValues()
    {
        if (IsDisposed) return;
        _loading = true;
        try
        {
            var c = _app.Config;
            _encoding.SelectedIndex = 0;
            _method.SelectedIndex = c.InputMethod == InputMethodKind.Vni ? 1 : 0;

            var hk = HotkeyDefinition.ParseOrDefault(c.ToggleHotkey).ToString();
            _hkCtrlShift.Checked = hk == "Ctrl+Shift";
            _hkAltZ.Checked = hk == "Alt+Z";
            _hkCustom.Checked = !_hkCtrlShift.Checked && !_hkAltZ.Checked;
            _hkText.Text = _hkCustom.Checked ? hk : "";
            _hkText.Enabled = _hkCustom.Checked;

            _toneNew.Checked = c.ToneStyle == ToneStyle.New;
            _spell.Checked = c.SpellCheck;
            _wToU.Checked = c.WStandaloneToU;
            _wToU.Enabled = c.InputMethod == InputMethodKind.Telex;
            _remember.Checked = c.RememberPerApp;
            _startup.Checked = c.RunAtStartup;
            _admin.Checked = c.RunAsAdmin;
            _beep.Checked = c.BeepOnToggle;
            _notify.Checked = c.NotifyOnToggle;
            _showAtLogin.Checked = c.ShowPanelAtLogin;
            UpdateHint();
        }
        finally
        {
            _loading = false;
        }
    }

    private void Commit()
    {
        if (_loading) return;

        string hotkey = _app.Config.ToggleHotkey;
        if (_hkCtrlShift.Checked) hotkey = "Ctrl+Shift";
        else if (_hkAltZ.Checked) hotkey = "Alt+Z";
        else if (_hkCustom.Checked && _hkText.Text.Trim().Length > 0)
        {
            if (HotkeyDefinition.TryParse(_hkText.Text, out var parsed))
            {
                hotkey = parsed.ToString();
                _hkText.BackColor = SystemColors.Window;
            }
            else
            {
                _hkText.BackColor = Color.MistyRose;
            }
        }

        var c = _app.Config.Clone();
        c.InputMethod = _method.SelectedIndex == 1 ? InputMethodKind.Vni : InputMethodKind.Telex;
        c.ToggleHotkey = hotkey;
        c.ToneStyle = _toneNew.Checked ? ToneStyle.New : ToneStyle.Old;
        c.SpellCheck = _spell.Checked;
        c.WStandaloneToU = _wToU.Checked;
        c.RememberPerApp = _remember.Checked;
        c.RunAtStartup = _startup.Checked;
        c.RunAsAdmin = _admin.Checked;
        c.BeepOnToggle = _beep.Checked;
        c.NotifyOnToggle = _notify.Checked;
        c.ShowPanelAtLogin = _showAtLogin.Checked;
        _app.ApplyConfig(c); // phát ConfigChanged → LoadValues đồng bộ lại (vd. khi UAC bị từ chối)
    }

    private void OnStateChanged(InputState s)
    {
        if (IsDisposed) return;
        bool on = s.Effective;
        _status.Text = on ? "V   Tiếng Việt" : s.Excluded ? "E   Đang tắt trong ứng dụng" : "E   English";
        _status.BackColor = on ? VColor : EColor;
        _status.FlatAppearance.BorderColor = _status.BackColor;
        _status.FlatAppearance.MouseOverBackColor = ControlPaint.Light(_status.BackColor, 0.15f);
        UpdateHint();
    }

    private void UpdateHint()
    {
        var hk = HotkeyDefinition.ParseOrDefault(_app.Config.ToggleHotkey);
        var app = _app.State.Process;
        _statusHint.Text = $"Nhấn {hk.ToString().Replace("+", " + ")} hoặc bấm nút trên để chuyển Việt/Anh" +
                           (app is null ? "" : $"\nỨng dụng hiện tại: {app}");
        _statusHint.TextAlign = ContentAlignment.MiddleCenter;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _app.StateChanged -= OnStateChanged;
        _app.ConfigChanged -= LoadValues;
        base.OnFormClosed(e);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _tips.Dispose();
        base.Dispose(disposing);
    }

    // ------------------------------------------------------------------ helpers

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        grid.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 12, 6),
        }, 0, row);
        control.Anchor = AnchorStyles.Left;
        grid.Controls.Add(control, 1, row);
    }

    private static ComboBox Combo() => new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };

    private static RadioButton Radio(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(0, 3, 10, 0) };

    private static CheckBox Check(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 4, 3, 4) };

    private static Button Btn(string text) => new()
    {
        Text = text,
        AutoSize = true,
        MinimumSize = new Size(84, 30),
        Margin = new Padding(3, 0, 3, 0),
    };
}
