using System.Drawing;
using System.Windows.Forms;

namespace FrostGlass
{
    internal sealed class DarkMenuColorTable : ProfessionalColorTable
    {
        private static readonly Color MenuBackground = Color.FromArgb(24, 29, 38);
        private static readonly Color MenuHover = Color.FromArgb(48, 58, 74);
        private static readonly Color MenuBorderColor = Color.FromArgb(62, 73, 90);

        public override Color ToolStripDropDownBackground { get { return MenuBackground; } }
        public override Color ImageMarginGradientBegin { get { return MenuBackground; } }
        public override Color ImageMarginGradientMiddle { get { return MenuBackground; } }
        public override Color ImageMarginGradientEnd { get { return MenuBackground; } }
        public override Color MenuBorder { get { return MenuBorderColor; } }
        public override Color MenuItemBorder { get { return Color.Transparent; } }
        public override Color MenuItemSelected { get { return MenuHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return MenuHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return MenuHover; } }
        public override Color SeparatorDark { get { return Color.FromArgb(40, 48, 61); } }
        public override Color SeparatorLight { get { return Color.FromArgb(40, 48, 61); } }
        public override Color MenuItemPressedGradientBegin { get { return MenuHover; } }
        public override Color MenuItemPressedGradientEnd { get { return MenuHover; } }
    }

    internal sealed class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer()
            : base(new DarkMenuColorTable())
        {
        }
    }
}
