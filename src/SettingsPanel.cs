using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FrostGlass
{
    internal sealed class SettingsPanel : Form
    {
        private readonly TrayAppContext _app;
        private readonly AppSettings _settings;
        private readonly FrostPane _pane;

        private readonly Font _fontTitle;
        private readonly Font _fontSubtitle;
        private readonly Font _fontLabel;
        private readonly Font _fontValue;
        private readonly Font _fontSmall;
        private readonly Font _fontToggle;
        private readonly Font _fontToggleSub;
        private readonly Font _fontButton;

        private Rectangle _closeRect;
        private Rectangle _clarityLabelRect;
        private Rectangle _clarityValueRect;
        private Rectangle _claritySliderRect;
        private Rectangle _opacityLabelRect;
        private Rectangle _opacityValueRect;
        private Rectangle _opacitySliderRect;
        private Rectangle _hintRect;
        private Rectangle _hintRect2;
        private Rectangle _toggleTopRect;
        private Rectangle _shortcutRowRect;
        private Rectangle _shortcutPillRect;
        private Rectangle _reselectRect;
        private Rectangle _hideRect;
        private Rectangle _titleDragRect;
        private Rectangle _footerRect;

        private bool _hoverClose;
        private bool _hoverClarity;
        private bool _hoverOpacity;
        private bool _hoverTop;
        private bool _hoverShortcut;
        private bool _hoverReselect;
        private bool _hoverHide;
        private bool _hoverTitleBar;
        private int _activeSlider; // 1 = clarity, 2 = transparency
        private bool _draggingPanel;
        private Point _panelDragCursorStart;
        private Point _panelDragLocationStart;
        private ContextMenuStrip _shortcutMenu;

        public SettingsPanel(TrayAppContext app, AppSettings settings, FrostPane pane)
        {
            _app = app;
            _settings = settings;
            _pane = pane;

            _fontTitle = Theme.Font(15.0f, FontStyle.Bold);
            _fontSubtitle = Theme.Font(8.75f);
            _fontLabel = Theme.Font(9.5f);
            _fontValue = Theme.Font(10.0f, FontStyle.Bold);
            _fontSmall = Theme.Font(8.25f);
            _fontToggle = Theme.Font(9.5f);
            _fontToggleSub = Theme.Font(8.0f);
            _fontButton = Theme.Font(9.5f);

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Theme.PanelBottom;
            Text = "毛玻璃设置";
            KeyPreview = true;
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            _shortcutMenu = BuildShortcutMenu();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= NativeMethods.WS_EX_TOOLWINDOW;
                cp.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.TryEnableRoundedCorners(Handle);
            ClientSize = new Size(Theme.S(this, 350), Theme.S(this, 436));
            RecalculateLayout();
        }

        public void ApplyDesignSize()
        {
            Size target = new Size(Theme.S(this, 350), Theme.S(this, 436));
            if (ClientSize != target)
            {
                ClientSize = target;
                RecalculateLayout();
                Invalidate();
            }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            NativeMethods.ForceForegroundWindow(Handle);
            Activate();
            Focus();
            BringToFront();
        }

        private void RecalculateLayout()
        {
            int pad = Theme.S(this, 20);
            int close = Theme.S(this, 28);
            int contentWidth = ClientSize.Width - pad * 2;

            _closeRect = new Rectangle(ClientSize.Width - pad - close, Theme.S(this, 15), close, close);
            _titleDragRect = new Rectangle(0, 0, ClientSize.Width, Theme.S(this, 68));

            _clarityLabelRect = new Rectangle(pad, Theme.S(this, 80), contentWidth / 2, Theme.S(this, 20));
            _clarityValueRect = new Rectangle(pad + contentWidth / 2, Theme.S(this, 80),
                contentWidth / 2, Theme.S(this, 20));
            _claritySliderRect = new Rectangle(pad, Theme.S(this, 105), contentWidth, Theme.S(this, 26));

            _opacityLabelRect = new Rectangle(pad, Theme.S(this, 140), contentWidth / 2, Theme.S(this, 20));
            _opacityValueRect = new Rectangle(pad + contentWidth / 2, Theme.S(this, 140),
                contentWidth / 2, Theme.S(this, 20));
            _opacitySliderRect = new Rectangle(pad, Theme.S(this, 165), contentWidth, Theme.S(this, 26));

            _hintRect = new Rectangle(pad, Theme.S(this, 198), contentWidth, Theme.S(this, 16));
            _hintRect2 = new Rectangle(pad, Theme.S(this, 214), contentWidth, Theme.S(this, 16));
            _toggleTopRect = new Rectangle(pad, Theme.S(this, 244), contentWidth, Theme.S(this, 42));
            _shortcutRowRect = new Rectangle(pad, Theme.S(this, 286), contentWidth, Theme.S(this, 42));
            int shortcutWidth = Theme.S(this, 132);
            _shortcutPillRect = new Rectangle(_shortcutRowRect.Right - shortcutWidth - Theme.S(this, 10),
                _shortcutRowRect.Top + Theme.S(this, 8), shortcutWidth, Theme.S(this, 26));

            int buttonWidth = (contentWidth - Theme.S(this, 10)) / 2;
            _reselectRect = new Rectangle(pad, Theme.S(this, 352), buttonWidth, Theme.S(this, 36));
            _hideRect = new Rectangle(_reselectRect.Right + Theme.S(this, 10), Theme.S(this, 352),
                contentWidth - buttonWidth - Theme.S(this, 10), Theme.S(this, 36));
            _footerRect = new Rectangle(pad, Theme.S(this, 400), contentWidth, Theme.S(this, 18));
        }

        public void SyncFromSettings()
        {
            Invalidate();
        }

        public void PositionNear(Rectangle paneBounds)
        {
            if (!IsHandleCreated)
                return;
            Screen screen = Screen.FromRectangle(paneBounds);
            Rectangle area = screen.WorkingArea;
            int margin = Theme.S(this, 10);
            int gap = Theme.S(this, 10);
            int x = paneBounds.Right - Width;
            x = Math.Max(area.Left + margin, Math.Min(area.Right - Width - margin, x));
            int y = paneBounds.Top - Height - gap;
            if (y < area.Top + margin)
                y = paneBounds.Bottom + gap;
            if (y + Height > area.Bottom - margin)
                y = area.Bottom - Height - margin;
            y = Math.Max(area.Top + margin, y);
            Location = new Point(x, y);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Theme.ConfigureGraphics(g);
            g.Clear(Theme.PanelBottom);

            Rectangle full = ClientRectangle;
            using (LinearGradientBrush background = new LinearGradientBrush(full,
                Theme.PanelTop, Theme.PanelBottom, LinearGradientMode.Vertical))
                g.FillRectangle(background, full);

            using (SolidBrush glow = new SolidBrush(Color.FromArgb(15, Theme.Accent)))
                g.FillEllipse(glow, ClientSize.Width - Theme.S(this, 200), -Theme.S(this, 120),
                    Theme.S(this, 280), Theme.S(this, 240));

            using (Pen border = new Pen(Theme.PanelBorder, 1))
                g.DrawRectangle(border, 0, 0, ClientSize.Width - 1, ClientSize.Height - 1);

            DrawHeader(g);
            DrawSliderSection(g, _clarityLabelRect, _clarityValueRect, _claritySliderRect,
                "清晰度", _settings.Clarity, 40, 100, _hoverClarity, _activeSlider == 1);
            DrawSliderSection(g, _opacityLabelRect, _opacityValueRect, _opacitySliderRect,
                "透明度", _settings.Transparency, 10, 90, _hoverOpacity, _activeSlider == 2);

            Theme.DrawString(g, "清晰度越高越容易辨认，建议 70–90", _fontSmall,
                Theme.TextMuted, _hintRect, ContentAlignment.MiddleLeft);
            Theme.DrawString(g, "透明度越高，磨砂上的雾色越淡", _fontSmall,
                Theme.TextMuted, _hintRect2, ContentAlignment.MiddleLeft);

            DrawDivider(g, Theme.S(this, 234));
            DrawToggle(g, _toggleTopRect, "总在最上层",
                "始终覆盖在其他窗口上方", _settings.AlwaysOnTop, _hoverTop);
            DrawShortcutRow(g);
            DrawDivider(g, Theme.S(this, 338));
            DrawButton(g, _reselectRect, "重新框选", true, _hoverReselect);
            DrawButton(g, _hideRect, "隐藏毛玻璃", false, _hoverHide);

            Theme.DrawString(g, "Ctrl+Alt+G 显示/隐藏     Ctrl+Alt+R 重新框选",
                _fontSmall, Theme.TextMuted, _footerRect, ContentAlignment.MiddleCenter);
        }

        private void DrawHeader(Graphics g)
        {
            int pad = Theme.S(this, 20);

            Rectangle logo = new Rectangle(pad, Theme.S(this, 15), Theme.S(this, 26), Theme.S(this, 26));
            using (GraphicsPath logoPath = Theme.RoundedRect(logo, Theme.S(this, 7)))
            {
                using (LinearGradientBrush fill = new LinearGradientBrush(logo,
                    Color.FromArgb(90, 255, 255, 255), Color.FromArgb(24, 255, 255, 255),
                    LinearGradientMode.ForwardDiagonal))
                    g.FillPath(fill, logoPath);
                using (Pen border = new Pen(Color.FromArgb(105, 255, 255, 255), 1))
                    g.DrawPath(border, logoPath);
                using (Pen shine = new Pen(Color.FromArgb(120, 255, 255, 255), 1))
                    g.DrawLine(shine, logo.Left + Theme.S(this, 6), logo.Bottom - Theme.S(this, 7),
                        logo.Right - Theme.S(this, 6), logo.Top + Theme.S(this, 7));
            }

            Rectangle title = new Rectangle(logo.Right + Theme.S(this, 10), Theme.S(this, 11),
                ClientSize.Width - logo.Right - Theme.S(this, 72), Theme.S(this, 25));
            Theme.DrawString(g, "毛玻璃", _fontTitle, Theme.TextPrimary, title, ContentAlignment.MiddleLeft);

            Rectangle subtitle = new Rectangle(pad, Theme.S(this, 42),
                ClientSize.Width - pad * 2 - Theme.S(this, 36), Theme.S(this, 18));
            Theme.DrawString(g, "隐私遮挡 · 只挡视线，不挡操作", _fontSubtitle, Theme.TextMuted,
                subtitle, ContentAlignment.MiddleLeft);

            DrawCloseButton(g);
        }

        private void DrawCloseButton(Graphics g)
        {
            if (_hoverClose)
            {
                using (GraphicsPath path = Theme.RoundedRect(_closeRect, Theme.S(this, 7)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(26, 255, 255, 255)))
                    g.FillPath(fill, path);
            }

            int inset = Theme.S(this, 9);
            using (Pen pen = new Pen(_hoverClose ? Theme.TextPrimary : Theme.TextSecondary,
                Math.Max(1, Theme.S(this, 1))))
            {
                g.DrawLine(pen, _closeRect.Left + inset, _closeRect.Top + inset,
                    _closeRect.Right - inset, _closeRect.Bottom - inset);
                g.DrawLine(pen, _closeRect.Right - inset, _closeRect.Top + inset,
                    _closeRect.Left + inset, _closeRect.Bottom - inset);
            }
        }

        private void DrawSliderSection(Graphics g, Rectangle labelRect, Rectangle valueRect,
            Rectangle sliderRect, string label, int value, int min, int max, bool hover, bool dragging)
        {
            Theme.DrawString(g, label, _fontLabel, Theme.TextSecondary,
                labelRect, ContentAlignment.MiddleLeft);
            Theme.DrawString(g, value + "%", _fontValue, Theme.TextPrimary,
                valueRect, ContentAlignment.MiddleRight);

            int thumbRadius = Theme.S(this, 8);
            int trackHeight = Theme.S(this, 6);
            int left = sliderRect.Left + thumbRadius;
            int right = sliderRect.Right - thumbRadius;
            int cy = sliderRect.Top + sliderRect.Height / 2;
            float fraction = (value - min) / (float)(max - min);
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            int thumbX = left + (int)Math.Round((right - left) * fraction);

            Rectangle track = new Rectangle(left, cy - trackHeight / 2, Math.Max(1, right - left), trackHeight);
            using (GraphicsPath trackPath = Theme.RoundedRect(track, trackHeight / 2))
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(60, 70, 86)))
                g.FillPath(trackBrush, trackPath);

            if (thumbX > left)
            {
                Rectangle fill = new Rectangle(left, cy - trackHeight / 2, thumbX - left, trackHeight);
                using (GraphicsPath fillPath = Theme.RoundedRect(fill, trackHeight / 2))
                using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                    new Rectangle(fill.Left, fill.Top, Math.Max(1, fill.Width), Math.Max(1, fill.Height)),
                    Theme.AccentMuted, Theme.Accent, LinearGradientMode.Horizontal))
                    g.FillPath(fillBrush, fillPath);
            }

            if (hover || dragging)
            {
                using (SolidBrush halo = new SolidBrush(Color.FromArgb(45, Theme.Accent)))
                    g.FillEllipse(halo, thumbX - thumbRadius - Theme.S(this, 3),
                        cy - thumbRadius - Theme.S(this, 3),
                        (thumbRadius + Theme.S(this, 3)) * 2, (thumbRadius + Theme.S(this, 3)) * 2);
            }

            using (SolidBrush shadow = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
                g.FillEllipse(shadow, thumbX - thumbRadius, cy - thumbRadius + Theme.S(this, 2),
                    thumbRadius * 2, thumbRadius * 2);
            using (SolidBrush thumb = new SolidBrush(Color.FromArgb(250, 248, 250, 253)))
                g.FillEllipse(thumb, thumbX - thumbRadius, cy - thumbRadius, thumbRadius * 2, thumbRadius * 2);
            using (Pen ring = new Pen(Color.FromArgb(90, Theme.Accent), 1))
                g.DrawEllipse(ring, thumbX - thumbRadius, cy - thumbRadius, thumbRadius * 2, thumbRadius * 2);
        }


        private void DrawShortcutRow(Graphics g)
        {
            Rectangle row = _shortcutRowRect;
            if (_hoverShortcut)
            {
                using (GraphicsPath path = Theme.RoundedRect(row, Theme.S(this, 8)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    g.FillPath(fill, path);
            }

            int textWidth = _shortcutPillRect.Left - row.Left - Theme.S(this, 26);
            Rectangle titleRect = new Rectangle(row.Left + Theme.S(this, 10), row.Top + Theme.S(this, 4),
                textWidth, Theme.S(this, 18));
            Rectangle subRect = new Rectangle(row.Left + Theme.S(this, 10), row.Top + Theme.S(this, 21),
                textWidth, Theme.S(this, 15));
            Theme.DrawString(g, "按住调整", _fontToggle, Theme.TextPrimary, titleRect, ContentAlignment.MiddleLeft);
            Theme.DrawString(g, "按住组合键拖动/缩放", _fontToggleSub, Theme.TextMuted,
                subRect, ContentAlignment.MiddleLeft);

            using (GraphicsPath path = Theme.RoundedRect(_shortcutPillRect, Theme.S(this, 7)))
            using (SolidBrush fill = new SolidBrush(_hoverShortcut ? Theme.ControlHover : Theme.ControlBackground))
            using (Pen border = new Pen(Color.FromArgb(90, Theme.PanelBorder), 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            Rectangle textRect = new Rectangle(_shortcutPillRect.Left + Theme.S(this, 10), _shortcutPillRect.Top,
                _shortcutPillRect.Width - Theme.S(this, 28), _shortcutPillRect.Height);
            Theme.DrawString(g, AppSettings.ModifierText(_settings.AdjustModifiers), _fontButton,
                Theme.TextPrimary, textRect, ContentAlignment.MiddleCenter);

            int cx = _shortcutPillRect.Right - Theme.S(this, 14);
            int cy = _shortcutPillRect.Top + _shortcutPillRect.Height / 2;
            int half = Theme.S(this, 3);
            using (Pen chevron = new Pen(Theme.TextSecondary, Math.Max(1, Theme.S(this, 1))))
            {
                g.DrawLine(chevron, cx - half, cy - Theme.S(this, 1), cx, cy + half);
                g.DrawLine(chevron, cx, cy + half, cx + half, cy - Theme.S(this, 1));
            }
        }

        private ContextMenuStrip BuildShortcutMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(24, 29, 38);
            menu.ForeColor = Theme.TextPrimary;
            menu.Font = Theme.Font(9.5f);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Renderer = new DarkMenuRenderer();
            AddShortcutMenuItem(menu, 3, "Ctrl + Alt");
            AddShortcutMenuItem(menu, 5, "Ctrl + Shift");
            AddShortcutMenuItem(menu, 6, "Alt + Shift");
            AddShortcutMenuItem(menu, 7, "Ctrl + Alt + Shift");
            menu.Opening += delegate { UpdateShortcutMenuChecks(menu); };
            return menu;
        }

        private void AddShortcutMenuItem(ContextMenuStrip menu, int modifiers, string label)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(label);
            item.Name = label;
            item.Tag = modifiers;
            item.Click += delegate
            {
                _settings.AdjustModifiers = modifiers;
                _app.SaveSettings();
                _pane.ShowHint("按住 " + label + " 可拖动/缩放", 2800);
                Invalidate();
            };
            menu.Items.Add(item);
        }

        private void UpdateShortcutMenuChecks(ContextMenuStrip menu)
        {
            int current = AppSettings.NormalizeAdjustModifiers(_settings.AdjustModifiers);
            foreach (ToolStripItem item in menu.Items)
            {
                ToolStripMenuItem menuItem = item as ToolStripMenuItem;
                if (menuItem == null || menuItem.Tag == null)
                    continue;
                int value = (int)menuItem.Tag;
                menuItem.Text = (value == current ? "✓  " : "    ") + menuItem.Name;
            }
        }

        private void ShowShortcutMenu()
        {
            if (_shortcutMenu == null)
                return;
            Point location = new Point(_shortcutPillRect.Left,
                _shortcutPillRect.Bottom + Theme.S(this, 4));
            _shortcutMenu.Show(this, location);
        }

        private void DrawToggle(Graphics g, Rectangle row, string title, string subtitle, bool on, bool hover)
        {
            if (hover)
            {
                using (GraphicsPath path = Theme.RoundedRect(row, Theme.S(this, 8)))
                using (SolidBrush fill = new SolidBrush(Color.FromArgb(18, 255, 255, 255)))
                    g.FillPath(fill, path);
            }

            int switchWidth = Theme.S(this, 40);
            int switchHeight = Theme.S(this, 22);
            int switchX = row.Right - switchWidth - Theme.S(this, 10);
            int switchY = row.Top + (row.Height - switchHeight) / 2;
            Rectangle track = new Rectangle(switchX, switchY, switchWidth, switchHeight);

            using (GraphicsPath trackPath = Theme.RoundedRect(track, switchHeight / 2))
            using (SolidBrush trackBrush = new SolidBrush(on ? Theme.Accent : Color.FromArgb(55, 65, 80)))
                g.FillPath(trackBrush, trackPath);

            int knobRadius = switchHeight / 2 - Theme.S(this, 2);
            int knobX = on ? track.Right - Theme.S(this, 2) - knobRadius * 2 : track.Left + Theme.S(this, 2);
            using (SolidBrush knob = new SolidBrush(Color.FromArgb(252, 250, 252, 255)))
                g.FillEllipse(knob, knobX, track.Top + Theme.S(this, 2), knobRadius * 2, knobRadius * 2);

            int textWidth = switchX - row.Left - Theme.S(this, 26);
            Rectangle titleRect = new Rectangle(row.Left + Theme.S(this, 10), row.Top + Theme.S(this, 4),
                textWidth, Theme.S(this, 18));
            Rectangle subRect = new Rectangle(row.Left + Theme.S(this, 10), row.Top + Theme.S(this, 21),
                textWidth, Theme.S(this, 15));
            Theme.DrawString(g, title, _fontToggle, Theme.TextPrimary, titleRect, ContentAlignment.MiddleLeft);
            Theme.DrawString(g, subtitle, _fontToggleSub, Theme.TextMuted, subRect, ContentAlignment.MiddleLeft);
        }

        private void DrawButton(Graphics g, Rectangle bounds, string text, bool primary, bool hover)
        {
            using (GraphicsPath path = Theme.RoundedRect(bounds, Theme.S(this, 8)))
            {
                if (primary)
                {
                    Color start = hover ? Color.FromArgb(175, 200, 252) : Color.FromArgb(122, 162, 247);
                    Color end = hover ? Color.FromArgb(132, 168, 248) : Color.FromArgb(99, 137, 220);
                    using (LinearGradientBrush fill = new LinearGradientBrush(bounds, start, end,
                        LinearGradientMode.Vertical))
                        g.FillPath(fill, path);
                    using (Pen border = new Pen(Color.FromArgb(80, 255, 255, 255), 1))
                        g.DrawPath(border, path);
                    Theme.DrawString(g, text, _fontButton, Color.FromArgb(12, 17, 26),
                        bounds, ContentAlignment.MiddleCenter);
                }
                else
                {
                    using (SolidBrush fill = new SolidBrush(hover ? Theme.ControlHover : Theme.ControlBackground))
                        g.FillPath(fill, path);
                    using (Pen border = new Pen(Color.FromArgb(80, Theme.PanelBorder), 1))
                        g.DrawPath(border, path);
                    Theme.DrawString(g, text, _fontButton, Theme.TextPrimary,
                        bounds, ContentAlignment.MiddleCenter);
                }
            }
        }

        private void DrawDivider(Graphics g, int y)
        {
            using (Pen pen = new Pen(Theme.Divider, 1))
                g.DrawLine(pen, Theme.S(this, 20), y, ClientSize.Width - Theme.S(this, 20), y);
        }

        private int ValueFromMouse(Rectangle sliderRect, int x, int min, int max)
        {
            int thumbRadius = Theme.S(this, 8);
            int left = sliderRect.Left + thumbRadius;
            int right = sliderRect.Right - thumbRadius;
            float fraction = (x - left) / (float)Math.Max(1, right - left);
            fraction = Math.Max(0f, Math.Min(1f, fraction));
            return min + (int)Math.Round(fraction * (max - min));
        }

        private void SetClarityFromMouse(int x)
        {
            int value = ValueFromMouse(_claritySliderRect, x, 40, 100);
            _pane.SetClarity(value);
            Invalidate();
        }

        private void SetTransparencyFromMouse(int x)
        {
            int value = ValueFromMouse(_opacitySliderRect, x, 10, 90);
            _pane.SetTransparency(value);
            Invalidate();
        }

        private bool In(Rectangle rect, Point p)
        {
            return rect.Contains(p);
        }

        private void UpdateHover(Point p)
        {
            bool hoverClose = In(_closeRect, p);
            bool hoverBlur = In(Rectangle.Inflate(_claritySliderRect, 0, Theme.S(this, 5)), p);
            bool hoverOpacity = In(Rectangle.Inflate(_opacitySliderRect, 0, Theme.S(this, 5)), p);
            bool hoverTop = In(_toggleTopRect, p);
            bool hoverShortcut = In(_shortcutRowRect, p);
            bool hoverReselect = In(_reselectRect, p);
            bool hoverHide = In(_hideRect, p);
            bool hoverTitle = In(_titleDragRect, p) && !hoverClose;

            bool changed = hoverClose != _hoverClose || hoverBlur != _hoverClarity ||
                hoverOpacity != _hoverOpacity || hoverTop != _hoverTop ||
                hoverShortcut != _hoverShortcut || hoverReselect != _hoverReselect ||
                hoverHide != _hoverHide || hoverTitle != _hoverTitleBar;
            _hoverClose = hoverClose;
            _hoverClarity = hoverBlur;
            _hoverOpacity = hoverOpacity;
            _hoverTop = hoverTop;
            _hoverShortcut = hoverShortcut;
            _hoverReselect = hoverReselect;
            _hoverHide = hoverHide;
            _hoverTitleBar = hoverTitle;

            if (_activeSlider != 0 || hoverBlur || hoverOpacity)
                Cursor = Cursors.SizeWE;
            else if (hoverClose || hoverTop || hoverShortcut || hoverReselect || hoverHide)
                Cursor = Cursors.Hand;
            else if (_draggingPanel || hoverTitle)
                Cursor = Cursors.SizeAll;
            else
                Cursor = Cursors.Default;

            if (changed)
                Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_draggingPanel)
            {
                Point delta = Cursor.Position;
                Location = new Point(
                    _panelDragLocationStart.X + delta.X - _panelDragCursorStart.X,
                    _panelDragLocationStart.Y + delta.Y - _panelDragCursorStart.Y);
                return;
            }
            if (_activeSlider == 1)
            {
                SetClarityFromMouse(e.X);
                return;
            }
            if (_activeSlider == 2)
            {
                SetTransparencyFromMouse(e.X);
                return;
            }
            UpdateHover(e.Location);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            if (In(_closeRect, e.Location))
            {
                Hide();
                return;
            }
            if (In(Rectangle.Inflate(_claritySliderRect, 0, Theme.S(this, 5)), e.Location))
            {
                _activeSlider = 1;
                Capture = true;
                SetClarityFromMouse(e.X);
                return;
            }
            if (In(Rectangle.Inflate(_opacitySliderRect, 0, Theme.S(this, 5)), e.Location))
            {
                _activeSlider = 2;
                Capture = true;
                SetTransparencyFromMouse(e.X);
                return;
            }
            if (In(_toggleTopRect, e.Location))
            {
                _pane.SetAlwaysOnTop(!_settings.AlwaysOnTop);
                _app.SaveSettings();
                Invalidate();
                return;
            }
            if (In(_shortcutRowRect, e.Location))
            {
                ShowShortcutMenu();
                return;
            }
            if (In(_reselectRect, e.Location))
            {
                Hide();
                _app.Reselect();
                return;
            }
            if (In(_hideRect, e.Location))
            {
                Hide();
                _app.HidePane();
                return;
            }
            if (In(_titleDragRect, e.Location))
            {
                _draggingPanel = true;
                _panelDragCursorStart = Cursor.Position;
                _panelDragLocationStart = Location;
                Capture = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left)
                return;

            bool sliderChanged = _activeSlider != 0;
            _activeSlider = 0;
            _draggingPanel = false;
            Capture = false;
            if (sliderChanged)
            {
                _app.SaveSettings();
                UpdateHover(e.Location);
            }
            Invalidate();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                Hide();
                return true;
            }
            if (keyData == Keys.Left || keyData == Keys.Right)
            {
                int delta = keyData == Keys.Left ? -5 : 5;
                _pane.SetTransparency(_settings.Transparency + delta);
                _app.SaveSettings();
                Invalidate();
                return true;
            }
            if (keyData == Keys.Up || keyData == Keys.Down)
            {
                int change = keyData == Keys.Up ? 5 : -5;
                _pane.SetClarity(_settings.Clarity + change);
                _app.SaveSettings();
                Invalidate();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _fontTitle.Dispose();
                _fontSubtitle.Dispose();
                _fontLabel.Dispose();
                _fontValue.Dispose();
                _fontSmall.Dispose();
                _fontToggle.Dispose();
                _fontToggleSub.Dispose();
                _fontButton.Dispose();
                if (_shortcutMenu != null)
                    _shortcutMenu.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
