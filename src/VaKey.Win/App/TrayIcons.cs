using System.Drawing.Drawing2D;
using System.Drawing.Text;
using VaKey.Win.Native;

namespace VaKey.Win.App;

/// <summary>Vẽ biểu tượng khay: "V" nền xanh (tiếng Việt), "E" nền đỏ (tiếng Anh).</summary>
internal static class TrayIcons
{
    public static readonly Color VietnameseColor = Color.FromArgb(0x15, 0x65, 0xC0);
    public static readonly Color EnglishColor = Color.FromArgb(0xC6, 0x28, 0x28);

    public static Icon Create(string letter, Color background, int size = 32)
    {
        using var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            float r = size * 0.22f;
            using var path = RoundedRect(new RectangleF(0, 0, size - 1, size - 1), r);
            using var brush = new SolidBrush(background);
            g.FillPath(brush, path);

            using var font = new Font("Segoe UI", size * 0.68f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(letter, font, Brushes.White, new RectangleF(0, size * 0.03f, size, size), sf);
        }

        var hIcon = bmp.GetHicon();
        try
        {
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(RectangleF rect, float radius)
    {
        float d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
