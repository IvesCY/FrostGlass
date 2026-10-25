using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FrostGlass
{
    /// <summary>
    /// The interactive caption bar. It is a separate window because the glass
    /// itself is always click-through, including its title area.
    /// </summary>
    internal sealed class PaneBar : Form
    {
        private enum DragMode { None, Move, North, West, East, NorthWest, NorthEast }

        private readonly FrostPane _pane;
        private readonly Font _font;
        private DragMode _dragMode;
        private Point _dragStartCursor;
        private Rectangle _dragStartPaneBounds;
        private int _hoverButton;
        private const byte IdleAlpha = 145;
        private const byte HoverAlpha = 230;

        public PaneBar(FrostPane pane)
        {
            _pane = pane;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = pane.TopMost;
            BackColor = Color.FromArgb(20, 25, 34);
            Text = "毛玻璃标题栏";
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            _font = Theme.Font(9.0f, FontStyle.Bold);
            SyncFromPane();
        }

        public int BarHeight
        {
            get { return Theme.S(this, 34); }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_NOACTIVATE;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                cp.ExStyle |= NativeMethods.WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetBarAlpha(IdleAlpha);
            SyncFromPane();
        }

        public void SyncFromPane()
        {
            Rectangle paneBounds = _pane.Bounds;
            Bounds = new Rectangle(paneBounds.Left, paneBounds.Top, paneBounds.Width, BarHeight);
            TopMost = _pane.TopMost;
            Invalidate();
        }

        private void SetBarAlpha(byte alpha)
        {
            if (!IsHandleCreated)
                return;
            try
            {
                NativeMethods.SetLayeredWindowAttributes(Handle, 0, alpha, NativeMethods.LWA_ALPHA);
            }
            catch
            {
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
                SetBarAlpha(IdleAlpha);
        }

        private Rectangle ButtonRect(int which)
        {
            int width = Theme.S(this, 30);
            int height = Theme.S(this, 26);
            int gap = Theme.S(this, 2);
            int right = Theme.S(this, 6);
            int top = (BarHeight - height) / 2;
            int closeX = ClientSize.Width - right - width;
            int minimizeX = closeX - gap - width;
            int settingsX = minimizeX - gap - width;
            if (which == 1) return new Rectangle(settingsX, top, width, height);
            if (which == 2) return new Rectangle(minimizeX, top, width, height);
            return new Rectangle(closeX, top, width, height);
        }

        private int ButtonAt(Point p)
        {
            if (ButtonRect(1).Contains(p)) return 1;
            if (ButtonRect(2).Contains(p)) return 2;
            if (ButtonRect(3).Contains(p)) return 3;
            return 0;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureGraphics(g);
            g.Clear(Color.FromArgb(20, 25, 34));

            Rectangle full = ClientRectangle;
            using (LinearGradientBrush fill = new LinearGradientBrush(full,
                Color.FromArgb(31, 37, 49), Color.FromArgb(18, 22, 30), LinearGradientMode.Vertical))
                g.FillRectangle(fill, full);

            using (Pen topLine = new Pen(Color.FromArgb(32, 255, 255, 255), 1))
                g.DrawLine(topLine, 1, 1, Width - 2, 1);
            using (Pen bottomLine = new Pen(Color.FromArgb(45, 255, 255, 255), 1))
                g.DrawLine(bottomLine, 1, Height - 2, Width - 2, Height - 2);

            int iconSize = Theme.S(this, 16);
            Rectangle logo = new Rectangle(Theme.S(this, 10), (BarHeight - iconSize) / 2,
                iconSize, iconSize);
            using (GraphicsPath logoPath = Theme.RoundedRect(logo, Theme.S(this, 4)))
            {
                using (LinearGradientBrush logoFill = new LinearGradientBrush(logo,
                    Color.FromArgb(135, 255, 255, 255), Color.FromArgb(40, 255, 255, 255),
                    LinearGradientMode.ForwardDiagonal))
                    g.FillPath(logoFill, logoPath);
                using (Pen logoBorder = new Pen(Color.FromArgb(100, 255, 255, 255), 1))
                    g.DrawPath(logoBorder, logoPath);
                using (Pen shine = new Pen(Color.FromArgb(130, 255, 255, 255), 1))
                    g.DrawLine(shine, logo.Left + Theme.S(this, 4), logo.Bottom - Theme.S(this, 4),
                        logo.Right - Theme.S(this, 4), logo.Top + Theme.S(this, 4));
            }

            int textLeft = logo.Right + Theme.S(this, 7);
            int textRight = ButtonRect(1).Left - Theme.S(this, 8);
            Rectangle titleRect = new Rectangle(textLeft, 0, Math.Max(0, textRight - textLeft), BarHeight);
            if (titleRect.Width > Theme.S(this, 20))
                Theme.DrawString(g, "毛玻璃", _font, Color.FromArgb(236, 246, 248, 252),
                    titleRect, ContentAlignment.MiddleLeft);

            DrawCaptionButton(g, 1);
            DrawCaptionButton(g, 2);
            DrawCaptionButton(g, 3);
        }

        private void DrawCaptionButton(Graphics g, int which)
        {
            Rectangle button = ButtonRect(which);
            bool hovered = _hoverButton == which;
            if (hovered)
            {
                Color fillColor = which == 3
                    ? Color.FromArgb(232, 196, 43, 28)
                    : Color.FromArgb(30, 255, 255, 255);
                using (GraphicsPath path = Theme.RoundedRect(button, Theme.S(this, 5)))
                using (SolidBrush fill = new SolidBrush(fillColor))
                    g.FillPath(fill, path);
            }

            int cx = button.Left + button.Width / 2;
            int cy = button.Top + button.Height / 2;
            Color iconColor = Color.FromArgb(which == 3 && hovered ? 255 : 225, 245, 247, 250);
            using (Pen pen = new Pen(iconColor, Math.Max(1f, Theme.S(this, 1.3f))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (which == 3)
                {
                    int h = Theme.S(this, 4);
                    g.DrawLine(pen, cx - h, cy - h, cx + h, cy + h);
                    g.DrawLine(pen, cx + h, cy - h, cx - h, cy + h);
                }
                else if (which == 2)
                {
                    int h = Theme.S(this, 5);
                    g.DrawLine(pen, cx - h, cy, cx + h, cy);
                }
                else
                {
                    int left = cx - Theme.S(this, 6);
                    int right = cx + Theme.S(this, 6);
                    int gap = Theme.S(this, 4);
                    int radius = Math.Max(1, Theme.S(this, 1.6f));
                    using (SolidBrush knob = new SolidBrush(iconColor))
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            int y = cy + i * gap;
                            g.DrawLine(pen, left, y, right, y);
                            int knobX = i == 0
                                ? left + Theme.S(this, 9)
                                : (i < 0 ? left + Theme.S(this, 4) : right - Theme.S(this, 4));
                            g.FillEllipse(knob, knobX - radius, y - radius, radius * 2, radius * 2);
                        }
                    }
                }
            }
        }

        private DragMode EdgeAt(Point p)
        {
            int border = Theme.S(this, 5);
            bool left = p.X <= border;
            bool right = p.X >= Width - border;
            bool top = p.Y <= border;
            if (top && left) return DragMode.NorthWest;
            if (top && right) return DragMode.NorthEast;
            if (top) return DragMode.North;
            if (left) return DragMode.West;
            if (right) return DragMode.East;
            return DragMode.Move;
        }

        private Cursor CursorFor(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.North: return Cursors.SizeNS;
                case DragMode.West:
                case DragMode.East: return Cursors.SizeWE;
                case DragMode.NorthWest:
                    return Cursors.SizeNWSE;
                case DragMode.NorthEast:
                    return Cursors.SizeNESW;
                default: return Cursors.Default;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            SetBarAlpha(HoverAlpha);
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverButton = 0;
            if (_dragMode == DragMode.None)
                SetBarAlpha(IdleAlpha);
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            int button = ButtonAt(e.Location);
            if (button != 0)
            {
                if (button == 1) _pane.OpenSettingsFromOverlay();
                else if (button == 2) _pane.MinimizeFromOverlay();
                else _pane.CloseFromOverlay();
                return;
            }

            _dragMode = EdgeAt(e.Location);
            _dragStartCursor = Cursor.Position;
            _dragStartPaneBounds = _pane.Bounds;
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragMode == DragMode.None)
            {
                int button = ButtonAt(e.Location);
                if (button != _hoverButton)
                {
                    _hoverButton = button;
                    Invalidate();
                }
                if (button != 0)
                    Cursor = Cursors.Hand;
                else
                    Cursor = CursorFor(EdgeAt(e.Location));
                return;
            }

            Point now = Cursor.Position;
            int dx = now.X - _dragStartCursor.X;
            int dy = now.Y - _dragStartCursor.Y;
            Rectangle bounds = _dragStartPaneBounds;
            int minWidth = Theme.S(this, 200);
            int minHeight = Theme.S(this, 90);

            if (_dragMode == DragMode.Move)
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                int keepX = Math.Min(Theme.S(this, 80), bounds.Width);
                int keepY = Math.Min(Theme.S(this, 50), bounds.Height);
                bounds.X = Math.Max(vs.Left - bounds.Width + keepX,
                    Math.Min(vs.Right - keepX, bounds.X + dx));
                bounds.Y = Math.Max(vs.Top - bounds.Height + keepY,
                    Math.Min(vs.Bottom - keepY, bounds.Y + dy));
            }
            else
            {
                bool west = _dragMode == DragMode.West || _dragMode == DragMode.NorthWest;
                bool east = _dragMode == DragMode.East || _dragMode == DragMode.NorthEast;
                bool north = _dragMode == DragMode.North || _dragMode == DragMode.NorthWest || _dragMode == DragMode.NorthEast;

                if (west)
                {
                    int newLeft = Math.Min(bounds.Right - minWidth, bounds.Left + dx);
                    bounds.Width = bounds.Right - newLeft;
                    bounds.X = newLeft;
                }
                if (east)
                    bounds.Width = Math.Max(minWidth, bounds.Width + dx);
                if (north)
                {
                    int newTop = Math.Min(bounds.Bottom - minHeight, bounds.Top + dy);
                    bounds.Height = bounds.Bottom - newTop;
                    bounds.Y = newTop;
                }
            }

            _pane.ApplyBoundsFromOverlay(bounds);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;
            bool dragging = _dragMode != DragMode.None;
            _dragMode = DragMode.None;
            Capture = false;
            Cursor = CursorFor(EdgeAt(e.Location));
            if (dragging)
                _pane.SaveGeometryFromOverlay();
            if (!ClientRectangle.Contains(e.Location))
                SetBarAlpha(IdleAlpha);
            else
                SetBarAlpha(HoverAlpha);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _font.Dispose();
            base.Dispose(disposing);
        }
    }
}
