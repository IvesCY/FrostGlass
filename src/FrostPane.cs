using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace FrostGlass
{
    internal sealed class FrostPane : Form
    {
        private enum DragMode
        {
            None,
            Move,
            North,
            South,
            East,
            West,
            NorthEast,
            NorthWest,
            SouthEast,
            SouthWest
        }

        private readonly TrayAppContext _app;
        private readonly AppSettings _settings;
        private readonly ContextMenuStrip _menu;
        private readonly Font _hintFont;
        private readonly Font _barFont;
        private readonly Timer _hintTimer;
        private readonly Timer _previewTimer;
        private readonly Timer _adjustTimer;
        private readonly FastBlur _blur;

        private DragMode _dragMode = DragMode.None;
        private Point _dragStartCursor;
        private Rectangle _dragStartBounds;
        private bool _dragMoved;
        private bool _hover;
        private bool _editMode;
        private bool _mouseDown;
        private int _hoverButton;
        private string _hint;
        private bool _hotkeysRegistered;
        private PaneBar _bar;
        private PaneResizer _resizer;
        private bool _syncingOverlays;
        private bool _previewMode;
        private bool _previewInitialized;
        private bool _captureErrorLogged;
        private Bitmap _captureBitmap;
        private Bitmap _blurredBitmap;
        private float _blurWeight;
        private readonly ImageAttributes _blendAttributes;
        private readonly ColorMatrix _blendMatrix;

        private ToolStripMenuItem _menuTopMost;
        private ToolStripMenuItem _menuHide;

        public FrostPane(TrayAppContext app, AppSettings settings, Rectangle bounds)
        {
            _app = app;
            _settings = settings;

            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            MinimumSize = new Size(Theme.S(this, 200), Theme.S(this, 90));
            ShowInTaskbar = false;
            TopMost = settings.AlwaysOnTop;
            BackColor = Color.Black;
            Text = "毛玻璃";
            DoubleBuffered = true;
            ResizeRedraw = true;
            Cursor = Cursors.Default;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);

            _hintFont = Theme.Font(9.0f);
            _barFont = Theme.Font(9.0f, FontStyle.Bold);
            _hintTimer = new Timer();
            _hintTimer.Tick += new EventHandler(HintTimer_Tick);
            _previewTimer = new Timer();
            _previewTimer.Interval = 100;
            _previewTimer.Tick += new EventHandler(PreviewTimer_Tick);
            _adjustTimer = new Timer();
            _adjustTimer.Interval = 20;
            _adjustTimer.Tick += new EventHandler(AdjustTimer_Tick);
            _blur = new FastBlur();
            _blendMatrix = new ColorMatrix();
            _blendAttributes = new ImageAttributes();
            _menu = BuildContextMenu();
        }

        public AppSettings Settings
        {
            get { return _settings; }
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
                cp.ExStyle |= NativeMethods.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            NativeMethods.SetLayeredWindowAttributes(Handle, 0, 255, NativeMethods.LWA_ALPHA);
            NativeMethods.SetInputTransparent(Handle, true);
            RegisterHotkeys();
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            StopPreview();
            StopAdjustTracking();
            UnregisterHotkeys();
            base.OnHandleDestroyed(e);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (!_previewInitialized)
            {
                _previewInitialized = true;
                _previewMode = TestCaptureExclusion();
            }
            if (_previewMode)
                StartPreview();
            else
                ApplyAppearance();
            StartAdjustTracking();
            EnsureOverlays();
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible)
            {
                if (_previewInitialized)
                {
                    if (_previewMode)
                        StartPreview();
                    else
                        ApplyAppearance();
                }
                StartAdjustTracking();
                if (IsHandleCreated && !_editMode)
                    NativeMethods.SetInputTransparent(Handle, true);
                EnsureOverlays();
            }
            else
            {
                StopPreview();
                StopAdjustTracking();
                ExitEditMode(true);
                HideOverlays();
                if (_menu != null)
                    _menu.Close();
            }
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            base.OnDpiChanged(e);
            MinimumSize = new Size(Theme.S(this, 200), Theme.S(this, 90));
            Invalidate();
        }

        private bool TestCaptureExclusion()
        {
            if (!IsHandleCreated)
                return false;
            try
            {
                bool ok = NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
                if (ok)
                    NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_NONE);
                return ok;
            }
            catch
            {
                return false;
            }
        }

        private void ApplyAppearance()
        {
            if (!IsHandleCreated)
                return;
            try
            {
                NativeMethods.ApplyAcrylic(Handle, _settings.AccentAlpha, AppSettings.TintColor);
            }
            catch (Exception ex)
            {
                Log.Write("应用毛玻璃效果失败", ex);
            }
        }

        private void StartPreview()
        {
            if (!_previewMode || !Visible || !IsHandleCreated || IsDisposed)
                return;
            if (!_previewTimer.Enabled)
                _previewTimer.Start();
            RefreshPreview();
        }

        private void StopPreview()
        {
            _previewTimer.Stop();
        }

        private void PreviewTimer_Tick(object sender, EventArgs e)
        {
            RefreshPreview();
        }

        private void RefreshPreview()
        {
            if (!_previewMode || !Visible || !IsHandleCreated || IsDisposed)
                return;

            Size size = ClientSize;
            if (size.Width < 2 || size.Height < 2)
                return;

            if (_captureBitmap == null ||
                _captureBitmap.Width != size.Width || _captureBitmap.Height != size.Height)
            {
                if (_captureBitmap != null)
                    _captureBitmap.Dispose();
                if (_blurredBitmap != null)
                    _blurredBitmap.Dispose();
                _captureBitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                _blurredBitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            }

            bool excluded = false;
            try
            {
                excluded = NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_EXCLUDEFROMCAPTURE);
                if (!excluded)
                {
                    DisablePreviewAndFallback();
                    return;
                }

                Point origin = PointToScreen(Point.Empty);
                Rectangle target = new Rectangle(origin, size);
                Rectangle visible = Rectangle.Intersect(target, SystemInformation.VirtualScreen);

                using (Graphics g = Graphics.FromImage(_captureBitmap))
                {
                    g.Clear(AppSettings.TintColor);
                    if (visible.Width > 0 && visible.Height > 0)
                    {
                        Point destination = new Point(visible.X - target.X, visible.Y - target.Y);
                        g.CopyFromScreen(visible.Location, destination, visible.Size,
                            CopyPixelOperation.SourceCopy);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!_captureErrorLogged)
                {
                    _captureErrorLogged = true;
                    Log.Write("实时采样屏幕失败，继续显示上一帧", ex);
                }
                return;
            }
            finally
            {
                if (excluded)
                    NativeMethods.SetWindowDisplayAffinity(Handle, NativeMethods.WDA_NONE);
            }

            double targetRadius = CurrentBlurRadius();
            int displayRadius = (int)Math.Ceiling(targetRadius);
            if (displayRadius > 0)
            {
                using (Graphics bg = Graphics.FromImage(_blurredBitmap))
                {
                    bg.CompositingMode = CompositingMode.SourceCopy;
                    bg.DrawImageUnscaled(_captureBitmap, 0, 0);
                }
                _blur.Blur(_blurredBitmap, displayRadius);
                _blurWeight = Math.Max(0f, Math.Min(1f, (float)(targetRadius / displayRadius)));
            }
            else
            {
                _blurWeight = 0f;
            }
            Invalidate();
        }

        private double CurrentBlurRadius()
        {
            int dpi = 96;
            try
            {
                if (IsHandleCreated)
                    dpi = (int)NativeMethods.GetDpiForWindow(Handle);
            }
            catch
            {
            }
            if (dpi <= 0)
                dpi = 96;
            double fraction = (100.0 - _settings.Clarity) / 60.0;
            return Math.Max(0.0, fraction * 6.0 * dpi / 96.0);
        }

        private void DisablePreviewAndFallback()
        {
            StopPreview();
            _previewMode = false;
            ApplyAppearance();
            ShowHint("当前系统不支持实时预览，已切换为强模糊遮挡", 4000);
            Invalidate();
        }

        private void RegisterHotkeys()
        {
            if (_hotkeysRegistered)
                return;
            _hotkeysRegistered = true;
            uint modifiers = NativeMethods.MOD_CONTROL | NativeMethods.MOD_ALT;
            if (!NativeMethods.RegisterHotKey(Handle, NativeMethods.HOTKEY_TOGGLE, modifiers, (uint)Keys.G))
                Log.Write("快捷键 Ctrl+Alt+G 注册失败", null);
            if (!NativeMethods.RegisterHotKey(Handle, NativeMethods.HOTKEY_RESELECT, modifiers, (uint)Keys.R))
                Log.Write("快捷键 Ctrl+Alt+R 注册失败", null);
        }

        private void UnregisterHotkeys()
        {
            if (!_hotkeysRegistered)
                return;
            _hotkeysRegistered = false;
            try
            {
                NativeMethods.UnregisterHotKey(Handle, NativeMethods.HOTKEY_TOGGLE);
                NativeMethods.UnregisterHotKey(Handle, NativeMethods.HOTKEY_RESELECT);
            }
            catch
            {
            }
        }

        private const int ButtonSettings = 1;
        private const int ButtonMinimize = 2;
        private const int ButtonClose = 3;

        private int BarHeight
        {
            get { return Theme.S(this, 34); }
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
            if (which == ButtonSettings)
                return new Rectangle(settingsX, top, width, height);
            if (which == ButtonMinimize)
                return new Rectangle(minimizeX, top, width, height);
            if (which == ButtonClose)
                return new Rectangle(closeX, top, width, height);
            return Rectangle.Empty;
        }

        private int ButtonAt(Point p)
        {
            if (ButtonRect(ButtonSettings).Contains(p)) return ButtonSettings;
            if (ButtonRect(ButtonMinimize).Contains(p)) return ButtonMinimize;
            if (ButtonRect(ButtonClose).Contains(p)) return ButtonClose;
            return 0;
        }

        private bool IsInteractivePoint(Point p)
        {
            if (p.X < 0 || p.Y < 0 || p.X >= ClientSize.Width || p.Y >= ClientSize.Height)
                return false;
            if (_editMode)
                return true;
            if (ButtonAt(p) != 0)
                return true;
            if (p.Y < BarHeight)
                return true;
            int grip = Math.Max(Theme.S(this, 8), 4);
            return p.X <= grip || p.X >= ClientSize.Width - grip || p.Y >= ClientSize.Height - grip;
        }

        private void HandleCaptionButton(int button)
        {
            if (button == ButtonSettings)
                _app.ShowSettings();
            else if (button == ButtonMinimize)
                _app.HidePane();
            else if (button == ButtonClose)
                _app.ExitApp();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_HOTKEY)
            {
                if (_app.IsSelecting)
                    return;
                int id = m.WParam.ToInt32();
                if (id == NativeMethods.HOTKEY_TOGGLE)
                    _app.ToggleVisibility();
                else if (id == NativeMethods.HOTKEY_RESELECT)
                    _app.Reselect();
                return;
            }
            base.WndProc(ref m);
        }

        public void SetTransparency(int value)
        {
            value = AppSettings.Clamp(value, 10, 90);
            if (value == _settings.Transparency)
                return;
            _settings.Transparency = value;
            if (!_previewMode)
                ApplyAppearance();
            Invalidate();
            _app.OnPaneStateChanged();
        }

        public void SetClarity(int value)
        {
            value = AppSettings.Clamp(value, 40, 100);
            if (value == _settings.Clarity)
                return;
            _settings.Clarity = value;
            Invalidate();
            _app.OnPaneStateChanged();
        }

        public void SetAlwaysOnTop(bool value)
        {
            if (_settings.AlwaysOnTop == value)
                return;
            _settings.AlwaysOnTop = value;
            TopMost = value;
            try
            {
                if (!_previewMode)
                    ApplyAppearance();
            }
            catch
            {
            }
            ShowHint(value ? "已置顶显示" : "已取消置顶，可被其他窗口覆盖", 2200);
            SyncOverlays();
            _app.SaveSettings();
            _app.OnPaneStateChanged();
        }

        private void StartAdjustTracking()
        {
            if (!Visible || !IsHandleCreated || IsDisposed)
                return;
            if (!_editMode)
                NativeMethods.SetInputTransparent(Handle, true);
            if (!_adjustTimer.Enabled)
                _adjustTimer.Start();
        }

        private void StopAdjustTracking()
        {
            _adjustTimer.Stop();
        }

        private void AdjustTimer_Tick(object sender, EventArgs e)
        {
            if (!Visible || IsDisposed || !IsHandleCreated)
                return;
            bool down = IsAdjustChordDown();
            if (down && !_editMode)
                EnterEditMode();
            else if (!down && _editMode && !_mouseDown && _dragMode == DragMode.None)
                ExitEditMode(false);
        }

        private bool IsAdjustChordDown()
        {
            int modifiers = AppSettings.NormalizeAdjustModifiers(_settings.AdjustModifiers);
            bool needCtrl = (modifiers & 1) != 0;
            bool needAlt = (modifiers & 2) != 0;
            bool needShift = (modifiers & 4) != 0;
            if (needCtrl && (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) == 0)
                return false;
            if (needAlt && (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU) & 0x8000) == 0)
                return false;
            if (needShift && (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT) & 0x8000) == 0)
                return false;
            return needCtrl || needAlt || needShift;
        }

        private void EnterEditMode()
        {
            if (_editMode)
                return;
            _editMode = true;
            if (IsHandleCreated)
                NativeMethods.SetInputTransparent(Handle, false);
            Invalidate();
        }

        private void ExitEditMode(bool force)
        {
            if (!_editMode)
                return;
            if (!force && (_mouseDown || _dragMode != DragMode.None))
                return;
            _editMode = false;
            _hover = false;
            _mouseDown = false;
            _dragMode = DragMode.None;
            if (IsHandleCreated)
                NativeMethods.SetInputTransparent(Handle, true);
            Cursor = Cursors.Default;
            Invalidate();
        }

        public void ShowHint(string text, int milliseconds)
        {
            _hint = text;
            _hintTimer.Stop();
            _hintTimer.Interval = Math.Max(800, milliseconds);
            _hintTimer.Start();
            Invalidate();
        }

        private void HintTimer_Tick(object sender, EventArgs e)
        {
            _hintTimer.Stop();
            _hint = null;
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不绘制背景，让 DWM 的 Acrylic 模糊从窗口底下透出来。
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Transparent);
            Theme.ConfigureGraphics(g);

            if (_previewMode)
            {
                Rectangle destination = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
                if (_captureBitmap != null)
                {
                    if (_blurredBitmap != null && _blurWeight >= 0.999f)
                    {
                        g.DrawImage(_blurredBitmap, destination);
                    }
                    else if (_blurredBitmap != null && _blurWeight > 0.001f)
                    {
                        g.DrawImage(_captureBitmap, destination);
                        _blendMatrix.Matrix33 = _blurWeight;
                        _blendAttributes.SetColorMatrix(_blendMatrix);
                        g.DrawImage(_blurredBitmap, destination, 0, 0, _blurredBitmap.Width,
                            _blurredBitmap.Height, GraphicsUnit.Pixel, _blendAttributes);
                    }
                    else
                    {
                        g.DrawImage(_captureBitmap, destination);
                    }
                }
                else
                {
                    using (SolidBrush background = new SolidBrush(AppSettings.TintColor))
                        g.FillRectangle(background, ClientRectangle);
                }

                int tintAlpha = _settings.AccentAlpha;
                if (tintAlpha > 0)
                {
                    using (SolidBrush veil = new SolidBrush(Color.FromArgb(tintAlpha, AppSettings.TintColor)))
                        g.FillRectangle(veil, ClientRectangle);
                }
            }

            int s = Math.Max(1, Theme.S(this, 1));
            int alpha = _editMode ? 120 : (_hover ? 90 : 45);
            using (Pen border = new Pen(Color.FromArgb(alpha, 255, 255, 255), s))
                g.DrawRectangle(border, 0, 0, Width - s, Height - s);

            DrawHint(g);
        }

        private void DrawTitleBar(Graphics g)
        {
            Rectangle bar = new Rectangle(0, 0, ClientSize.Width, BarHeight);
            if (bar.Height <= 2)
                return;

            using (LinearGradientBrush fill = new LinearGradientBrush(
                new Rectangle(bar.Left, bar.Top, bar.Width, bar.Height),
                Color.FromArgb(190, 27, 33, 44), Color.FromArgb(150, 17, 21, 29),
                LinearGradientMode.Vertical))
                g.FillRectangle(fill, bar);

            using (Pen topLine = new Pen(Color.FromArgb(48, 255, 255, 255), 1))
                g.DrawLine(topLine, 1, 1, Width - 2, 1);
            using (Pen bottomLine = new Pen(Color.FromArgb(65, 255, 255, 255), 1))
                g.DrawLine(bottomLine, 1, bar.Bottom - 1, Width - 2, bar.Bottom - 1);

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
            int textRight = ButtonRect(ButtonSettings).Left - Theme.S(this, 8);
            Rectangle titleRect = new Rectangle(textLeft, 0, Math.Max(0, textRight - textLeft), BarHeight);
            if (titleRect.Width > Theme.S(this, 20))
                Theme.DrawString(g, "毛玻璃", _barFont, Color.FromArgb(234, 246, 248, 252),
                    titleRect, ContentAlignment.MiddleLeft);

            DrawCaptionButton(g, ButtonSettings);
            DrawCaptionButton(g, ButtonMinimize);
            DrawCaptionButton(g, ButtonClose);
        }

        private void DrawCaptionButton(Graphics g, int which)
        {
            Rectangle button = ButtonRect(which);
            if (button.Width <= 0 || button.Height <= 0)
                return;

            bool hovered = _hoverButton == which;
            if (hovered)
            {
                Color fillColor = which == ButtonClose
                    ? Color.FromArgb(232, 196, 43, 28)
                    : Color.FromArgb(30, 255, 255, 255);
                using (GraphicsPath path = Theme.RoundedRect(button, Theme.S(this, 5)))
                using (SolidBrush fill = new SolidBrush(fillColor))
                    g.FillPath(fill, path);
            }

            int cx = button.Left + button.Width / 2;
            int cy = button.Top + button.Height / 2;
            Color iconColor = Color.FromArgb(which == ButtonClose && hovered ? 255 : 220, 245, 247, 250);
            using (Pen pen = new Pen(iconColor, Math.Max(1f, Theme.S(this, 1.3f))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (which == ButtonClose)
                {
                    int h = Theme.S(this, 4);
                    g.DrawLine(pen, cx - h, cy - h, cx + h, cy + h);
                    g.DrawLine(pen, cx + h, cy - h, cx - h, cy + h);
                }
                else if (which == ButtonMinimize)
                {
                    int h = Theme.S(this, 5);
                    g.DrawLine(pen, cx - h, cy, cx + h, cy);
                }
                else
                {
                    int left = cx - Theme.S(this, 6);
                    int right = cx + Theme.S(this, 6);
                    int gap = Theme.S(this, 4);
                    int r = Math.Max(1, Theme.S(this, 1.6f));
                    using (SolidBrush knob = new SolidBrush(iconColor))
                    {
                        for (int i = -1; i <= 1; i++)
                        {
                            int y = cy + i * gap;
                            g.DrawLine(pen, left, y, right, y);
                            int knobX = i == 0
                                ? left + Theme.S(this, 9)
                                : (i < 0 ? left + Theme.S(this, 4) : right - Theme.S(this, 4));
                            g.FillEllipse(knob, knobX - r, y - r, r * 2, r * 2);
                        }
                    }
                }
            }
        }

        private void DrawHint(Graphics g)
        {
            if (string.IsNullOrEmpty(_hint) || Width < Theme.S(this, 120))
                return;

            SizeF measured = g.MeasureString(_hint, _hintFont);
            int textHeight = Math.Max(1, (int)Math.Ceiling(measured.Height));
            int padX = Theme.S(this, 12);
            int padY = Theme.S(this, 7);
            int dot = Theme.S(this, 6);
            int gap = Theme.S(this, 8);
            int safety = Theme.S(this, 16);

            int maxWidth = Width - Theme.S(this, 18);
            int width = Math.Min(maxWidth, (int)Math.Ceiling(measured.Width) + safety + padX * 2 + dot + gap);
            if (width < Theme.S(this, 60))
                return;

            int pillTop = BarHeight + Theme.S(this, 8);
            if (pillTop + textHeight + padY * 2 > Height - Theme.S(this, 4))
                return;
            Rectangle pill = new Rectangle(Theme.S(this, 9), pillTop, width,
                textHeight + padY * 2);
            using (GraphicsPath path = Theme.RoundedRect(pill, pill.Height / 2))
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(232, 17, 21, 29)))
            using (Pen border = new Pen(Color.FromArgb(45, 255, 255, 255), 1))
            {
                g.FillPath(fill, path);
                g.DrawPath(border, path);
            }

            int cy = pill.Top + pill.Height / 2;
            using (SolidBrush dotBrush = new SolidBrush(Theme.Accent))
                g.FillEllipse(dotBrush, pill.Left + padX, cy - dot / 2, dot, dot);

            Rectangle textRect = new Rectangle(pill.Left + padX + dot + gap, pill.Top,
                width - padX * 2 - dot - gap, pill.Height);
            Theme.DrawString(g, _hint, _hintFont, Color.FromArgb(238, 246, 248, 252),
                textRect, ContentAlignment.MiddleLeft);
        }

        private DragMode EdgeAt(Point p)
        {
            int grip = Math.Max(Theme.S(this, 8), 4);
            bool left = p.X <= grip;
            bool right = p.X >= Width - grip;
            bool top = p.Y <= grip;
            bool bottom = p.Y >= Height - grip;

            if (top && left) return DragMode.NorthWest;
            if (top && right) return DragMode.NorthEast;
            if (bottom && left) return DragMode.SouthWest;
            if (bottom && right) return DragMode.SouthEast;
            if (top) return DragMode.North;
            if (bottom) return DragMode.South;
            if (left) return DragMode.West;
            if (right) return DragMode.East;
            return DragMode.Move;
        }

        private Cursor CursorFor(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.North:
                case DragMode.South:
                    return Cursors.SizeNS;
                case DragMode.East:
                case DragMode.West:
                    return Cursors.SizeWE;
                case DragMode.NorthWest:
                case DragMode.SouthEast:
                    return Cursors.SizeNWSE;
                case DragMode.NorthEast:
                case DragMode.SouthWest:
                    return Cursors.SizeNESW;
                default:
                    return Cursors.SizeAll;
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            if (_hoverButton != 0)
            {
                _hoverButton = 0;
            }
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
                HandleCaptionButton(button);
                return;
            }
            if (!IsInteractivePoint(e.Location))
                return;

            _mouseDown = true;
            _menu.Close();
            _dragMode = EdgeAt(e.Location);
            _dragStartCursor = Cursor.Position;
            _dragStartBounds = Bounds;
            _dragMoved = false;
            Capture = true;
            _hintTimer.Stop();
            _hint = null;
            Invalidate();
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
                else if (e.Y < BarHeight && EdgeAt(e.Location) == DragMode.Move)
                    Cursor = Cursors.Default;
                else
                    Cursor = CursorFor(EdgeAt(e.Location));
                return;
            }

            Point now = Cursor.Position;
            int dx = now.X - _dragStartCursor.X;
            int dy = now.Y - _dragStartCursor.Y;
            if (!_dragMoved && (Math.Abs(dx) > 1 || Math.Abs(dy) > 1))
                _dragMoved = true;

            Rectangle bounds = _dragStartBounds;
            int minWidth = MinimumSize.Width;
            int minHeight = MinimumSize.Height;

            if (_dragMode == DragMode.Move)
            {
                Rectangle vs = SystemInformation.VirtualScreen;
                int keepX = Math.Min(Theme.S(this, 80), bounds.Width);
                int keepY = Math.Min(Theme.S(this, 50), bounds.Height);
                int x = bounds.X + dx;
                int y = bounds.Y + dy;
                x = Math.Max(vs.Left - bounds.Width + keepX, Math.Min(vs.Right - keepX, x));
                y = Math.Max(vs.Top - bounds.Height + keepY, Math.Min(vs.Bottom - keepY, y));
                bounds = new Rectangle(x, y, bounds.Width, bounds.Height);
            }
            else
            {
                bool west = _dragMode == DragMode.West || _dragMode == DragMode.NorthWest || _dragMode == DragMode.SouthWest;
                bool east = _dragMode == DragMode.East || _dragMode == DragMode.NorthEast || _dragMode == DragMode.SouthEast;
                bool north = _dragMode == DragMode.North || _dragMode == DragMode.NorthWest || _dragMode == DragMode.NorthEast;
                bool south = _dragMode == DragMode.South || _dragMode == DragMode.SouthWest || _dragMode == DragMode.SouthEast;

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
                if (south)
                    bounds.Height = Math.Max(minHeight, bounds.Height + dy);
            }

            if (bounds != Bounds)
                Bounds = bounds;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                if (IsInteractivePoint(e.Location))
                {
                    UpdateMenuState();
                    _menu.Show(Cursor.Position);
                }
                return;
            }
            if (e.Button != MouseButtons.Left)
                return;

            _mouseDown = false;
            if (_dragMode == DragMode.None)
            {
                if (_editMode && !IsAdjustChordDown())
                    ExitEditMode(true);
                return;
            }

            Capture = false;
            bool moved = _dragMoved;
            _dragMode = DragMode.None;
            int button = ButtonAt(e.Location);
            if (button != 0)
                Cursor = Cursors.Hand;
            else if (e.Y < BarHeight && EdgeAt(e.Location) == DragMode.Move)
                Cursor = Cursors.Default;
            else
                Cursor = CursorFor(EdgeAt(e.Location));
            if (moved)
            {
                _settings.LastRect = Bounds;
                _settings.HasLastRect = true;
                _app.SaveSettings();
            }
            Invalidate();
            if (_editMode && !IsAdjustChordDown())
                ExitEditMode(true);
        }

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.BackColor = Color.FromArgb(24, 29, 38);
            menu.ForeColor = Theme.TextPrimary;
            menu.Font = Theme.Font(9.5f);
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false;
            menu.Renderer = new DarkMenuRenderer();
            menu.Opening += delegate { UpdateMenuState(); };

            ToolStripMenuItem settings = new ToolStripMenuItem("设置…");
            settings.Click += delegate { _app.ShowSettings(); };
            menu.Items.Add(settings);
            menu.Items.Add(new ToolStripSeparator());

            _menuTopMost = new ToolStripMenuItem("总在最上层");
            _menuTopMost.Click += delegate { SetAlwaysOnTop(!_settings.AlwaysOnTop); };
            menu.Items.Add(_menuTopMost);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem reselect = new ToolStripMenuItem("重新框选区域");
            reselect.ShortcutKeyDisplayString = "Ctrl+Alt+R";
            reselect.Click += delegate { _app.Reselect(); };
            menu.Items.Add(reselect);

            _menuHide = new ToolStripMenuItem("隐藏毛玻璃");
            _menuHide.ShortcutKeyDisplayString = "Ctrl+Alt+G";
            _menuHide.Click += delegate { _app.HidePane(); };
            menu.Items.Add(_menuHide);

            menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("退出毛玻璃");
            exit.Click += delegate { _app.ExitApp(); };
            menu.Items.Add(exit);

            return menu;
        }

        private void UpdateMenuState()
        {
            SetChecked(_menuTopMost, _settings.AlwaysOnTop, "总在最上层");
            _menuHide.Text = "隐藏毛玻璃";
        }

        private static void SetChecked(ToolStripMenuItem item, bool value, string text)
        {
            item.Text = (value ? "✓  " : "    ") + text;
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (!_syncingOverlays)
                SyncOverlays();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (!_syncingOverlays)
                SyncOverlays();
        }

        private void EnsureOverlays()
        {
            if (!Visible || !IsHandleCreated || IsDisposed)
                return;
            if (_resizer == null)
                _resizer = new PaneResizer(this);
            if (_bar == null)
                _bar = new PaneBar(this);
            if (!_resizer.Visible)
                _resizer.Show();
            if (!_bar.Visible)
                _bar.Show();
            SyncOverlays();
            _resizer.BringToFront();
            _bar.BringToFront();
        }

        private void SyncOverlays()
        {
            if (_bar != null)
                _bar.SyncFromPane();
            if (_resizer != null)
                _resizer.SyncFromPane();
        }

        private void HideOverlays()
        {
            if (_bar != null && _bar.Visible)
                _bar.Hide();
            if (_resizer != null && _resizer.Visible)
                _resizer.Hide();
        }

        public void ApplyBoundsFromOverlay(Rectangle bounds)
        {
            _syncingOverlays = true;
            try
            {
                Bounds = bounds;
            }
            finally
            {
                _syncingOverlays = false;
            }
            SyncOverlays();
        }

        public void SaveGeometryFromOverlay()
        {
            _settings.LastRect = Bounds;
            _settings.HasLastRect = true;
            _app.SaveSettings();
        }

        public void OpenSettingsFromOverlay()
        {
            _app.ShowSettings();
        }

        public void MinimizeFromOverlay()
        {
            _app.HidePane();
        }

        public void CloseFromOverlay()
        {
            _app.ExitApp();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UnregisterHotkeys();
                _adjustTimer.Stop();
                _adjustTimer.Dispose();
                _previewTimer.Stop();
                _previewTimer.Dispose();
                _hintTimer.Stop();
                _hintTimer.Dispose();
                HideOverlays();
                if (_bar != null)
                {
                    _bar.Dispose();
                    _bar = null;
                }
                if (_resizer != null)
                {
                    _resizer.Dispose();
                    _resizer = null;
                }
                _menu.Dispose();
                _hintFont.Dispose();
                _barFont.Dispose();
                _blur.Dispose();
                _blendAttributes.Dispose();
                if (_captureBitmap != null)
                {
                    _captureBitmap.Dispose();
                    _captureBitmap = null;
                }
                if (_blurredBitmap != null)
                {
                    _blurredBitmap.Dispose();
                    _blurredBitmap = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}
