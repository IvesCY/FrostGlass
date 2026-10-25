using System;
using System.Drawing;
using System.Windows.Forms;

namespace FrostGlass
{
    internal sealed class TrayAppContext : ApplicationContext
    {
        private readonly AppSettings _settings;
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private Icon _trayIcon;
        private bool _ownsTrayIcon;
        private FrostPane _pane;
        private SettingsPanel _panel;
        private Timer _startupTimer;
        private Timer _exitTimer;
        private bool _selecting;
        private bool _exiting;

        private ToolStripMenuItem _menuShowHide;
        private ToolStripMenuItem _menuSettings;
        private ToolStripMenuItem _menuReselect;
        private ToolStripMenuItem _menuTopMost;
        private ToolStripMenuItem _menuExit;

        public TrayAppContext(AppSettings settings)
        {
            _settings = settings;
            BuildTray();

            _startupTimer = new Timer();
            _startupTimer.Interval = 150;
            _startupTimer.Tick += StartupTimer_Tick;
            _startupTimer.Start();
        }

        public bool IsSelecting
        {
            get { return _selecting; }
        }

        private void StartupTimer_Tick(object sender, EventArgs e)
        {
            _startupTimer.Stop();
            _startupTimer.Dispose();
            _startupTimer = null;
            RunSelection(true);
        }

