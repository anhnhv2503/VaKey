using VaKey.Core.Settings;
using VaKey.Win.Config;
using VaKey.Win.Input;

namespace VaKey.Win.UI;

/// <summary>Cửa sổ cài đặt (dựng bằng code, không cần designer).</summary>
internal sealed class SettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly ConfigStore _store;
    private readonly Action<AppConfig> _apply;

    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _encoding = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Enabled = false };
    private readonly RadioButton _toneNew = new() { Text = "Kiểu mới: hòa, thúy, khỏe", AutoSize = true };
    private readonly RadioButton _toneOld = new() { Text = "Kiểu cũ: hoà, thuý, khoẻ", AutoSize = true };
    private readonly CheckBox _spell = new() { Text = "Kiểm tra chính tả (không biến đổi từ tiếng Anh)", AutoSize = true };
    private readonly CheckBox _wToU = new() { Text = "Telex: phím w đứng riêng thành ư", AutoSize = true };
    private readonly ComboBox _hotkey = new() { DropDownStyle = ComboBoxStyle.DropDown, Width = 160 };
    private readonly CheckBox _startup = new() { Text = "Khởi động cùng Windows", AutoSize = true };
    private readonly CheckBox _admin = new() { Text = "Khởi động với quyền Admin (gõ được trong app Admin)", AutoSize = true };
    private readonly CheckBox _remember = new() { Text = "Nhớ trạng thái Việt/Anh theo từng ứng dụng", AutoSize = true };
    private readonly CheckBox _beep = new() { Text = "Âm báo khi chuyển chế độ", AutoSize = true };
    private readonly CheckBox _notify = new() { Text = "Thông báo nhỏ khi chuyển chế độ", AutoSize = true };
    private readonly CheckBox _log = new() { Text = "Ghi log chẩn đoán (không ghi nội dung gõ)", AutoSize = true };
    private readonly TextBox _excluded = MultiLine();
    private readonly TextBox _english = MultiLine();
    private readonly ErrorProvider _errors = new();

    public SettingsForm(AppConfig config, ConfigStore store, Action<AppConfig> apply, Icon icon)
    {
        _config = config;
        _store = store;
        _apply = apply;

        Text = "Cài đặt VaKey";
        Icon = icon;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        ShowInTaskbar = true;

        _method.Items.AddRange(["Telex", "VNI"]);
        _encoding.Items.Add("Unicode dựng sẵn");
        _hotkey.Items.AddRange(HotkeyDefinition.Presets);

        var tabs = new TabControl { Width = 520, Height = 420 };
        tabs.TabPages.Add(BuildTypingTab());
        tabs.TabPages.Add(BuildSystemTab());
        tabs.TabPages.Add(BuildAppsTab());

        var ok = new Button { Text = "OK", DialogResult = DialogResult.None, AutoSize = true };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = true };
        var applyBtn = new Button { Text = "Áp dụng", AutoSize = true };
        ok.Click += (_, _) =>
        {
            if (Commit()) Close();
        };
        applyBtn.Click += (_, _) => Commit();
        cancel.Click += (_, _) => Close();
        AcceptButton = ok;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(0, 8, 0, 0),
        };
        buttons.Controls.AddRange([applyBtn, cancel, ok]);

        var root = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        root.Controls.Add(tabs);
        root.Controls.Add(buttons);
        Controls.Add(root);

        LoadValues();
    }

    private TabPage BuildTypingTab()
    {
        var page = new TabPage("Kiểu gõ") { Padding = new Padding(10) };
        var grid = Grid();
        AddRow(grid, "Kiểu gõ:", _method);
        AddRow(grid, "Bảng mã:", _encoding);
        var tone = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true };
        tone.Controls.AddRange([_toneNew, _toneOld]);
        AddRow(grid, "Đặt dấu:", tone);
        AddRow(grid, "", _spell);
        AddRow(grid, "", _wToU);
        AddRow(grid, "Từ tiếng Anh\nkhông biến đổi:", _english);
        grid.Controls.Add(Hint("Mỗi dòng một từ, ví dụ: tax, if, this. Những từ này luôn được giữ nguyên."), 1, grid.RowCount++);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildSystemTab()
    {
        var page = new TabPage("Hệ thống") { Padding = new Padding(10) };
        var grid = Grid();
        AddRow(grid, "Phím chuyển Việt/Anh:", _hotkey);
        grid.Controls.Add(Hint("Ví dụ: Ctrl+Shift, Alt+Z, Ctrl+Space, Ctrl+Alt+V"), 1, grid.RowCount++);
        AddRow(grid, "", _startup);
        AddRow(grid, "", _admin);
        AddRow(grid, "", _beep);
        AddRow(grid, "", _notify);
        AddRow(grid, "", _log);

        var openFolder = new LinkLabel { Text = "Mở thư mục cấu hình", AutoSize = true };
        openFolder.LinkClicked += (_, _) =>
        {
            Directory.CreateDirectory(_store.Directory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_store.Directory) { UseShellExecute = true });
        };
        AddRow(grid, "", openFolder);
        page.Controls.Add(grid);
        return page;
    }

    private TabPage BuildAppsTab()
    {
        var page = new TabPage("Ứng dụng") { Padding = new Padding(10) };
        var grid = Grid();
        AddRow(grid, "", _remember);
        AddRow(grid, "Luôn tắt bộ gõ\ntrong:", _excluded);
        grid.Controls.Add(Hint("Mỗi dòng một tên tiến trình, ví dụ: game.exe, keepass.exe.\n" +
                               "Chế độ gửi phím cho từng ứng dụng: sửa profiles.json trong thư mục cấu hình."), 1, grid.RowCount++);
        page.Controls.Add(grid);
        return page;
    }

    private void LoadValues()
    {
        _method.SelectedIndex = _config.InputMethod == InputMethodKind.Vni ? 1 : 0;
        _encoding.SelectedIndex = 0;
        _toneNew.Checked = _config.ToneStyle == ToneStyle.New;
        _toneOld.Checked = _config.ToneStyle == ToneStyle.Old;
        _spell.Checked = _config.SpellCheck;
        _wToU.Checked = _config.WStandaloneToU;
        _hotkey.Text = _config.ToggleHotkey;
        _startup.Checked = _config.RunAtStartup;
        _admin.Checked = _config.RunAsAdmin;
        _remember.Checked = _config.RememberPerApp;
        _beep.Checked = _config.BeepOnToggle;
        _notify.Checked = _config.NotifyOnToggle;
        _log.Checked = _config.DiagnosticLogging;
        _excluded.Text = string.Join(Environment.NewLine, _config.ExcludedApps);
        _english.Text = string.Join(Environment.NewLine, _config.EnglishExceptions);
    }

    private bool Commit()
    {
        if (!HotkeyDefinition.TryParse(_hotkey.Text, out var hotkey))
        {
            _errors.SetError(_hotkey, "Phím tắt không hợp lệ. Cần ít nhất 2 phím bổ trợ, hoặc phím bổ trợ + một phím.");
            return false;
        }

        _errors.SetError(_hotkey, "");

        var c = _config.Clone();
        c.InputMethod = _method.SelectedIndex == 1 ? InputMethodKind.Vni : InputMethodKind.Telex;
        c.ToneStyle = _toneOld.Checked ? ToneStyle.Old : ToneStyle.New;
        c.SpellCheck = _spell.Checked;
        c.WStandaloneToU = _wToU.Checked;
        c.ToggleHotkey = hotkey.ToString();
        c.RunAtStartup = _startup.Checked;
        c.RunAsAdmin = _admin.Checked;
        c.RememberPerApp = _remember.Checked;
        c.BeepOnToggle = _beep.Checked;
        c.NotifyOnToggle = _notify.Checked;
        c.DiagnosticLogging = _log.Checked;
        c.ExcludedApps = Lines(_excluded).Select(AppConfig.NormalizeProcessName).Distinct().ToList();
        c.EnglishExceptions = Lines(_english).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        _apply(c);
        _config.RunAsAdmin = c.RunAsAdmin;
        _hotkey.Text = c.ToggleHotkey;
        return true;
    }

    private static IEnumerable<string> Lines(TextBox box) =>
        box.Lines.SelectMany(l => l.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    private static TableLayoutPanel Grid() => new()
    {
        ColumnCount = 2,
        Dock = DockStyle.Fill,
        AutoSize = true,
        ColumnStyles = { new ColumnStyle(SizeType.AutoSize), new ColumnStyle(SizeType.Percent, 100) },
    };

    private static void AddRow(TableLayoutPanel grid, string label, Control control)
    {
        int row = grid.RowCount++;
        if (label.Length > 0)
        {
            grid.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left | AnchorStyles.Top, Padding = new Padding(0, 6, 8, 0) }, 0, row);
        }

        control.Margin = new Padding(3, 4, 3, 4);
        grid.Controls.Add(control, 1, row);
    }

    private static Label Hint(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = SystemColors.GrayText,
        Margin = new Padding(3, 0, 3, 8),
    };

    private static TextBox MultiLine() => new()
    {
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
        Width = 300,
        Height = 110,
        AcceptsReturn = true,
    };

    protected override void Dispose(bool disposing)
    {
        if (disposing) _errors.Dispose();
        base.Dispose(disposing);
    }
}
