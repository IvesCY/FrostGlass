using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FrostGlass
{
    internal sealed class SelectorForm : Form
    {
        private readonly Rectangle _virtualScreen;
        private readonly Rectangle _lastRect;
        private readonly bool _hasLastRect;
        private Bitmap _desktop;

        private Point _startPoint;
        private Rectangle _selection;
        private bool _dragging;
        private bool _hasSelection;
        private bool _smallSelection;
        private Rectangle _resultRect;

        private readonly Font _fontTitle;
        private readonly Font _fontSub;
        private readonly Font _fontBadge;

        public Rectangle SelectedRect
        {
            get { return _resultRect; }
        }

        public SelectorForm(Rectangle? lastRect)
        {
            _virtualScreen = SystemInformation.VirtualScreen;
            _lastRect = Rectangle.Empty;
            _hasLastRect = false;
            if (lastRect.HasValue && lastRect.Value.Width >= 24 && lastRect.Value.Height >= 24)
            {
                _lastRect = lastRect.Value;
                _hasLastRect = true;
            }

            _fontTitle = Theme.Font(15.0f, FontStyle.Bold);
            _fontSub = Theme.Font(9.5f);
            _fontBadge = Theme.Font(9.0f, FontStyle.Bold);

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = _virtualScreen;
            TopMost = true;
            ShowInTaskbar = false;
            KeyPreview = true;
            Cursor = Cursors.Cross;
            BackColor = Color.FromArgb(10, 13, 19);
            Text = "毛玻璃 - 选择区域";
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            CaptureDesktop();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        private void CaptureDesktop()
        {
            try
            {
                _desktop = new Bitmap(_virtualScreen.Width, _virtualScreen.Height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(_desktop))
                {
                    g.CopyFromScreen(_virtualScreen.Location, Point.Empty, _virtualScreen.Size,
                        CopyPixelOperation.SourceCopy);
                }
            }
            catch (Exception ex)
            {
                Log.Write("截取桌面失败，将使用暗色背景进行框选", ex);
                if (_desktop != null)
                {
                    _desktop.Dispose();
                    _desktop = null;
                }
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            NativeMethods.ForceForegroundWindow(Handle);
            Activate();
            Focus();
            Invalidate();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_desktop != null)
            {
                _desktop.Dispose();
                _desktop = null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fontTitle.Dispose();
                _fontSub.Dispose();
                _fontBadge.Dispose();
            }
            base.Dispose(disposing);
        }

        private Point ClampToClient(Point p)
        {
            int x = Math.Max(0, Math.Min(ClientSize.Width - 1, p.X));
            int y = Math.Max(0, Math.Min(ClientSize.Height - 1, p.Y));
            return new Point(x, y);
        }

        private Rectangle NormalizedSelection(Point end)
        {
            int x1 = Math.Min(_startPoint.X, end.X);
            int y1 = Math.Min(_startPoint.Y, end.Y);
            int x2 = Math.Max(_startPoint.X, end.X);
            int y2 = Math.Max(_startPoint.Y, end.Y);
            return new Rectangle(x1, y1, x2 - x1, y2 - y1);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return;
            }
            if (e.Button != MouseButtons.Left)
                return;

            _startPoint = ClampToClient(e.Location);
            _selection = new Rectangle(_startPoint.X, _startPoint.Y, 0, 0);
            _dragging = true;
            _hasSelection = true;
            _smallSelection = false;
            Capture = true;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_dragging)
                return;

            Point current = ClampToClient(e.Location);
            _selection = NormalizedSelection(current);
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!_dragging || e.Button != MouseButtons.Left)
                return;

            _dragging = false;
            Capture = false;
            Point current = ClampToClient(e.Location);
            _selection = NormalizedSelection(current);

            int minSize = Theme.S(this, 24);
            if (_selection.Width < minSize || _selection.Height < minSize)
            {
                _hasSelection = false;
                _smallSelection = true;
                Invalidate();
                return;
            }

            _resultRect = new Rectangle(
                _virtualScreen.X + _selection.X,
                _virtualScreen.Y + _selection.Y,
                _selection.Width,
                _selection.Height);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
                return true;
            }
            if (keyData == Keys.Enter)
            {
                if (_dragging && _selection.Width >= Theme.S(this, 24) && _selection.Height >= Theme.S(this, 24))
                {
                    _resultRect = new Rectangle(
                        _virtualScreen.X + _selection.X,
                        _virtualScreen.Y + _selection.Y,
                        _selection.Width,
                        _selection.Height);
                    DialogResult = DialogResult.OK;
                    Close();
                    return true;
                }
                if (_hasLastRect)
                {
                    _resultRect = _lastRect;
                    DialogResult = DialogResult.OK;
                    Close();
                    return true;
                }
                _smallSelection = false;
                _hasSelection = false;
                Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 整个表面在 OnPaint 中一次性绘制，避免闪烁。
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureGraphics(g);
            g.Clear(Color.FromArgb(10, 13, 19));

            if (_desktop != null)
                g.DrawImageUnscaled(_desktop, 0, 0);

            using (SolidBrush dim = new SolidBrush(Color.FromArgb(146, 5, 8, 13)))
                g.FillRectangle(dim, ClientRectangle);

            if (_hasSelection && _selection.Width > 0 && _selection.Height > 0)
                DrawActiveSelection(g);
            else if (_hasLastRect)
                DrawLastRegion(g);

            if (_dragging)
                DrawGuides(g);

            DrawHintCard(g);
        }

        private void DrawActiveSelection(Graphics g)
        {
            Rectangle sel = _selection;
            if (_desktop != null)
            {
                Rectangle source = Rectangle.Intersect(new Rectangle(0, 0, _desktop.Width, _desktop.Height), sel);
                if (source.Width > 0 && source.Height > 0)
                    g.DrawImage(_desktop, sel, source, GraphicsUnit.Pixel);
            }

            int border = Math.Max(1, Theme.S(this, 1));
            using (Pen shadow = new Pen(Color.FromArgb(150, 0, 0, 0), border * 3))
            using (Pen light = new Pen(Color.FromArgb(245, 255, 255, 255), border))
            {
                Rectangle outer = Rectangle.Inflate(sel, border, border);
                g.DrawRectangle(shadow, outer);
                g.DrawRectangle(light, sel);
            }

            int handle = Theme.S(this, 11);
            using (Pen handlePen = new Pen(Color.FromArgb(255, 255, 255, 255), Math.Max(2, Theme.S(this, 2))))
            {
                handlePen.StartCap = LineCap.Square;
                handlePen.EndCap = LineCap.Square;
                g.DrawLine(handlePen, sel.Left, sel.Top, sel.Left + handle, sel.Top);
                g.DrawLine(handlePen, sel.Left, sel.Top, sel.Left, sel.Top + handle);
                g.DrawLine(handlePen, sel.Right, sel.Top, sel.Right - handle, sel.Top);
                g.DrawLine(handlePen, sel.Right, sel.Top, sel.Right, sel.Top + handle);
                g.DrawLine(handlePen, sel.Left, sel.Bottom, sel.Left + handle, sel.Bottom);
                g.DrawLine(handlePen, sel.Left, sel.Bottom, sel.Left, sel.Bottom - handle);
                g.DrawLine(handlePen, sel.Right, sel.Bottom, sel.Right - handle, sel.Bottom);
                g.DrawLine(handlePen, sel.Right, sel.Bottom, sel.Right, sel.Bottom - handle);
            }

            DrawSizeBadge(g, sel);
        }

        private void DrawSizeBadge(Graphics g, Rectangle sel)
        {
            string text = sel.Width + " × " + sel.Height;
            Size textSize = TextRenderer.MeasureText(text, _fontBadge);
            int padX = Theme.S(this, 9);
            int padY = Theme.S(this, 5);
            int w = textSize.Width + padX * 2;
            int h = textSize.Height + padY * 2;
            int x = sel.Left;
            int y = sel.Top - h - Theme.S(this, 9);
            if (y < Theme.S(this, 8))
                y = sel.Top + Theme.S(this, 9);
            if (x + w > ClientSize.Width - Theme.S(this, 8))
                x = ClientSize.Width - w - Theme.S(this, 8);
            x = Math.Max(Theme.S(this, 8), x);

            Rectangle badge = new Rectangle(x, y, w, h);
            using (GraphicsPath path = Theme.RoundedRect(badge, Theme.S(this, 6)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(235, 17, 21, 29)))
            using (Pen border = new Pen(Color.FromArgb(55, 255, 255, 255), 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            Theme.DrawString(g, text, _fontBadge, Color.FromArgb(245, 255, 255, 255),
                badge, ContentAlignment.MiddleCenter);
        }

        private void DrawLastRegion(Graphics g)
        {
            Rectangle local = new Rectangle(
                _lastRect.X - _virtualScreen.X,
                _lastRect.Y - _virtualScreen.Y,
                _lastRect.Width,
                _lastRect.Height);

            using (Pen pen = new Pen(Color.FromArgb(150, 145, 180, 252), Math.Max(1, Theme.S(this, 1))))
            {
                pen.DashStyle = DashStyle.Dash;
                g.DrawRectangle(pen, local);
            }

            string label = "上次区域 · 按 Enter 使用";
            Size textSize = TextRenderer.MeasureText(label, _fontSub);
            int padX = Theme.S(this, 9);
            int padY = Theme.S(this, 5);
            Rectangle badge = new Rectangle(local.Left, local.Top + Theme.S(this, 7),
                textSize.Width + padX * 2, textSize.Height + padY * 2);
            using (GraphicsPath path = Theme.RoundedRect(badge, Theme.S(this, 5)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(215, 17, 21, 29)))
            using (Pen border = new Pen(Color.FromArgb(70, 145, 180, 252), 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }
            Theme.DrawString(g, label, _fontSub, Color.FromArgb(220, 190, 208, 250),
                badge, ContentAlignment.MiddleCenter);
        }

        private void DrawGuides(Graphics g)
        {
            Point mouse = PointToClient(Cursor.Position);
            using (Pen guide = new Pen(Color.FromArgb(55, 255, 255, 255), Math.Max(1, Theme.S(this, 1))))
            {
                guide.DashStyle = DashStyle.Dot;
                g.DrawLine(guide, mouse.X, 0, mouse.X, ClientSize.Height);
                g.DrawLine(guide, 0, mouse.Y, ClientSize.Width, mouse.Y);
            }
        }

        private void DrawHintCard(Graphics g)
        {
            int pad = Theme.S(this, 20);
            int cardWidth = Theme.S(this, 560);
            cardWidth = Math.Min(cardWidth, ClientSize.Width - pad * 2);
            int cardHeight = Theme.S(this, 84);
            Rectangle card = new Rectangle(
                (ClientSize.Width - cardWidth) / 2,
                Theme.S(this, 26),
                cardWidth,
                cardHeight);

            using (GraphicsPath path = Theme.RoundedRect(card, Theme.S(this, 12)))
            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(242, 18, 23, 32)))
            using (Pen border = new Pen(Color.FromArgb(65, 255, 255, 255), 1))
            {
                Rectangle shadowRect = Rectangle.Inflate(card, Theme.S(this, 2), Theme.S(this, 3));
                shadowRect.Offset(0, Theme.S(this, 3));
                using (GraphicsPath shadowPath = Theme.RoundedRect(shadowRect, Theme.S(this, 14)))
                    g.FillPath(shadow, shadowPath);
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int contentLeft = card.Left + Theme.S(this, 22);
            int contentRight = card.Right - Theme.S(this, 22);
            int titleY = card.Top + Theme.S(this, 15);
            int subY = card.Top + Theme.S(this, 44);

            string title;
            string subtitle;
            if (_dragging)
            {
                title = "松开鼠标，完成框选";
                subtitle = "右键或 Esc 取消";
            }
            else if (_smallSelection)
            {
                title = "选区太小了，请重新拖动";
                subtitle = "按住左键拖出一块矩形区域 · Esc 取消";
            }
            else
            {
                title = "框选要遮挡的区域";
                subtitle = "按住左键拖动，松开即可生成毛玻璃";
                if (_hasLastRect)
                    subtitle += "  ·  Enter 使用上次区域";
            }

            using (SolidBrush accentDot = new SolidBrush(Theme.Accent))
                g.FillEllipse(accentDot, contentLeft, titleY + Theme.S(this, 3), Theme.S(this, 7), Theme.S(this, 7));

            Rectangle titleRect = new Rectangle(contentLeft + Theme.S(this, 15), titleY,
                contentRight - contentLeft - Theme.S(this, 15), Theme.S(this, 25));
            Rectangle subRect = new Rectangle(contentLeft, subY, contentRight - contentLeft, Theme.S(this, 20));
            Theme.DrawString(g, title, _fontTitle, Theme.TextPrimary, titleRect, ContentAlignment.MiddleLeft);
            Theme.DrawString(g, subtitle, _fontSub, Theme.TextSecondary, subRect, ContentAlignment.MiddleLeft);
        }
    }
}
