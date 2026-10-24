using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ProcessorAffinityManager
{
    // The UI is built in code, not in the Visual Studio designer.
    [DesignerCategory("")]
    internal class MainForm : Form
    {
        private class ProcessEntry
        {
            public string Name;
            public int Id;
            public long Mask; // -1 = no access
        }

        private readonly Config config;
        private readonly int cpuCount = Math.Min(Environment.ProcessorCount, 64);
        private bool allowShow;
        private bool exiting;
        private bool trayHintShown;
        private List<ProcessEntry> processes = new List<ProcessEntry>();

        private readonly TextBox filterBox = new TextBox();
        private readonly ListView processList = new ListView();
        private readonly FlowLayoutPanel cpuPanel = new FlowLayoutPanel();
        private readonly List<ThemedCheckBox> cpuBoxes = new List<ThemedCheckBox>();
        private readonly ListView ruleList = new ListView();
        private readonly StatusStrip statusStrip = new StatusStrip();
        private readonly ToolStripStatusLabel statusLabel = new ToolStripStatusLabel();
        private readonly NotifyIcon trayIcon = new NotifyIcon();
        private readonly Timer ruleTimer = new Timer { Interval = 10000 };

        // Everything that shows text, so the language can be switched without a restart
        private readonly ToolStripMenuItem fileMenu = new ToolStripMenuItem();
        private readonly ToolStripMenuItem exitItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem settingsMenu = new ToolStripMenuItem();
        private readonly ToolStripMenuItem autostartItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem trayItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem languageMenu = new ToolStripMenuItem();
        private readonly CardPanel processGroup = new CardPanel();
        private readonly CardPanel cpuGroup = new CardPanel();
        private readonly CardPanel ruleGroup = new CardPanel();
        private readonly Button refreshButton = new Button();
        private readonly Button allButton = new Button();
        private readonly Button noneButton = new Button();
        private readonly Button applyOnceButton = new Button();
        private readonly Button saveRuleButton = new Button { Tag = "primary" };
        private readonly Button removeRuleButton = new Button();
        private readonly ToolStripMenuItem trayOpenItem = new ToolStripMenuItem();
        private readonly ToolStripMenuItem trayExitItem = new ToolStripMenuItem();

        public MainForm(Config config, bool startHidden)
        {
            this.config = config;
            allowShow = !startHidden;

            Theme.Refresh();
            Text = "Processor Affinity Manager";
            Icon = LoadIcon("app.ico", new Size(32, 32));
            Font = Theme.UpdateFonts(Lang.Current, 10f);
            Size = new Size(980, 640);
            MinimumSize = new Size(800, 500);
            StartPosition = FormStartPosition.CenterScreen;

            var menu = BuildMenu();
            var content = BuildContent();
            statusStrip.SizingGrip = false;
            statusStrip.RenderMode = ToolStripRenderMode.ManagerRenderMode;
            statusStrip.Items.Add(statusLabel);

            Controls.Add(content);
            Controls.Add(statusStrip);
            Controls.Add(menu);
            MainMenuStrip = menu;

            BuildTray();
            ApplyLanguage();
            Theme.Apply(this);
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            // Check the rules every 10 seconds: if a program (e.g. audiodg) restarts,
            // its affinity is set again right away.
            ruleTimer.Tick += (s, e) => ApplyRules();
            ruleTimer.Start();

            RefreshRules();
            ApplyRules();
            RefreshProcesses();

            if (!IsHandleCreated)
                CreateHandle();
        }

        // ---------------------------------------------------------------- Layout

        private MenuStrip BuildMenu()
        {
            var menu = new MenuStrip { Padding = new Padding(6, 4, 0, 4) };

            exitItem.Click += (s, e) => ExitApp();
            fileMenu.DropDownItems.Add(exitItem);

            autostartItem.Checked = Autostart.IsEnabled();
            autostartItem.Click += (s, e) => ToggleAutostart();
            trayItem.Checked = config.StartInTray;
            trayItem.CheckOnClick = true;
            trayItem.CheckedChanged += (s, e) =>
            {
                config.StartInTray = trayItem.Checked;
                config.Save();
            };

            // The submenu opens when hovering with the mouse
            foreach (var language in Lang.Available)
            {
                var code = language.Code;
                var item = new ToolStripMenuItem(language.Name) { Tag = code };
                item.Click += (s, e) => ChangeLanguage(code);
                languageMenu.DropDownItems.Add(item);
            }

            settingsMenu.DropDownItems.Add(autostartItem);
            settingsMenu.DropDownItems.Add(trayItem);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
            settingsMenu.DropDownItems.Add(languageMenu);

            menu.Items.Add(fileMenu);
            menu.Items.Add(settingsMenu);
            return menu;
        }

        private Control BuildContent()
        {
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(10, 4, 10, 6) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 50));

            // Left: running programs
            processGroup.Dock = DockStyle.Fill;
            processGroup.Margin = new Padding(6);
            SetupListView(processList, 220, 70, 160);
            processList.SelectedIndexChanged += (s, e) =>
            {
                var entry = SelectedProcess;
                if (entry != null && entry.Mask > 0)
                    SetCheckedMask(entry.Mask);
            };

            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(0, 0, 0, 10) };
            refreshButton.Dock = DockStyle.Right;
            refreshButton.Width = 120;
            refreshButton.Click += (s, e) => RefreshProcesses();
            filterBox.Dock = DockStyle.Fill;
            filterBox.BorderStyle = BorderStyle.FixedSingle;
            filterBox.TextChanged += (s, e) => FillProcessList(SelectedProcess?.Id);
            filterBox.HandleCreated += (s, e) => UpdateCueBanner();
            searchPanel.Controls.Add(filterBox);
            searchPanel.Controls.Add(new Panel { Dock = DockStyle.Right, Width = 8 });
            searchPanel.Controls.Add(refreshButton);

            processGroup.Controls.Add(processList);
            processGroup.Controls.Add(searchPanel);
            table.Controls.Add(processGroup, 0, 0);
            table.SetRowSpan(processGroup, 2);

            // Top right: select CPUs
            cpuGroup.Dock = DockStyle.Fill;
            cpuGroup.Margin = new Padding(6);
            cpuPanel.Dock = DockStyle.Fill;
            cpuPanel.AutoScroll = true;
            for (int i = 0; i < cpuCount; i++)
            {
                var box = new ThemedCheckBox { Text = "CPU " + i, AutoSize = false, Size = new Size(84, 28) };
                cpuBoxes.Add(box);
                cpuPanel.Controls.Add(box);
            }

            var cpuButtons = ButtonRow(
                (allButton, () => SetCheckedMask(AllCpusMask)),
                (noneButton, () => SetCheckedMask(0)),
                (applyOnceButton, ApplyToSelected),
                (saveRuleButton, SaveRule));

            cpuGroup.Controls.Add(cpuPanel);
            cpuGroup.Controls.Add(cpuButtons);
            table.Controls.Add(cpuGroup, 1, 0);

            // Bottom right: saved rules
            ruleGroup.Dock = DockStyle.Fill;
            ruleGroup.Margin = new Padding(6);
            SetupListView(ruleList, 170, 180);
            ruleList.SelectedIndexChanged += (s, e) =>
            {
                if (ruleList.SelectedItems.Count > 0)
                    SetCheckedMask(((AffinityRule)ruleList.SelectedItems[0].Tag).Mask);
            };

            var ruleButtons = ButtonRow((removeRuleButton, RemoveRule));

            ruleGroup.Controls.Add(ruleList);
            ruleGroup.Controls.Add(ruleButtons);
            table.Controls.Add(ruleGroup, 1, 1);

            return table;
        }

        // Lists are owner-drawn so that the header and selection use the theme colors.
        private static void SetupListView(ListView list, params int[] columnWidths)
        {
            list.Dock = DockStyle.Fill;
            list.View = View.Details;
            list.FullRowSelect = true;
            list.MultiSelect = false;
            list.HideSelection = false;
            list.BorderStyle = BorderStyle.None;
            list.OwnerDraw = true;
            list.SmallImageList = new ImageList { ImageSize = new Size(1, 26) }; // row height
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(list, true, null);
            foreach (int width in columnWidths)
                list.Columns.Add("", width);

            list.DrawColumnHeader += (s, e) =>
            {
                var p = Theme.Current;
                using (var fill = new SolidBrush(p.Surface))
                    e.Graphics.FillRectangle(fill, e.Bounds);
                using (var pen = new Pen(p.Border))
                    e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, list.Font, r, p.SubText,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
            };
            list.DrawItem += (s, e) => { };
            list.DrawSubItem += (s, e) =>
            {
                var p = Theme.Current;
                using (var fill = new SolidBrush(e.Item.Selected ? p.Selection : p.Surface))
                    e.Graphics.FillRectangle(fill, e.Bounds);
                var r = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, list.Font, r, e.Item.ForeColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            };

            // the last column fills the width, otherwise a light strip remains on the right of the header
            list.Resize += (s, e) => FitLastColumn(list);
            list.HandleCreated += (s, e) =>
            {
                Theme.ApplyScrollbars(list);
                FitLastColumn(list);
            };
        }

        private static void FitLastColumn(ListView list)
        {
            if (list.Columns.Count > 0)
                list.Columns[list.Columns.Count - 1].Width = -2;
        }

        private static FlowLayoutPanel ButtonRow(params (Button Button, Action OnClick)[] buttons)
        {
            var row = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
            foreach (var b in buttons)
            {
                var onClick = b.OnClick;
                b.Button.AutoSize = true;
                b.Button.Padding = new Padding(8, 2, 8, 2);
                b.Button.Click += (s, e) => onClick();
                row.Controls.Add(b.Button);
            }
            return row;
        }

        private void BuildTray()
        {
            trayOpenItem.Click += (s, e) => ShowFromTray();
            trayExitItem.Click += (s, e) => ExitApp();

            var menu = new ContextMenuStrip();
            menu.Items.Add(trayOpenItem);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(trayExitItem);

            UpdateTrayIcon();
            trayIcon.Text = "Processor Affinity Manager";
            trayIcon.ContextMenuStrip = menu;
            trayIcon.DoubleClick += (s, e) => ShowFromTray();
            trayIcon.Visible = true;
        }

        // ---------------------------------------------------------------- Theme / icons

        private static Icon LoadIcon(string name, Size size)
        {
            using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream(name))
                return new Icon(stream, size);
        }

        // Black icon on a light taskbar, white on a dark one – otherwise it would not be visible.
        private void UpdateTrayIcon()
        {
            bool lightTaskbar = false;
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                lightTaskbar = key?.GetValue("SystemUsesLightTheme") is int value && value == 1;

            var old = trayIcon.Icon;
            trayIcon.Icon = LoadIcon(lightTaskbar ? "app.ico" : "tray-white.ico", SystemInformation.SmallIconSize);
            old?.Dispose();
        }

        // Windows was switched between light and dark → apply immediately
        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.General && IsHandleCreated)
                BeginInvoke(new Action(ApplyTheme));
        }

        private void ApplyTheme()
        {
            UpdateTrayIcon();
            if (!Theme.Refresh())
                return;

            Theme.Apply(this);
            FillProcessList(SelectedProcess?.Id);
            Invalidate(true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this);
        }

        // ---------------------------------------------------------------- Language

        private void ChangeLanguage(string code)
        {
            if (code == Lang.Current)
                return;

            Lang.Set(code);
            config.Language = code;
            config.Save();
            Font = Theme.UpdateFonts(code, 10f);
            ApplyLanguage();
            Invalidate(true);
        }

        /// <summary>Sets all texts in the current language – takes effect immediately, without a restart.</summary>
        private void ApplyLanguage()
        {
            fileMenu.Text = Lang.T("Menu.File");
            exitItem.Text = Lang.T("Menu.Exit");
            settingsMenu.Text = Lang.T("Menu.Settings");
            autostartItem.Text = Lang.T("Menu.Autostart");
            trayItem.Text = Lang.T("Menu.StartInTray");
            languageMenu.Text = Lang.T("Menu.Language");
            foreach (ToolStripMenuItem item in languageMenu.DropDownItems)
                item.Checked = (string)item.Tag == Lang.Current;

            processGroup.Text = Lang.T("Group.Processes");
            cpuGroup.Text = Lang.T("Group.Affinity");
            ruleGroup.Text = Lang.T("Group.Rules");

            processList.Columns[0].Text = Lang.T("Col.Program");
            processList.Columns[1].Text = "PID";
            processList.Columns[2].Text = Lang.T("Col.Affinity");
            ruleList.Columns[0].Text = Lang.T("Col.Program");
            ruleList.Columns[1].Text = Lang.T("Col.Cpus");

            refreshButton.Text = Lang.T("Btn.Refresh");
            allButton.Text = Lang.T("Btn.All");
            noneButton.Text = Lang.T("Btn.None");
            applyOnceButton.Text = Lang.T("Btn.ApplyOnce");
            saveRuleButton.Text = Lang.T("Btn.SaveRule");
            removeRuleButton.Text = Lang.T("Btn.RemoveRule");

            trayOpenItem.Text = Lang.T("Tray.Open");
            trayExitItem.Text = Lang.T("Menu.Exit");

            UpdateCueBanner();
            statusLabel.Text = "";

            // Refill the lists so that "no access" / "All CPUs" are translated
            FillProcessList(SelectedProcess?.Id);
            RefreshRules();
        }

        private void UpdateCueBanner()
        {
            const int EM_SETCUEBANNER = 0x1501;
            if (filterBox.IsHandleCreated)
                SendMessage(filterBox.Handle, EM_SETCUEBANNER, (IntPtr)1, Lang.T("Search"));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        // ---------------------------------------------------------------- Processes

        private ProcessEntry SelectedProcess =>
            processList.SelectedItems.Count > 0 ? (ProcessEntry)processList.SelectedItems[0].Tag : null;

        private void RefreshProcesses()
        {
            int? selectedId = SelectedProcess?.Id;
            var list = new List<ProcessEntry>();

            foreach (var p in Process.GetProcesses())
            {
                using (p)
                {
                    if (p.Id == 0 || p.Id == 4) // Idle process / System
                        continue;

                    long mask = -1;
                    try { mask = (long)p.ProcessorAffinity; }
                    catch { /* protected process or already exited */ }

                    list.Add(new ProcessEntry { Name = p.ProcessName, Id = p.Id, Mask = mask });
                }
            }

            processes = list
                .OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => e.Id)
                .ToList();
            FillProcessList(selectedId);
        }

        private void FillProcessList(int? selectId)
        {
            string filter = filterBox.Text.Trim();

            processList.BeginUpdate();
            processList.Items.Clear();
            foreach (var entry in processes)
            {
                if (filter.Length > 0 && entry.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var item = new ListViewItem(new[]
                {
                    entry.Name,
                    entry.Id.ToString(),
                    entry.Mask < 0 ? Lang.T("NoAccess") : FormatMask(entry.Mask),
                }) { Tag = entry };

                if (entry.Mask < 0)
                    item.ForeColor = Theme.Current.SubText;

                processList.Items.Add(item);
                if (entry.Id == selectId)
                    item.Selected = true;
            }
            processList.EndUpdate();
        }

        private void ApplyToSelected()
        {
            var entry = SelectedProcess;
            if (entry == null)
            {
                SetStatus(Lang.T("Status.SelectProgram"));
                return;
            }

            long mask = GetCheckedMask();
            if (mask == 0)
            {
                SetStatus(Lang.T("Status.SelectCpu"));
                return;
            }

            try
            {
                using (var p = Process.GetProcessById(entry.Id))
                    p.ProcessorAffinity = (IntPtr)mask;
                SetStatus(Lang.T("Status.Applied", entry.Name, entry.Id, FormatMask(mask)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("Error.Apply") + "\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshProcesses();
        }

        // ---------------------------------------------------------------- Rules

        private void SaveRule()
        {
            var entry = SelectedProcess;
            if (entry == null)
            {
                SetStatus(Lang.T("Status.SelectProgram"));
                return;
            }

            long mask = GetCheckedMask();
            if (mask == 0)
            {
                SetStatus(Lang.T("Status.SelectCpu"));
                return;
            }

            config.Rules.RemoveAll(r => string.Equals(r.ProcessName, entry.Name, StringComparison.OrdinalIgnoreCase));
            config.Rules.Add(new AffinityRule { ProcessName = entry.Name, Mask = mask });
            config.Save();

            RefreshRules();
            ApplyRules();
            RefreshProcesses();
            SetStatus(Lang.T("Status.RuleSaved", entry.Name, FormatMask(mask)));
        }

        private void RemoveRule()
        {
            if (ruleList.SelectedItems.Count == 0)
            {
                SetStatus(Lang.T("Status.SelectRule"));
                return;
            }

            var rule = (AffinityRule)ruleList.SelectedItems[0].Tag;
            config.Rules.Remove(rule);
            config.Save();
            RefreshRules();
            SetStatus(Lang.T("Status.RuleRemoved", rule.ProcessName));
        }

        private void RefreshRules()
        {
            ruleList.BeginUpdate();
            ruleList.Items.Clear();
            foreach (var rule in config.Rules.OrderBy(r => r.ProcessName, StringComparer.OrdinalIgnoreCase))
                ruleList.Items.Add(new ListViewItem(new[] { rule.ProcessName, FormatMask(rule.Mask) }) { Tag = rule });
            ruleList.EndUpdate();
        }

        /// <summary>Sets the affinity for all rules, but only if it is not already correct.</summary>
        private void ApplyRules()
        {
            foreach (var rule in config.Rules)
            {
                foreach (var p in Process.GetProcessesByName(rule.ProcessName))
                {
                    using (p)
                    {
                        try
                        {
                            if ((long)p.ProcessorAffinity == rule.Mask)
                                continue;

                            p.ProcessorAffinity = (IntPtr)rule.Mask;
                            SetStatus(Lang.T("Status.RuleApplied", DateTime.Now.ToString("HH:mm:ss"),
                                rule.ProcessName, p.Id, FormatMask(rule.Mask)));
                        }
                        catch
                        {
                            // no access, or the process has exited in the meantime
                        }
                    }
                }
            }
        }

        // ---------------------------------------------------------------- CPU mask

        private long AllCpusMask => cpuCount >= 64 ? -1L : (1L << cpuCount) - 1;

        private long GetCheckedMask()
        {
            long mask = 0;
            for (int i = 0; i < cpuBoxes.Count; i++)
                if (cpuBoxes[i].Checked)
                    mask |= 1L << i;
            return mask;
        }

        private void SetCheckedMask(long mask)
        {
            for (int i = 0; i < cpuBoxes.Count; i++)
                cpuBoxes[i].Checked = (mask & (1L << i)) != 0;
        }

        private string FormatMask(long mask)
        {
            if ((mask & AllCpusMask) == AllCpusMask)
                return Lang.T("AllCpus");

            var cpus = Enumerable.Range(0, 64).Where(i => (mask & (1L << i)) != 0);
            return "CPU " + string.Join(", ", cpus);
        }

        // ---------------------------------------------------------------- Window / tray

        private void SetStatus(string text) => statusLabel.Text = text;

        private void ToggleAutostart()
        {
            try
            {
                if (autostartItem.Checked)
                    Autostart.Disable();
                else
                    Autostart.Enable();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Lang.T("Error.Autostart") + "\n" + ex.Message,
                    Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            autostartItem.Checked = Autostart.IsEnabled();
        }

        public void ShowFromTray()
        {
            allowShow = true;
            Show();
            if (WindowState == FormWindowState.Minimized)
                WindowState = FormWindowState.Normal;
            Activate();
            RefreshProcesses();
        }

        // Prevents the window from briefly flashing when starting in the tray.
        protected override void SetVisibleCore(bool value)
        {
            if (!allowShow)
            {
                value = false;
                if (!IsHandleCreated)
                    CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        // Closing (X) only hides the window, so the rules keep being applied.
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                if (!trayHintShown)
                {
                    trayIcon.ShowBalloonTip(3000, "Processor Affinity Manager", Lang.T("Tray.Hint"), ToolTipIcon.Info);
                    trayHintShown = true;
                }
                return;
            }

            trayIcon.Visible = false;
            base.OnFormClosing(e);
        }

        private void ExitApp()
        {
            exiting = true;
            trayIcon.Visible = false;
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
                ruleTimer.Dispose();
                trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