        private void BuildTray()
        {
            try
            {
                using (Icon source = Icon.ExtractAssociatedIcon(Application.ExecutablePath))
                {
                    if (source != null)
                    {
                        _trayIcon = new Icon(source, SystemInformation.SmallIconSize);
                        _ownsTrayIcon = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("加载应用图标失败", ex);
            }
            if (_trayIcon == null)
            {
                _trayIcon = SystemIcons.Application;
                _ownsTrayIcon = false;
            }

            _trayMenu = new ContextMenuStrip();
            _trayMenu.BackColor = Color.FromArgb(24, 29, 38);
            _trayMenu.ForeColor = Theme.TextPrimary;
            _trayMenu.Font = Theme.Font(9.5f);
            _trayMenu.ShowImageMargin = false;
            _trayMenu.ShowCheckMargin = false;
            _trayMenu.Renderer = new DarkMenuRenderer();
            _trayMenu.Opening += delegate { UpdateTrayState(); };

            _menuShowHide = new ToolStripMenuItem("隐藏毛玻璃");
            _menuShowHide.ShortcutKeyDisplayString = "Ctrl+Alt+G";
            _menuShowHide.Click += delegate { ToggleVisibility(); };
            _trayMenu.Items.Add(_menuShowHide);

            _menuSettings = new ToolStripMenuItem("设置…");
            _menuSettings.Click += delegate { ShowSettings(); };
            _trayMenu.Items.Add(_menuSettings);

            _menuReselect = new ToolStripMenuItem("重新框选区域");
            _menuReselect.ShortcutKeyDisplayString = "Ctrl+Alt+R";
            _menuReselect.Click += delegate { Reselect(); };
            _trayMenu.Items.Add(_menuReselect);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _menuTopMost = new ToolStripMenuItem("总在最上层");
            _menuTopMost.Click += delegate { ToggleTopMostFromTray(); };
            _trayMenu.Items.Add(_menuTopMost);

            _trayMenu.Items.Add(new ToolStripSeparator());

            _menuExit = new ToolStripMenuItem("退出毛玻璃");
            _menuExit.Click += delegate { ExitApp(); };
            _trayMenu.Items.Add(_menuExit);

            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "毛玻璃 · 单击打开设置";
            _tray.Visible = true;
            _tray.ContextMenuStrip = _trayMenu;
            _tray.MouseClick += Tray_MouseClick;
        }

        private void Tray_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                ShowSettings();
        }

        private void ToggleTopMostFromTray()
        {
            if (_pane == null)
                return;
            _pane.SetAlwaysOnTop(!_settings.AlwaysOnTop);
            UpdateTrayState();
        }

        private void RunSelection(bool initial)
        {
            if (_selecting || _exiting)
                return;

            _selecting = true;
            FrostPane oldPane = _pane;
            bool hadPane = oldPane != null;
            bool wasVisible = hadPane && oldPane.Visible;

            try
            {
                HidePanel();
                if (hadPane)
                {
                    oldPane.Hide();
                    Application.DoEvents();
                    try { NativeMethods.DwmFlush(); }
                    catch { }
                    System.Threading.Thread.Sleep(110);
                }

                Rectangle? lastRect = null;
                if (_settings.HasLastRect && IsRectOnScreen(_settings.LastRect))
                    lastRect = _settings.LastRect;

                using (SelectorForm selector = new SelectorForm(lastRect))
                {
                    DialogResult result = selector.ShowDialog();
                    if (result == DialogResult.OK &&
                        selector.SelectedRect.Width >= 24 && selector.SelectedRect.Height >= 24)
                    {
                        Rectangle rect = selector.SelectedRect;
                        DisposePanel();
                        if (_pane != null)
                        {
                            _pane.Dispose();
                            _pane = null;
                        }
                        CreatePane(rect);
                        _settings.LastRect = rect;
                        _settings.HasLastRect = true;
                        SaveSettings();
                    }
                    else if (initial)
                    {
                        ScheduleExit();
                    }
                    else if (hadPane && wasVisible)
                    {
                        _pane.Show();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Write("框选区域失败", ex);
                if (_pane == null && initial)
                    ScheduleExit();
                else
                    MessageBox.Show("框选区域时出现问题：" + ex.Message, "毛玻璃",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _selecting = false;
                UpdateTrayState();
            }
        }

        private void CreatePane(Rectangle rect)
        {
            _pane = new FrostPane(this, _settings, rect);
            _pane.Show();
            UpdateTrayState();

            _pane.ShowHint("拖动标题栏移动 · 拖边缘缩放 · 右上角设置", 5200);
        }

        private bool IsRectOnScreen(Rectangle rect)
        {
            Rectangle visible = Rectangle.Intersect(rect, SystemInformation.VirtualScreen);
            return visible.Width >= 24 && visible.Height >= 24;
        }

        public void ToggleVisibility()
        {
            if (_exiting || _selecting || _pane == null)
                return;

            if (_pane.Visible)
            {
                _pane.Hide();
                HidePanel();
            }
            else
            {
                _pane.Show();
            }
            UpdateTrayState();
        }

        public void HidePane()
        {
            if (_pane == null)
                return;
            _pane.Hide();
            HidePanel();
            UpdateTrayState();
        }

        public void Reselect()
        {
            if (_exiting || _selecting || _pane == null)
                return;
            RunSelection(false);
        }

        public void ShowSettings()
        {
            if (_exiting || _selecting || _pane == null)
                return;

            if (!_pane.Visible)
                _pane.Show();

            if (_panel == null)
            {
                _panel = new SettingsPanel(this, _settings, _pane);
                // 先创建句柄，取得真实尺寸后再计算位置，避免面板第一次出现在左上角。
                IntPtr unused = _panel.Handle;
            }

            _panel.SyncFromSettings();
            _panel.PositionNear(_pane.Bounds);
            if (!_panel.Visible)
                _panel.Show();
            // WinForms 首次创建边框时会多算一段标题栏高度，显示后按设计尺寸校正。
            _panel.ApplyDesignSize();
            _panel.PositionNear(_pane.Bounds);
            _panel.BringToFront();
            _panel.Activate();
            UpdateTrayState();
        }

        private void HidePanel()
        {
            if (_panel != null && _panel.Visible)
                _panel.Hide();
        }

        private void DisposePanel()
        {
            if (_panel != null)
            {
                _panel.Dispose();
                _panel = null;
            }
        }

        public void OnPaneStateChanged()
        {
            if (_panel != null && _panel.Visible)
                _panel.SyncFromSettings();
            UpdateTrayState();
        }

        public void SaveSettings()
        {
            _settings.Save();
        }

        private void UpdateTrayState()
        {
            if (_tray == null)
                return;

            bool hasPane = _pane != null;
            bool visible = hasPane && _pane.Visible;

            if (!hasPane)
                _tray.Text = "毛玻璃";
            else if (!visible)
                _tray.Text = "毛玻璃（已隐藏）";
            else
                _tray.Text = "毛玻璃（已启用）";

            _menuShowHide.Enabled = hasPane;
            _menuShowHide.Text = visible ? "隐藏毛玻璃" : "显示毛玻璃";
            _menuSettings.Enabled = hasPane;
            _menuReselect.Enabled = hasPane;
            _menuTopMost.Enabled = hasPane;
            SetMenuCheck(_menuTopMost, _settings.AlwaysOnTop, "总在最上层");
        }

        private static void SetMenuCheck(ToolStripMenuItem item, bool value, string text)
        {
            item.Text = (value ? "✓  " : "    ") + text;
        }

        public void ExitApp()
        {
            if (_exiting)
                return;
            _exiting = true;
            HidePanel();
            _exitTimer = new Timer();
            _exitTimer.Interval = 80;
            _exitTimer.Tick += ExitTimer_Tick;
            _exitTimer.Start();
        }

        private void ExitTimer_Tick(object sender, EventArgs e)
        {
            _exitTimer.Stop();
            _exitTimer.Dispose();
            _exitTimer = null;
            ExitAppNow();
        }

        private void ScheduleExit()
        {
            if (!_exiting)
                ExitApp();
        }

        private void ExitAppNow()
        {
            SaveSettings();

            if (_startupTimer != null)
            {
                _startupTimer.Stop();
                _startupTimer.Dispose();
                _startupTimer = null;
            }

            DisposePanel();

            if (_pane != null)
            {
                _pane.Dispose();
                _pane = null;
            }

            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.ContextMenuStrip = null;
                _tray.Dispose();
                _tray = null;
            }

            if (_trayMenu != null)
            {
                _trayMenu.Dispose();
                _trayMenu = null;
            }

            if (_trayIcon != null && _ownsTrayIcon)
                _trayIcon.Dispose();
            _trayIcon = null;

            ExitThread();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_exitTimer != null)
                {
                    _exitTimer.Stop();
                    _exitTimer.Dispose();
                    _exitTimer = null;
                }
                if (_startupTimer != null)
                {
                    _startupTimer.Stop();
                    _startupTimer.Dispose();
                    _startupTimer = null;
                }
                DisposePanel();
                if (_pane != null)
                {
                    _pane.Dispose();
                    _pane = null;
                }
                if (_tray != null)
                {
                    _tray.Visible = false;
                    _tray.Dispose();
                    _tray = null;
                }
                if (_trayMenu != null)
                {
                    _trayMenu.Dispose();
                    _trayMenu = null;
                }
                if (_trayIcon != null && _ownsTrayIcon)
                    _trayIcon.Dispose();
                _trayIcon = null;
            }
            base.Dispose(disposing);
        }
    }
}
