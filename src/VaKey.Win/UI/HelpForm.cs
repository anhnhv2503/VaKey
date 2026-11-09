namespace VaKey.Win.UI;

/// <summary>Bảng tra cứu phím Telex / VNI và mẹo sử dụng.</summary>
internal sealed class HelpForm : Form
{
    public HelpForm(Icon icon, string hotkey)
    {
        Text = "Hướng dẫn VaKey";
        Icon = icon;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(500, 470);
        MinimizeBox = false;
        MinimumSize = new Size(420, 380);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(Table("Telex", ("Gõ", 90), ("Kết quả", 90), ("Ví dụ", 260),
        [
            ["aa", "â", "caan → cân"],
            ["aw", "ă", "awn → ăn"],
            ["ee", "ê", "ddeem → đêm"],
            ["oo", "ô", "coo → cô"],
            ["ow", "ơ", "cowm → cơm"],
            ["uw  hoặc  w", "ư", "tuw, tw → tư"],
            ["uow", "ươ", "nguowif → người"],
            ["dd", "đ", "ddi → đi"],
            ["s", "dấu sắc", "cas → cá"],
            ["f", "dấu huyền", "caf → cà"],
            ["r", "dấu hỏi", "car → cả"],
            ["x", "dấu ngã", "cax → cã"],
            ["j", "dấu nặng", "caj → cạ"],
            ["z", "xóa dấu", "casz → ca"],
            ["gõ lặp", "lấy lại phím", "ass → as, aaa → aa, ww → w"],
        ]));
        tabs.TabPages.Add(Table("VNI", ("Phím", 90), ("Kết quả", 90), ("Ví dụ", 260),
        [
            ["1", "dấu sắc", "ca1 → cá"],
            ["2", "dấu huyền", "ca2 → cà"],
            ["3", "dấu hỏi", "ca3 → cả"],
            ["4", "dấu ngã", "ca4 → cã"],
            ["5", "dấu nặng", "ca5 → cạ"],
            ["6", "â ê ô", "ca6n → cân"],
            ["7", "ơ ư", "nguoi72 → người"],
            ["8", "ă", "a8n → ăn"],
            ["9", "đ", "d9i → đi"],
            ["0", "xóa dấu", "ca10 → ca"],
        ]));

        var tips = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            ScrollBars = ScrollBars.Vertical,
            Text = string.Join(Environment.NewLine + Environment.NewLine,
            [
                $"• Chuyển Việt/Anh: nhấn {hotkey}, hoặc click trái vào biểu tượng V/E ở khay hệ thống.",
                "• Dấu có thể gõ ở cuối từ: vieetj, vietej, viejt đều ra \"việt\".",
                "• Gõ sai dấu? Gõ phím dấu khác để thay, hoặc z (Telex) / 0 (VNI) để xóa dấu.",
                "• Từ tiếng Anh như window, class, password được giữ nguyên khi bật Kiểm tra chính tả. " +
                "Từ trùng với tiếng Việt (this → thí) thì thêm vào \"Từ tiếng Anh\" trong phần Mở rộng.",
                "• Không gõ được trong ứng dụng chạy quyền Admin: click phải biểu tượng khay → \"Chạy lại với quyền Admin\".",
                "• Muốn tắt hẳn bộ gõ trong một ứng dụng (game...): mở ứng dụng đó, click phải biểu tượng khay → \"Luôn tắt trong ...\".",
                "• Dấu bị gõ đúp: tắt các bộ gõ khác (Unikey, EVKey, OpenKey, bộ gõ tiếng Việt của Windows).",
            ]),
        };
        var tipsPage = new TabPage("Mẹo") { Padding = new Padding(12) };
        tipsPage.Controls.Add(tips);
        tabs.TabPages.Add(tipsPage);

        var close = new Button { Text = "Đóng", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(84, 30), Anchor = AnchorStyles.Right };
        var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(6) };
        bottom.Controls.Add(close);
        AcceptButton = close;
        CancelButton = close;

        Controls.Add(tabs);
        Controls.Add(bottom);
    }

    private static TabPage Table(string title, (string, int) c1, (string, int) c2, (string, int) c3, string[][] rows)
    {
        var list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Dock = DockStyle.Fill,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            Font = new Font("Segoe UI", 10f),
        };
        foreach (var (name, width) in new[] { c1, c2, c3 }) list.Columns.Add(name, width);
        foreach (var r in rows) list.Items.Add(new ListViewItem(r));

        var page = new TabPage(title) { Padding = new Padding(6) };
        page.Controls.Add(list);
        return page;
    }
}
