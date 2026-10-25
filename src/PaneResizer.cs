using System;
using System.Drawing;
using System.Windows.Forms;

namespace FrostGlass
{
    /// <summary>
    /// Invisible input-only frame along the left, right and bottom edges. The
    /// glass itself passes all input through; these thin strips only make the
    /// border draggable for resizing. The title bar covers the top edge.
    /// </summary>
    internal sealed class PaneResizer : Form
    {
        private enum DragMode { None, West, East, South, SouthWest, SouthEast }

        private readonly FrostPane _pane;
        private DragMode _dragMode;
        private Point _dragStartCursor;
        private Rectangle _dragStartPaneBounds;

        public PaneResizer(FrostPane pane)
        {
            _pane = pane;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            TopMost = pane.TopMost;
            BackColor = Color.Black;
            Text = "毛玻璃边框";
            DoubleBuffered = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            SyncFromPane();
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
            // Almost transparent: the real border is painted by the glass pane.
            NativeMethods.SetLayeredWindowAttributes(Handle, 0, 1, NativeMethods.LWA_ALPHA);
            UpdateRegion();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        public void SyncFromPane()
        {
            Bounds = _pane.Bounds;
            TopMost = _pane.TopMost;
            if (IsHandleCreated)
                UpdateRegion();
        }

        private void UpdateRegion()
        {
            if (!IsHandleCreated || ClientSize.Width < 4 || ClientSize.Height < 4)
                return;
            int border = Theme.S(this, 6);
            int bar = Theme.S(this, 34);
            Region region = new Region();
            region.MakeEmpty();
            region.Union(new Rectangle(0, bar, border, Height - bar));
            region.Union(new Rectangle(Width - border, bar, border, Height - bar));
            region.Union(new Rectangle(0, Height - border, Width, border));
            Region old = Region;
            Region = region;
            if (old != null)
                old.Dispose();
        }

        private DragMode EdgeAt(Point p)
        {
            int border = Theme.S(this, 6);
            bool left = p.X < border;
            bool right = p.X >= Width - border;
            bool bottom = p.Y >= Height - border;
            if (left && bottom) return DragMode.SouthWest;
            if (right && bottom) return DragMode.SouthEast;
            if (left) return DragMode.West;
            if (right) return DragMode.East;
            if (bottom) return DragMode.South;
            return DragMode.None;
        }

        private Cursor CursorFor(DragMode mode)
        {
            switch (mode)
            {
                case DragMode.West:
                case DragMode.East:
                    return Cursors.SizeWE;
                case DragMode.South:
                    return Cursors.SizeNS;
                case DragMode.SouthWest:
                    return Cursors.SizeNESW;
                case DragMode.SouthEast:
                    return Cursors.SizeNWSE;
                default:
                    return Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;
            _dragMode = EdgeAt(e.Location);
            if (_dragMode == DragMode.None)
                return;
            _dragStartCursor = Cursor.Position;
            _dragStartPaneBounds = _pane.Bounds;
            Capture = true;
            Cursor = CursorFor(_dragMode);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragMode == DragMode.None)
            {
                Cursor = CursorFor(EdgeAt(e.Location));
                return;
            }

            Point now = Cursor.Position;
            int dx = now.X - _dragStartCursor.X;
            int dy = now.Y - _dragStartCursor.Y;
            Rectangle bounds = _dragStartPaneBounds;
            int minWidth = Theme.S(this, 200);
            int minHeight = Theme.S(this, 90);

            bool west = _dragMode == DragMode.West || _dragMode == DragMode.SouthWest;
            bool east = _dragMode == DragMode.East || _dragMode == DragMode.SouthEast;
            bool south = _dragMode == DragMode.South || _dragMode == DragMode.SouthWest || _dragMode == DragMode.SouthEast;

            if (west)
            {
                int newLeft = Math.Min(bounds.Right - minWidth, bounds.Left + dx);
                bounds.Width = bounds.Right - newLeft;
                bounds.X = newLeft;
            }
            if (east)
                bounds.Width = Math.Max(minWidth, bounds.Width + dx);
            if (south)
                bounds.Height = Math.Max(minHeight, bounds.Height + dy);

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
            if (dragging)
                _pane.SaveGeometryFromOverlay();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // Painted by the glass pane; this window only captures edge drags.
        }
    }
}
