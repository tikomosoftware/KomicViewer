using System.Drawing;
using System.Drawing.Drawing2D;

namespace KomicViewer.Services;

/// <summary>
/// SVGのパスデータを用いたベクターアイコンを描画し、Bitmapとして提供するサービス。
/// SkiaSharpがプロジェクトにあるが、WinFormsの標準的なGraphicsでも十分な描画が可能なため、
/// 依存度を抑えるためにSystem.Drawingを使用。
/// </summary>
public static class IconService
{
    public enum IconType
    {
        File,
        Help,
        ThemeDark,
        ThemeLight,
        TopMostOn,
        TopMostOff,
        Language,
        Fullscreen,
        Restore,
        ViewSingle,
        ViewDual
    }

    public static Bitmap GetIcon(IconType type, int size, Color color)
    {
        var bmp = new Bitmap(size, size);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var pen = new Pen(color, size / 12f);
            using var brush = new SolidBrush(color);

            float padding = size * 0.15f;
            float innerSize = size - (padding * 2);
            var rect = new RectangleF(padding, padding, innerSize, innerSize);

            switch (type)
            {
                case IconType.File:
                    DrawFileIcon(g, rect, brush, pen);
                    break;
                case IconType.Help:
                    DrawHelpIcon(g, rect, brush, pen);
                    break;
                case IconType.ThemeDark:
                    DrawMoonIcon(g, rect, brush, pen);
                    break;
                case IconType.ThemeLight:
                    DrawSunIcon(g, rect, brush, pen);
                    break;
                case IconType.TopMostOn:
                    DrawPinnedIcon(g, rect, brush, pen);
                    break;
                case IconType.TopMostOff:
                    DrawUnpinnedIcon(g, rect, brush, pen);
                    break;
                case IconType.Language:
                    DrawLanguageIcon(g, rect, brush, pen);
                    break;
                case IconType.Fullscreen:
                    DrawFullscreenIcon(g, rect, brush, pen);
                    break;
                case IconType.Restore:
                    DrawRestoreIcon(g, rect, brush, pen);
                    break;
                case IconType.ViewSingle:
                    DrawViewSingleIcon(g, rect, brush, pen);
                    break;
                case IconType.ViewDual:
                    DrawViewDualIcon(g, rect, brush, pen);
                    break;
            }
        }
        return bmp;
    }

    private static void DrawFileIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        var path = new GraphicsPath();
        path.AddLine(rect.Left, rect.Bottom, rect.Left, rect.Top + rect.Height * 0.2f);
        path.AddLine(rect.Left, rect.Top + rect.Height * 0.2f, rect.Left + rect.Width * 0.4f, rect.Top + rect.Height * 0.2f);
        path.AddLine(rect.Left + rect.Width * 0.4f, rect.Top + rect.Height * 0.2f, rect.Left + rect.Width * 0.5f, rect.Top);
        path.AddLine(rect.Left + rect.Width * 0.5f, rect.Top, rect.Right, rect.Top);
        path.AddLine(rect.Right, rect.Top, rect.Right, rect.Bottom);
        path.CloseAllFigures();
        g.DrawPath(pen, path);
    }

    private static void DrawHelpIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        g.DrawEllipse(pen, rect);
        var font = new Font("Arial", rect.Height * 0.7f, FontStyle.Bold);
        var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        g.DrawString("?", font, brush, new RectangleF(rect.X, rect.Y + 1, rect.Width, rect.Height), format);
    }

    private static void DrawMoonIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        var path = new GraphicsPath();
        path.AddArc(rect, 45, 270);
        path.AddArc(rect.X + rect.Width * 0.2f, rect.Y, rect.Width, rect.Height, 315, -270);
        path.CloseAllFigures();
        g.FillPath(brush, path);
    }

    private static void DrawSunIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        float centerSize = rect.Width * 0.4f;
        g.FillEllipse(brush, rect.X + (rect.Width - centerSize) / 2, rect.Y + (rect.Height - centerSize) / 2, centerSize, centerSize);
        for (int i = 0; i < 8; i++)
        {
            var state = g.Save();
            g.TranslateTransform(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
            g.RotateTransform(i * 45);
            g.DrawLine(pen, 0, -rect.Height * 0.3f, 0, -rect.Height * 0.5f);
            g.Restore(state);
        }
    }

    private static void DrawPinnedIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        float cx = rect.X + rect.Width / 2;
        float cy = rect.Y + rect.Height / 2;
        g.FillRectangle(brush, cx - rect.Width * 0.2f, cy - rect.Height * 0.3f, rect.Width * 0.4f, rect.Height * 0.4f);
        g.DrawLine(pen, cx, cy + rect.Height * 0.1f, cx, cy + rect.Height * 0.5f);
        g.DrawLine(pen, cx - rect.Width * 0.3f, cy - rect.Height * 0.3f, cx + rect.Width * 0.3f, cy - rect.Height * 0.3f);
    }

    private static void DrawUnpinnedIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        var state = g.Save();
        g.TranslateTransform(rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
        g.RotateTransform(45);
        var localRect = new RectangleF(-rect.Width / 2, -rect.Height / 2, rect.Width, rect.Height);
        DrawPinnedIcon(g, localRect, brush, pen);
        g.Restore(state);
    }

    private static void DrawLanguageIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        g.DrawEllipse(pen, rect);
        g.DrawEllipse(pen, rect.X + rect.Width * 0.3f, rect.Y, rect.Width * 0.4f, rect.Height);
        g.DrawLine(pen, rect.Left, rect.Y + rect.Height * 0.5f, rect.Right, rect.Y + rect.Height * 0.5f);
    }

    private static void DrawFullscreenIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        float len = rect.Width * 0.3f;
        g.DrawLines(pen, [new PointF(rect.Left, rect.Top + len), new PointF(rect.Left, rect.Top), new PointF(rect.Left + len, rect.Top)]);
        g.DrawLines(pen, [new PointF(rect.Right - len, rect.Top), new PointF(rect.Right, rect.Top), new PointF(rect.Right, rect.Top + len)]);
        g.DrawLines(pen, [new PointF(rect.Left, rect.Bottom - len), new PointF(rect.Left, rect.Bottom), new PointF(rect.Left + len, rect.Bottom)]);
        g.DrawLines(pen, [new PointF(rect.Right - len, rect.Bottom), new PointF(rect.Right, rect.Bottom), new PointF(rect.Right, rect.Bottom - len)]);
    }

    private static void DrawRestoreIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        float offset = rect.Width * 0.2f;
        var r1 = new RectangleF(rect.X, rect.Y + offset, rect.Width - offset, rect.Height - offset);
        var r2 = new RectangleF(rect.X + offset, rect.Y, rect.Width - offset, rect.Height - offset);
        g.DrawRectangle(pen, r1.X, r1.Y, r1.Width, r1.Height);
        g.FillRectangle(new SolidBrush(Color.FromArgb(100, 100, 100)), r2.X, r2.Y, r2.Width, r2.Height);
        g.DrawRectangle(pen, r2.X, r2.Y, r2.Width, r2.Height);
    }

    private static void DrawViewSingleIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        float padding = rect.Width * 0.15f;
        g.DrawRectangle(pen, rect.X + padding, rect.Y + padding, rect.Width - padding * 2, rect.Height - padding * 2);
    }

    private static void DrawViewDualIcon(Graphics g, RectangleF rect, Brush brush, Pen pen)
    {
        float padding = rect.Width * 0.1f;
        float mid = rect.Width * 0.05f;
        float w = (rect.Width - padding * 2 - mid) / 2;
        g.DrawRectangle(pen, rect.X + padding, rect.Y + padding, w, rect.Height - padding * 2);
        g.DrawRectangle(pen, rect.X + padding + w + mid, rect.Y + padding, w, rect.Height - padding * 2);
    }
}
