using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace FrostGlass
{
    internal static class Theme
    {
        public static readonly Color TextPrimary = Color.FromArgb(244, 246, 250);
        public static readonly Color TextSecondary = Color.FromArgb(162, 171, 186);
        public static readonly Color TextMuted = Color.FromArgb(114, 124, 140);

        public static readonly Color PanelTop = Color.FromArgb(27, 33, 44);
        public static readonly Color PanelBottom = Color.FromArgb(18, 22, 30);
        public static readonly Color PanelBorder = Color.FromArgb(52, 61, 76);

        public static readonly Color Accent = Color.FromArgb(122, 162, 247);
        public static readonly Color AccentHover = Color.FromArgb(145, 180, 252);
        public static readonly Color AccentMuted = Color.FromArgb(76, 103, 163);

        public static readonly Color ControlBackground = Color.FromArgb(36, 43, 56);
        public static readonly Color ControlHover = Color.FromArgb(46, 55, 70);
        public static readonly Color Divider = Color.FromArgb(37, 44, 57);

        public static readonly FontFamily UiFamily = ResolveFamily("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");
        public static readonly FontFamily DisplayFamily = ResolveFamily("Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI");

        private static FontFamily ResolveFamily(params string[] names)
        {
            foreach (string name in names)
            {
                try
                {
                    FontFamily family = new FontFamily(name);
                    return family;
                }
                catch
                {
                }
            }
            return FontFamily.GenericSansSerif;
        }

        public static Font Font(float points)
        {
            return new Font(UiFamily, points, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font Font(float points, FontStyle style)
        {
            return new Font(UiFamily, points, style, GraphicsUnit.Point);
        }

        public static Color Alpha(Color color, int alpha)
        {
            return Color.FromArgb(AppSettings.Clamp(alpha, 0, 255), color.R, color.G, color.B);
        }

        public static Color Mix(Color from, Color to, float amount)
        {
            amount = Math.Max(0f, Math.Min(1f, amount));
            return Color.FromArgb(
                (int)Math.Round(from.R + (to.R - from.R) * amount),
                (int)Math.Round(from.G + (to.G - from.G) * amount),
                (int)Math.Round(from.B + (to.B - from.B) * amount));
        }

        public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return path;

            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.X, bounds.Y, diameter, diameter);
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.X;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void ConfigureGraphics(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }

        public static int S(Control control, double value)
        {
            double dpi = 96.0;
            try
            {
                if (control != null && control.IsHandleCreated)
                    dpi = NativeMethods.GetDpiForWindow(control.Handle);
                else
                    dpi = NativeMethods.GetDpiForSystem();
            }
            catch
            {
                dpi = control != null ? control.DeviceDpi : 96.0;
            }
            if (dpi <= 0)
                dpi = 96.0;
            return (int)Math.Round(value * dpi / 96.0);
        }

        public static void DrawString(Graphics g, string text, Font font, Color color,
            Rectangle bounds, ContentAlignment alignment)
        {
            using (SolidBrush brush = new SolidBrush(color))
            using (StringFormat format = new StringFormat())
            {
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.FormatFlags = StringFormatFlags.NoWrap;
                switch (alignment)
                {
                    case ContentAlignment.TopLeft:
                        format.Alignment = StringAlignment.Near;
                        format.LineAlignment = StringAlignment.Near;
                        break;
                    case ContentAlignment.TopCenter:
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Near;
                        break;
                    case ContentAlignment.TopRight:
                        format.Alignment = StringAlignment.Far;
                        format.LineAlignment = StringAlignment.Near;
                        break;
                    case ContentAlignment.MiddleLeft:
                        format.Alignment = StringAlignment.Near;
                        format.LineAlignment = StringAlignment.Center;
                        break;
                    case ContentAlignment.MiddleCenter:
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Center;
                        break;
                    case ContentAlignment.MiddleRight:
                        format.Alignment = StringAlignment.Far;
                        format.LineAlignment = StringAlignment.Center;
                        break;
                    case ContentAlignment.BottomLeft:
                        format.Alignment = StringAlignment.Near;
                        format.LineAlignment = StringAlignment.Far;
                        break;
                    case ContentAlignment.BottomCenter:
                        format.Alignment = StringAlignment.Center;
                        format.LineAlignment = StringAlignment.Far;
                        break;
                    case ContentAlignment.BottomRight:
                        format.Alignment = StringAlignment.Far;
                        format.LineAlignment = StringAlignment.Far;
                        break;
                }
                g.DrawString(text, font, brush, bounds, format);
            }
        }
    }
}
