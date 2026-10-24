using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ProzessorAffinityManager
{
    internal class Palette
    {
        public bool IsDark;
        public Color Back, Surface, Text, SubText, Border, Accent, AccentHover, AccentText, Selection, ButtonBack, ButtonHover;
    }

    /// <summary>
    /// Light and dark theme in black/gray/white; follows the Windows "app mode" setting.
    /// </summary>
    internal static class Theme
    {
        // The first installed font is used (Segoe UI Variable is available from Windows 11).
        // Scripts without Latin letters need a font that contains those characters.
        private static readonly string[] LatinFonts = { "Segoe UI Variable Text", "Segoe UI" };
        private static readonly string[] LatinTitleFonts = { "Segoe UI Variable Text Semibold", "Segoe UI Semibold" };

        private static string[] FontsFor(string language)
        {
            switch (language)
            {
                case "zh": return new[] { "Microsoft YaHei UI", "Microsoft YaHei" };
                case "ja": return new[] { "Yu Gothic UI", "Meiryo UI" };
                case "ko": return new[] { "Malgun Gothic" };
                case "hi":
                case "mr":
                case "bn":
                case "te":
                case "ta": return new[] { "Nirmala UI" };
                case "ar":
                case "ur": return new[] { "Segoe UI" };
                default: return null;
            }
        }

        private static readonly FontFamily[] Installed = new System.Drawing.Text.InstalledFontCollection().Families;

        /// <summary>Heading font of the cards, matching the current language.</summary>
        public static Font TitleFont { get; private set; } = FirstInstalled(LatinTitleFonts, 10.5f, FontStyle.Regular);

        /// <summary>Returns the font for the language and updates <see cref="TitleFont"/>.</summary>
        public static Font UpdateFonts(string language, float size)
        {
            var special = FontsFor(language);
            var font = FirstInstalled(special ?? LatinFonts, size, FontStyle.Regular);
            var title = special == null
                ? FirstInstalled(LatinTitleFonts, size + 0.5f, FontStyle.Regular)
                : FirstInstalled(special, size + 0.5f, FontStyle.Bold);

            var old = TitleFont;
            TitleFont = title;
            old.Dispose();
            return font;
        }

        private static Font FirstInstalled(string[] names, float size, FontStyle style)
        {
            foreach (string name in names)
                if (Array.Exists(Installed, f => f.Name == name))
                    return new Font(name, size, style);
            return new Font(FontFamily.GenericSansSerif, size, style);
        }

        public static readonly Palette Light = new Palette
        {
            IsDark = false,
            Back = Color.FromArgb(0xF5, 0xF4, 0xED),
            Surface = Color.FromArgb(0xFF, 0xFF, 0xFF),
            Text = Color.FromArgb(0x14, 0x14, 0x13),
            SubText = Color.FromArgb(0x73, 0x72, 0x6C),
            Border = Color.FromArgb(0xE0, 0xDE, 0xD5),
            Accent = Color.FromArgb(0x14, 0x14, 0x13),
            AccentHover = Color.FromArgb(0x3D, 0x3D, 0x3A),
            AccentText = Color.FromArgb(0xFA, 0xF9, 0xF5),
            Selection = Color.FromArgb(0xEB, 0xE9, 0xE1),
            ButtonBack = Color.FromArgb(0xFF, 0xFF, 0xFF),
            ButtonHover = Color.FromArgb(0xF0, 0xEE, 0xE6),
        };

        public static readonly Palette Dark = new Palette
        {
            IsDark = true,
            Back = Color.FromArgb(0x26, 0x26, 0x24),
            Surface = Color.FromArgb(0x30, 0x30, 0x2E),
            Text = Color.FromArgb(0xFA, 0xF9, 0xF5),
            SubText = Color.FromArgb(0xA6, 0xA3, 0x9A),
            Border = Color.FromArgb(0x45, 0x44, 0x3F),
            Accent = Color.FromArgb(0xFA, 0xF9, 0xF5),
            AccentHover = Color.FromArgb(0xDE, 0xDC, 0xD4),
            AccentText = Color.FromArgb(0x1F, 0x1E, 0x1D),
            Selection = Color.FromArgb(0x45, 0x45, 0x41),
            ButtonBack = Color.FromArgb(0x3A, 0x3A, 0x37),
            ButtonHover = Color.FromArgb(0x45, 0x44, 0x40),
        };

        public static Palette Current { get; private set; } = Light;

        /// <summary>Re-reads the Windows theme. Returns true if it has changed.</summary>
        public static bool Refresh()
        {
            bool dark = false;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                dark = key?.GetValue("AppsUseLightTheme") is int value && value == 0;

            var next = dark ? Dark : Light;
            bool changed = next != Current || !(ToolStripManager.Renderer is ThemedRenderer);
            Current = next;
            if (changed)
                ToolStripManager.Renderer = new ThemedRenderer(next);
            return changed;
        }

        public static void Apply(Control root) => ApplyTo(root, Current.Back);

        private static void ApplyTo(Control c, Color background)
        {
            var p = Current;
            Color childBackground = background;

            switch (c)
            {
                case Form form:
                    form.BackColor = p.Back;
                    form.ForeColor = p.Text;
                    ApplyTitleBar(form);
                    break;
                case CardPanel card:
                    card.BackColor = background;
                    card.ForeColor = p.Text;
                    childBackground = p.Surface;
                    break;
                case ListView list:
                    list.BackColor = p.Surface;
                    list.ForeColor = p.Text;
                    ApplyScrollbars(list);
                    break;
                case TextBox box:
                    box.BackColor = p.Back;
                    box.ForeColor = p.Text;
                    break;
                case Button button:
                    StyleButton(button);
                    break;
                case ToolStrip strip:
                    strip.BackColor = p.Back;
                    strip.ForeColor = p.Text;
                    break;
                default:
                    c.BackColor = background;
                    c.ForeColor = p.Text;
                    break;
            }

            c.Invalidate();
            foreach (Control child in c.Controls)
                ApplyTo(child, childBackground);
        }

        private static void StyleButton(Button b)
        {
            var p = Current;
            bool primary = (b.Tag as string) == "primary";
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.Cursor = Cursors.Hand;
            b.BackColor = primary ? p.Accent : p.ButtonBack;
            b.ForeColor = primary ? p.AccentText : p.Text;
            b.FlatAppearance.BorderColor = primary ? p.Accent : p.Border;
            b.FlatAppearance.MouseOverBackColor = primary ? p.AccentHover : p.ButtonHover;
            b.FlatAppearance.MouseDownBackColor = primary ? p.AccentHover : p.Border;
        }

        // ---------------------------------------------------------------- Windows window frame

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        /// <summary>Dark title bar and (from Windows 11) a title bar in the background color.</summary>
        public static void ApplyTitleBar(Form form)
        {
            if (!form.IsHandleCreated)
                return;

            int dark = Current.IsDark ? 1 : 0;
            if (DwmSetWindowAttribute(form.Handle, 20, ref dark, 4) != 0)
                DwmSetWindowAttribute(form.Handle, 19, ref dark, 4);

            int caption = ColorRef(Current.Back);
            int text = ColorRef(Current.Text);
            DwmSetWindowAttribute(form.Handle, 35, ref caption, 4);
            DwmSetWindowAttribute(form.Handle, 36, ref text, 4);
        }

        /// <summary>Dark scrollbars for lists.</summary>
        public static void ApplyScrollbars(Control c)
        {
            if (c.IsHandleCreated)
                SetWindowTheme(c.Handle, Current.IsDark ? "DarkMode_Explorer" : "Explorer", null);
        }

        private static int ColorRef(Color c) => c.R | (c.G << 8) | (c.B << 16);

        public static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            float d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    /// <summary>Rounded card with a heading (replaces the GroupBox).</summary>
    [DesignerCategory("")]
    internal class CardPanel : Panel
    {
        private const int TitleHeight = 26;

        public CardPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            Padding = new Padding(12, TitleHeight + 10, 12, 12);
        }

        [Browsable(true)]
        public override string Text
        {
            get => base.Text;
            set { base.Text = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            TextRenderer.DrawText(g, Text, Theme.TitleFont, new Point(2, 4), p.Text);

            var rect = new RectangleF(0.5f, TitleHeight + 0.5f, Width - 1.5f, Height - TitleHeight - 1.5f);
            using (var path = Theme.RoundedRect(rect, 8))
            using (var fill = new SolidBrush(p.Surface))
            using (var pen = new Pen(p.Border))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>Checkbox with a rounded box in the accent color.</summary>
    [DesignerCategory("")]
    internal class ThemedCheckBox : CheckBox
    {
        private bool hover;

        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var p = Theme.Current;
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            const float size = 16;
            var box = new RectangleF(1, (Height - size) / 2f, size, size);
            using (var path = Theme.RoundedRect(box, 4))
            {
                if (Checked)
                {
                    using (var fill = new SolidBrush(hover ? p.AccentHover : p.Accent))
                        g.FillPath(fill, path);
                    using (var pen = new Pen(p.AccentText, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                        g.DrawLines(pen, new[]
                        {
                            new PointF(box.Left + 4, box.Top + 8.5f),
                            new PointF(box.Left + 7, box.Top + 11.5f),
                            new PointF(box.Left + 12, box.Top + 5),
                        });
                }
                else
                {
                    using (var fill = new SolidBrush(p.Back))
                        g.FillPath(fill, path);
                    using (var pen = new Pen(hover ? p.Accent : p.SubText, 1.2f))
                        g.DrawPath(pen, path);
                }
            }

            var textRect = new Rectangle((int)size + 8, 0, Width - (int)size - 8, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, p.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
    }

    /// <summary>Menus, tray menu and status bar in the theme colors.</summary>
    internal class ThemedRenderer : ToolStripProfessionalRenderer
    {
        private readonly Palette p;

        public ThemedRenderer(Palette palette) : base(new ThemedColorTable(palette))
        {
            p = palette;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? p.Text : p.SubText;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = p.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(p.Accent, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                e.Graphics.DrawLines(pen, new[]
                {
                    new PointF(r.Left + r.Width * 0.2f, r.Top + r.Height * 0.52f),
                    new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.74f),
                    new PointF(r.Left + r.Width * 0.82f, r.Top + r.Height * 0.28f),
                });
        }
    }

    internal class ThemedColorTable : ProfessionalColorTable
    {
        private readonly Palette p;

        public ThemedColorTable(Palette palette)
        {
            p = palette;
            UseSystemColors = false;
        }

        public override Color MenuStripGradientBegin => p.Back;
        public override Color MenuStripGradientEnd => p.Back;
        public override Color StatusStripGradientBegin => p.Back;
        public override Color StatusStripGradientEnd => p.Back;
        public override Color ToolStripBorder => p.Back;
        public override Color ToolStripDropDownBackground => p.Surface;
        public override Color ImageMarginGradientBegin => p.Surface;
        public override Color ImageMarginGradientMiddle => p.Surface;
        public override Color ImageMarginGradientEnd => p.Surface;
        public override Color MenuBorder => p.Border;
        public override Color MenuItemBorder => p.Selection;
        public override Color MenuItemSelected => p.Selection;
        public override Color MenuItemSelectedGradientBegin => p.Selection;
        public override Color MenuItemSelectedGradientEnd => p.Selection;
        public override Color MenuItemPressedGradientBegin => p.Surface;
        public override Color MenuItemPressedGradientMiddle => p.Surface;
        public override Color MenuItemPressedGradientEnd => p.Surface;
        public override Color SeparatorDark => p.Border;
        public override Color SeparatorLight => p.Border;
        public override Color CheckBackground => Color.Transparent;
        public override Color CheckSelectedBackground => Color.Transparent;
        public override Color CheckPressedBackground => Color.Transparent;
    }
}
