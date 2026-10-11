using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace CustomNotepadLite
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string initialFile = null;
            if (args != null && args.Length > 0 && File.Exists(args[0]))
            {
                initialFile = args[0];
            }

            Application.Run(new MainForm(initialFile));
        }
    }

    public class MainForm : Form
    {
        private RichTextBox textBox;
        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusPos;
        private ToolStripStatusLabel statusChars;
        private ToolStripStatusLabel statusEstimatedSize;
        private ToolStripStatusLabel statusFormat;

        // State
        private string currentFilePath = null;
        private bool isModified = false;
        private bool isDarkMode = true;

        // Bare-bones fixed font: 12px Segoe UI
        private readonly Font defaultFixedFont = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Pixel);

        // Lite version: Exclusively .zip format
        public const string CustomFilter = "Stealth ZIP File (*.zip)|*.zip|All Files (*.*)|*.*";

        public MainForm(string initialFile)
        {
            InitializeComponents();
            ApplyTheme(isDarkMode);

            if (!string.IsNullOrEmpty(initialFile))
            {
                OpenFile(initialFile);
            }
            else
            {
                UpdateTitle();
            }
        }

        private void InitializeComponents()
        {
            this.Size = new Size(860, 560);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.MinimumSize = new Size(340, 220);
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
            this.FormClosing += MainForm_FormClosing;

            // Menu Bar
            menuStrip = new MenuStrip();
            menuStrip.RenderMode = ToolStripRenderMode.Professional;
            CreateMenu();
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // Status Bar
            statusStrip = new StatusStrip();
            statusPos = new ToolStripStatusLabel("Ln 1, Col 1");
            statusChars = new ToolStripStatusLabel("0 words | 0 chars");
            statusChars.Spring = true;
            statusChars.TextAlign = ContentAlignment.MiddleLeft;
            statusEstimatedSize = new ToolStripStatusLabel("Est. Size: ~0 B");
            statusFormat = new ToolStripStatusLabel("Format: ZIP (2 numbers/letter)");

            statusStrip.Items.AddRange(new ToolStripItem[] {
                statusPos,
                new ToolStripSeparator(),
                statusChars,
                new ToolStripSeparator(),
                statusEstimatedSize,
                new ToolStripSeparator(),
                statusFormat
            });
            this.Controls.Add(statusStrip);

            // Main Editor Box
            textBox = new RichTextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.BorderStyle = BorderStyle.None;
            textBox.AcceptsTab = true;
            textBox.DetectUrls = false;
            textBox.HideSelection = false;
            textBox.ScrollBars = RichTextBoxScrollBars.Both;
            textBox.Font = defaultFixedFont;
            textBox.WordWrap = true;
            textBox.ZoomFactor = 1.0f;

            textBox.TextChanged += (s, e) => {
                if (!isModified)
                {
                    isModified = true;
                    UpdateTitle();
                }
                UpdateStatus();
            };

            textBox.SelectionChanged += (s, e) => UpdateStatus();

            // Lock zoom shortcuts
            textBox.KeyDown += (s, e) => {
                if (e.Control && (e.KeyCode == Keys.Oemplus || e.KeyCode == Keys.OemMinus || e.KeyCode == Keys.D0 || e.KeyCode == Keys.NumPad0))
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            this.Controls.Add(textBox);
            textBox.BringToFront();
        }

        private void CreateMenu()
        {
            // === FILE MENU ===
            var fileMenu = new ToolStripMenuItem("&File");
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&New", null, (s, e) => ActionNew(), Keys.Control | Keys.N));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Open / Import (.zip)...", null, (s, e) => ActionOpen(), Keys.Control | Keys.O));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("&Save", null, (s, e) => ActionSave(), Keys.Control | Keys.S));
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("Save &As / Export (.zip)...", null, (s, e) => ActionSaveAs(), Keys.Control | Keys.Shift | Keys.S));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(new ToolStripMenuItem("E&xit", null, (s, e) => this.Close(), Keys.Alt | Keys.F4));

            // === EDIT MENU ===
            var editMenu = new ToolStripMenuItem("&Edit");
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Undo", null, (s, e) => { if (textBox.CanUndo) textBox.Undo(); }, Keys.Control | Keys.Z));
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Redo", null, (s, e) => { if (textBox.CanRedo) textBox.Redo(); }, Keys.Control | Keys.Y));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Cu&t", null, (s, e) => textBox.Cut(), Keys.Control | Keys.X));
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Copy", null, (s, e) => textBox.Copy(), Keys.Control | Keys.C));
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Paste", null, (s, e) => textBox.Paste(), Keys.Control | Keys.V));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Select &All", null, (s, e) => textBox.SelectAll(), Keys.Control | Keys.A));
            editMenu.DropDownItems.Add(new ToolStripMenuItem("&Find / Replace...", null, (s, e) => ShowFindReplaceDialog(), Keys.Control | Keys.F));
            editMenu.DropDownItems.Add(new ToolStripSeparator());
            editMenu.DropDownItems.Add(new ToolStripMenuItem("Insert Date / Time", null, (s, e) => { textBox.SelectedText = DateTime.Now.ToString("g"); }, Keys.F5));

            // === VIEW MENU ===
            var viewMenu = new ToolStripMenuItem("&View");
            var wrapItem = new ToolStripMenuItem("&Word Wrap", null, (s, e) => {
                var item = (ToolStripMenuItem)s;
                item.Checked = !item.Checked;
                textBox.WordWrap = item.Checked;
            }, Keys.Control | Keys.W);
            wrapItem.Checked = true;
            viewMenu.DropDownItems.Add(wrapItem);

            viewMenu.DropDownItems.Add(new ToolStripMenuItem("Toggle &Dark / Light Mode", null, (s, e) => {
                isDarkMode = !isDarkMode;
                ApplyTheme(isDarkMode);
            }, Keys.Control | Keys.T));

            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(new ToolStripMenuItem("View &Encrypted Payload (2 Numbers/Letter)...", null, (s, e) => ShowEncryptedPayload()));

            // === HELP MENU ===
            var helpMenu = new ToolStripMenuItem("&Help");
            helpMenu.DropDownItems.Add(new ToolStripMenuItem("&About", null, (s, e) => ShowAboutDialog()));

            menuStrip.Items.AddRange(new ToolStripItem[] { fileMenu, editMenu, viewMenu, helpMenu });
        }

        private void ApplyTheme(bool dark)
        {
            if (dark)
            {
                Color bgMain = Color.FromArgb(30, 30, 30);
                Color fgMain = Color.FromArgb(220, 220, 220);
                Color bgMenu = Color.FromArgb(45, 45, 48);

                this.BackColor = bgMain;
                textBox.BackColor = bgMain;
                textBox.ForeColor = fgMain;

                menuStrip.BackColor = bgMenu;
                menuStrip.ForeColor = fgMain;
                menuStrip.Renderer = new DarkMenuRenderer();

                statusStrip.BackColor = bgMenu;
                statusStrip.ForeColor = fgMain;
                statusStrip.Renderer = new DarkMenuRenderer();
            }
            else
            {
                Color bgMain = Color.White;
                Color fgMain = Color.FromArgb(20, 20, 20);
                Color bgMenu = Color.FromArgb(240, 240, 240);

                this.BackColor = bgMain;
                textBox.BackColor = bgMain;
                textBox.ForeColor = fgMain;

                menuStrip.BackColor = bgMenu;
                menuStrip.ForeColor = fgMain;
                menuStrip.Renderer = new ToolStripProfessionalRenderer();

                statusStrip.BackColor = bgMenu;
                statusStrip.ForeColor = fgMain;
                statusStrip.Renderer = new ToolStripProfessionalRenderer();
            }
        }

        private void UpdateTitle()
        {
            string fileName = string.IsNullOrEmpty(currentFilePath) ? "Untitled" : Path.GetFileName(currentFilePath);
            string mod = isModified ? "*" : "";
            this.Text = string.Format("{0}{1} - Custom Notepad Lite", mod, fileName);
            statusFormat.Text = "Format: ZIP (2 numbers/letter)";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return string.Format("{0:F1} KB", bytes / 1024.0);
            return string.Format("{0:F2} MB", bytes / (1024.0 * 1024.0));
        }

        private void UpdateStatus()
        {
            int index = textBox.SelectionStart;
            int line = textBox.GetLineFromCharIndex(index) + 1;
            int col = index - textBox.GetFirstCharIndexOfCurrentLine() + 1;
            statusPos.Text = string.Format("Ln {0}, Col {1}", line, col);

            string text = textBox.Text;
            int charCount = text.Length;

            int wordCount = 0;
            bool inWord = false;
            int effectiveChars = 0;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\r') effectiveChars++;

                if (char.IsWhiteSpace(text[i]))
                {
                    inWord = false;
                }
                else if (!inWord)
                {
                    inWord = true;
                    wordCount++;
                }
            }

            statusChars.Text = string.Format("{0:N0} words | {1:N0} chars", wordCount, charCount);

            // Lite estimated file size: 2 numbers per character
            long zipBytes = effectiveChars * 2L;
            statusEstimatedSize.Text = string.Format("Est. Size: ~{0}", FormatBytes(zipBytes));
        }

        // =========================================================================
        // CODEC: ZIP (2 numbers per letter, 00-99 mapping)
        // =========================================================================

        public static string EncodeZip(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') continue;

                int code;
                if (c == '\n') code = 0;
                else if (c == '\t') code = 1;
                else if (c >= 32 && c <= 126) code = (c - 32) + 2;
                else code = 2;

                sb.Append(code.ToString("D2"));
            }
            return sb.ToString();
        }

        public static bool TryDecodeZip(string numbers, out string result)
        {
            result = null;
            if (string.IsNullOrEmpty(numbers))
            {
                result = string.Empty;
                return true;
            }

            string clean = numbers.Trim();
            if (clean.Length % 2 != 0) return false;

            for (int i = 0; i < clean.Length; i++)
            {
                if (!char.IsDigit(clean[i])) return false;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < clean.Length; i += 2)
            {
                int code = int.Parse(clean.Substring(i, 2));
                if (code == 0) sb.Append('\n');
                else if (code == 1) sb.Append('\t');
                else if (code >= 2 && code <= 96) sb.Append((char)(code - 2 + 32));
                else sb.Append(' ');
            }
            result = sb.ToString();
            return true;
        }

        // =========================================================================
        // FILE ACTIONS
        // =========================================================================

        private bool PromptSaveChanges()
        {
            if (!isModified) return true;

            string name = string.IsNullOrEmpty(currentFilePath) ? "Untitled" : Path.GetFileName(currentFilePath);
            DialogResult res = MessageBox.Show(
                string.Format("Do you want to save changes to {0}?", name),
                "Custom Notepad Lite",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (res == DialogResult.Yes)
            {
                return ActionSave();
            }
            else if (res == DialogResult.No)
            {
                return true;
            }
            return false;
        }

        private void ActionNew()
        {
            if (!PromptSaveChanges()) return;

            textBox.Clear();
            currentFilePath = null;
            isModified = false;
            UpdateTitle();
            UpdateStatus();
        }

        private void ActionOpen()
        {
            if (!PromptSaveChanges()) return;

            using (var dlg = new OpenFileDialog())
            {
                dlg.Filter = CustomFilter;
                dlg.DefaultExt = "zip";
                dlg.Title = "Open Stealth Note (.zip)";
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    OpenFile(dlg.FileName);
                }
            }
        }

        public void OpenFile(string path)
        {
            try
            {
                string raw = File.ReadAllText(path, Encoding.UTF8);
                string decrypted;

                if (TryDecodeZip(raw, out decrypted))
                {
                    textBox.Text = decrypted;
                }
                else
                {
                    textBox.Text = raw;
                }

                currentFilePath = path;
                isModified = false;
                UpdateTitle();
                UpdateStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool ActionSave()
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                return ActionSaveAs();
            }
            return SaveZip(currentFilePath);
        }

        private bool ActionSaveAs()
        {
            using (var dlg = new SaveFileDialog())
            {
                dlg.Filter = CustomFilter;
                dlg.DefaultExt = "zip";
                dlg.Title = "Save As / Export (.zip)";
                dlg.FileName = string.IsNullOrEmpty(currentFilePath) ? "Document.zip" : Path.GetFileName(currentFilePath);

                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    currentFilePath = dlg.FileName;
                    return SaveZip(currentFilePath);
                }
            }
            return false;
        }

        private bool SaveZip(string path)
        {
            try
            {
                string encoded = EncodeZip(textBox.Text);
                File.WriteAllText(path, encoded, Encoding.UTF8);
                currentFilePath = path;
                isModified = false;
                UpdateTitle();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving .zip file:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private void ShowEncryptedPayload()
        {
            string text = textBox.Text;
            string zipPayload = EncodeZip(text);

            using (var dlg = new Form())
            {
                dlg.Text = "Encrypted Payload Inspector (ZIP 2-Numbers/Letter)";
                dlg.Size = new Size(680, 480);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.Font = defaultFixedFont;

                var txt = new TextBox() {
                    Multiline = true,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    ScrollBars = ScrollBars.Both,
                    Font = defaultFixedFont,
                    BackColor = isDarkMode ? Color.FromArgb(30, 30, 30) : Color.White,
                    ForeColor = isDarkMode ? Color.FromArgb(220, 220, 220) : Color.Black,
                    Text = "=== STEALTH .ZIP FORMAT (2 numbers per letter, 00-99 mapping) ===\r\n" +
                           "Total numbers: " + zipPayload.Length + " (" + (zipPayload.Length / 2) + " letters)\r\n\r\n" + 
                           zipPayload
                };

                dlg.Controls.Add(txt);
                dlg.ShowDialog(this);
            }
        }

        private void ShowFindReplaceDialog()
        {
            var dlg = new Form();
            dlg.Text = "Find and Replace";
            dlg.Size = new Size(440, 230);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.FormBorderStyle = FormBorderStyle.FixedDialog;
            dlg.MaximizeBox = false;
            dlg.MinimizeBox = false;

            var lblFind = new Label() { Text = "Find what:", Location = new Point(15, 18), AutoSize = true };
            var txtFind = new TextBox() { Location = new Point(110, 15), Width = 200, Text = textBox.SelectedText };
            var lblReplace = new Label() { Text = "Replace with:", Location = new Point(15, 53), AutoSize = true };
            var txtReplace = new TextBox() { Location = new Point(110, 50), Width = 200 };

            var chkMatchCase = new CheckBox() { Text = "Match case", Location = new Point(110, 85), AutoSize = true };

            var btnFindNext = new Button() { Text = "Find Next", Location = new Point(325, 14), Width = 90 };
            var btnReplace = new Button() { Text = "Replace", Location = new Point(325, 48), Width = 90 };
            var btnReplaceAll = new Button() { Text = "Replace All", Location = new Point(325, 82), Width = 90 };
            var btnClose = new Button() { Text = "Close", Location = new Point(325, 116), Width = 90 };

            btnFindNext.Click += (s, e) => {
                FindText(txtFind.Text, chkMatchCase.Checked);
            };

            btnReplace.Click += (s, e) => {
                if (textBox.SelectedText.Equals(txtFind.Text, chkMatchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
                {
                    textBox.SelectedText = txtReplace.Text;
                }
                FindText(txtFind.Text, chkMatchCase.Checked);
            };

            btnReplaceAll.Click += (s, e) => {
                if (string.IsNullOrEmpty(txtFind.Text)) return;
                int count = 0;
                int start = 0;
                string query = txtFind.Text;
                string replacement = txtReplace.Text;
                StringComparison comp = chkMatchCase.Checked ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

                string full = textBox.Text;
                var sb = new StringBuilder();
                int last = 0;
                while (true)
                {
                    int found = full.IndexOf(query, start, comp);
                    if (found == -1) break;
                    sb.Append(full.Substring(last, found - last));
                    sb.Append(replacement);
                    last = found + query.Length;
                    start = last;
                    count++;
                }
                sb.Append(full.Substring(last));
                textBox.Text = sb.ToString();
                MessageBox.Show(string.Format("Replaced {0} occurrence(s).", count), "Replace All", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            btnClose.Click += (s, e) => dlg.Close();

            dlg.Controls.AddRange(new Control[] {
                lblFind, txtFind, lblReplace, txtReplace, chkMatchCase, btnFindNext, btnReplace, btnReplaceAll, btnClose
            });
            dlg.AcceptButton = btnFindNext;
            dlg.CancelButton = btnClose;
            dlg.Show(this);
        }

        private bool FindText(string query, bool matchCase)
        {
            if (string.IsNullOrEmpty(query)) return false;

            int start = textBox.SelectionStart + textBox.SelectionLength;
            RichTextBoxFinds options = RichTextBoxFinds.None;
            if (matchCase) options |= RichTextBoxFinds.MatchCase;

            int found = textBox.Find(query, start, options);
            if (found == -1 && start > 0)
            {
                found = textBox.Find(query, 0, options);
            }

            if (found == -1)
            {
                MessageBox.Show(string.Format("Cannot find \"{0}\"", query), "Find", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }
            return true;
        }

        private void ShowAboutDialog()
        {
            MessageBox.Show(
                "Custom Notepad Lite\n\n" +
                "• Ultra-Lightweight & Clean\n" +
                "• Stealth .ZIP Format Only (2 numbers per letter)\n" +
                "• Standard clean Windows executable icon\n" +
                "• Live word, character & estimated file size counter\n" +
                "• Bare-bones 12px Segoe UI font\n" +
                "• Instant Dark / Light mode toggle (Ctrl+T)\n\n" +
                "Created for Nicholas",
                "About Custom Notepad Lite",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0 && File.Exists(files[0]))
            {
                if (PromptSaveChanges())
                {
                    OpenFile(files[0]);
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!PromptSaveChanges())
            {
                e.Cancel = true;
            }
        }
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkColorTable()) { }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = Color.FromArgb(220, 220, 220);
            base.OnRenderItemText(e);
        }

        private class DarkColorTable : ProfessionalColorTable
        {
            public override Color MenuStripGradientBegin { get { return Color.FromArgb(45, 45, 48); } }
            public override Color MenuStripGradientEnd { get { return Color.FromArgb(45, 45, 48); } }
            public override Color MenuItemSelected { get { return Color.FromArgb(62, 62, 66); } }
            public override Color MenuItemSelectedGradientBegin { get { return Color.FromArgb(62, 62, 66); } }
            public override Color MenuItemSelectedGradientEnd { get { return Color.FromArgb(62, 62, 66); } }
            public override Color MenuItemBorder { get { return Color.FromArgb(0, 122, 204); } }
            public override Color MenuBorder { get { return Color.FromArgb(63, 63, 70); } }
            public override Color ToolStripDropDownBackground { get { return Color.FromArgb(37, 37, 38); } }
            public override Color ImageMarginGradientBegin { get { return Color.FromArgb(37, 37, 38); } }
            public override Color ImageMarginGradientMiddle { get { return Color.FromArgb(37, 37, 38); } }
            public override Color ImageMarginGradientEnd { get { return Color.FromArgb(37, 37, 38); } }
            public override Color SeparatorDark { get { return Color.FromArgb(63, 63, 70); } }
            public override Color SeparatorLight { get { return Color.FromArgb(45, 45, 48); } }
            public override Color StatusStripGradientBegin { get { return Color.FromArgb(45, 45, 48); } }
            public override Color StatusStripGradientEnd { get { return Color.FromArgb(45, 45, 48); } }
        }
    }
}
