// FreeFlow for Windows - UI: iOS-style settings window, controls, recording pill.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FreeFlowWin
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(242, 242, 247);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(28, 28, 30);
        public static readonly Color Sub = Color.FromArgb(142, 142, 147);
        public static readonly Color Sep = Color.FromArgb(229, 229, 234);
        public static readonly Color Accent = Color.FromArgb(0, 122, 255);
        public static readonly Color Green = Color.FromArgb(52, 199, 89);
        public static readonly Color Red = Color.FromArgb(255, 59, 48);
        public static readonly Color Track = Color.FromArgb(233, 233, 235);

        static float s;
        public static float S // screen scale (1.0 at 100%, 1.5 at 150%)
        {
            get
            {
                if (s > 0) return s;
                try { using (var g = Graphics.FromHwnd(IntPtr.Zero)) s = g.DpiX / 96f; } catch { s = 1; }
                return s;
            }
        }

        static string family;
        static string Family
        {
            get
            {
                if (family != null) return family;
                family = "Segoe UI";
                foreach (var f in FontFamily.Families) if (f.Name == "Segoe UI Variable Text") { family = f.Name; break; }
                return family;
            }
        }
        public static Font F(float size) { return new Font(Family, size, FontStyle.Regular); }
        public static Font Icons(float size)
        {
            foreach (var n in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
                foreach (var f in FontFamily.Families) if (f.Name == n) return new Font(n, size);
            return F(size);
        }
        public static Font B(float size) { return new Font(Family, size, FontStyle.Bold); }
        public static Font Semi(float size)
        {
            try { var f = new Font(Family == "Segoe UI" ? "Segoe UI Semibold" : "Segoe UI Variable Text Semibold", size); if (f.Name.Contains("Semibold")) return f; } catch { }
            return B(size);
        }

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            var p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }
    }

    // white rounded group, like an iOS inset list
    class Card : Panel
    {
        public readonly List<int> Seps = new List<int>();
        public Card() { DoubleBuffered = true; BackColor = Theme.Bg; ResizeRedraw = true; }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 12 * Theme.S))
            using (var b = new SolidBrush(Theme.Card)) g.FillPath(b, p);
            g.SmoothingMode = SmoothingMode.None;
            using (var pen = new Pen(Theme.Sep)) foreach (int y in Seps) g.DrawLine(pen, 16 * Theme.S, y * Theme.S, Width, y * Theme.S);
        }
    }

    abstract class Smooth : Control
    {
        protected Smooth()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Theme.Card; Cursor = Cursors.Hand;
        }
    }

    // iOS switch
    class Toggle : Smooth
    {
        bool on; float t;
        readonly Timer anim = new Timer { Interval = 15 };
        public event EventHandler Changed;
        public Toggle(bool value)
        {
            on = value; t = value ? 1 : 0; Size = new Size(46, 28);
            anim.Tick += delegate
            {
                float target = on ? 1 : 0;
                t += (target - t) * 0.35f;
                if (Math.Abs(target - t) < 0.01f) { t = target; anim.Stop(); }
                Invalidate();
            };
        }
        public bool Checked { get { return on; } }
        protected override void OnClick(EventArgs e) { on = !on; anim.Start(); if (Changed != null) Changed(this, e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            float h = Height - 1;
            using (var p = Theme.Round(new RectangleF(0, 0, Width - 1, h), h / 2))
            using (var b = new SolidBrush(Theme.Mix(Theme.Track, Theme.Green, t))) g.FillPath(b, p);
            float d = h - 4, x = 2 + t * (Width - 1 - h);
            using (var sh = new SolidBrush(Color.FromArgb(30, 0, 0, 0))) g.FillEllipse(sh, x, 3, d, d);
            g.FillEllipse(Brushes.White, x, 2, d, d);
        }
    }

    // iOS segmented control
    class Segmented : Smooth
    {
        readonly string[] items; int sel; float pos;
        readonly Timer anim = new Timer { Interval = 15 };
        public event EventHandler Changed;
        public Segmented(string[] items, int selected)
        {
            this.items = items; sel = selected; pos = selected; Height = 34;
            anim.Tick += delegate { pos += (sel - pos) * 0.35f; if (Math.Abs(sel - pos) < 0.01f) { pos = sel; anim.Stop(); } Invalidate(); };
        }
        public int Selected { get { return sel; } set { sel = value; anim.Start(); } }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            int i = Math.Max(0, Math.Min(items.Length - 1, e.X * items.Length / Math.Max(1, Width)));
            if (i == sel) return;
            sel = i; anim.Start();
            if (Changed != null) Changed(this, EventArgs.Empty);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            using (var p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), 9))
            using (var b = new SolidBrush(Color.FromArgb(238, 238, 240))) g.FillPath(b, p);
            float w = (Width - 4f) / items.Length;
            var r = new RectangleF(2 + pos * w, 2, w, Height - 5);
            using (var p = Theme.Round(new RectangleF(r.X, r.Y + 1, r.Width, r.Height), 7))
            using (var b = new SolidBrush(Color.FromArgb(18, 0, 0, 0))) g.FillPath(b, p);
            using (var p = Theme.Round(r, 7)) g.FillPath(Brushes.White, p);
            using (var f = Theme.F(9.5f)) using (var fs = Theme.Semi(9.5f))
                for (int i = 0; i < items.Length; i++)
                {
                    var cell = new Rectangle((int)(2 + i * w), 0, (int)w, Height);
                    TextRenderer.DrawText(g, items[i], i == sel ? fs : f, cell, Theme.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
        }
    }

    // shows the talk key as keycaps; click to record a new one
    class KeyPill : Smooth
    {
        int[] keys = new int[0];
        public bool Capturing;
        public KeyPill() { Height = 30; }
        static int P(int px) { return (int)(px * Theme.S); }
        public void SetKeys(int[] k) { keys = k ?? new int[0]; Relayout(); }
        void Relayout()
        {
            int w = 0;
            using (var f = Theme.Semi(9f))
            {
                if (keys.Length == 0) w = TextRenderer.MeasureText("Press keys...", f).Width + P(24);
                else foreach (var k in keys) w += TextRenderer.MeasureText(KeyNames.Name(k), f).Width + P(18) + P(16);
                if (keys.Length > 0) w -= P(16);
            }
            Width = w + 2;
            if (Parent != null) Left = Parent.Width - P(16) - Width;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            var border = Capturing ? Theme.Accent : Color.FromArgb(214, 214, 219);
            var fg = Capturing ? Theme.Accent : Theme.Text;
            using (var f = Theme.Semi(9f))
            {
                if (keys.Length == 0)
                {
                    using (var p = Theme.Round(new RectangleF(0.5f, 0.5f, Width - 2, Height - 3), 7))
                    using (var pen = new Pen(Theme.Accent, 1.4f) { DashStyle = DashStyle.Dash }) g.DrawPath(pen, p);
                    TextRenderer.DrawText(g, "Press keys...", f, new Rectangle(0, 0, Width, Height - 2), Theme.Accent, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    return;
                }
                int x = 0;
                for (int i = 0; i < keys.Length; i++)
                {
                    string name = KeyNames.Name(keys[i]);
                    int w = TextRenderer.MeasureText(name, f).Width + P(18);
                    var r = new RectangleF(x + 0.5f, 0.5f, w - 1, Height - 3);
                    using (var p = Theme.Round(new RectangleF(r.X, r.Y + 1.5f, r.Width, r.Height), 7))
                    using (var b = new SolidBrush(border)) g.FillPath(b, p);
                    using (var p = Theme.Round(r, 7)) { g.FillPath(Brushes.White, p); using (var pen = new Pen(border)) g.DrawPath(pen, p); }
                    TextRenderer.DrawText(g, name, f, new Rectangle(x, 0, w, Height - 2), fg, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    x += w;
                    if (i < keys.Length - 1)
                    {
                        TextRenderer.DrawText(g, "+", f, new Rectangle(x, 0, P(16), Height - 2), Theme.Sub, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                        x += P(16);
                    }
                }
            }
        }
    }

    // thin progress line for downloads
    class ProgressLine : Control
    {
        float v;
        public ProgressLine() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true); BackColor = Theme.Card; Height = 4; }
        public float Value { get { return v; } set { v = Math.Max(0, Math.Min(1, value)); Invalidate(); } }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Theme.Round(new RectangleF(0, 0, Width - 1, Height - 1), Height / 2f)) using (var b = new SolidBrush(Theme.Track)) g.FillPath(b, p);
            if (v > 0) using (var p = Theme.Round(new RectangleF(0, 0, Math.Max(Height, (Width - 1) * v), Height - 1), Height / 2f)) using (var b = new SolidBrush(Theme.Accent)) g.FillPath(b, p);
        }
    }

    // ------------------------------------------------------------------ settings window (two columns, fits a laptop screen)
    class SettingsForm : Form
    {
        const int M = 20, Gap = 16, Col = 350, W = M * 2 + Col * 2 + Gap, X2 = M + Col + Gap;
        readonly Settings s;
        readonly Action changed;
        readonly bool preview;
        Label subtitle, stats, modeNote, keyStatus, chevron, engineLine, updateAction;
        KeyPill pill, cancelPill;
        bool capCancel;
        Segmented mode, engine;
        ProgressLine progress;
        Card advA, advB;
        TextBox keyBox;
        Timer keyTimer;
        int baseHeight;
        bool advOpen, engineBusy;
        Panel body;

        public SettingsForm(Settings s, Action changed)
        {
            this.s = s; this.changed = changed; preview = changed == null;
            Text = "FreeFlow"; ShowIcon = false; FormBorderStyle = FormBorderStyle.FixedSingle; MaximizeBox = false; MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen; BackColor = Theme.Bg; Font = Theme.F(10f); AutoScaleMode = AutoScaleMode.None;
            Icon = TrayApp.AppIcon();
            Build();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            int c = Theme.Bg.R | (Theme.Bg.G << 8) | (Theme.Bg.B << 16);
            Native.Dwm(Handle, 35, c);   // caption colour
            Native.Dwm(Handle, 34, c);   // border colour
            Native.Dwm(Handle, 33, 2);   // round corners
        }

        Label L(string text, Font f, Color c, int x, int y, Control parent)
        {
            var l = new Label { Text = text, Font = f, ForeColor = c, Left = x, Top = y, AutoSize = true, BackColor = parent is Card ? Theme.Card : Theme.Bg };
            parent.Controls.Add(l); return l;
        }

        void Build()
        {
            body = new Panel { Left = 0, Top = 0, Width = W, BackColor = Theme.Bg, AutoScroll = false };
            Controls.Add(body);

            int y = 14;
            // header
            var icon = new PictureBox { Left = M, Top = y, Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Theme.Bg };
            try { icon.Image = new Icon(TrayApp.AppIcon(), 64, 64).ToBitmap(); } catch { }
            body.Controls.Add(icon);
            L("FreeFlow", Theme.B(16f), Theme.Text, M + 60, y + 1, body);
            subtitle = L("", Theme.F(10f), Theme.Sub, M + 62, y + 29, body);
            stats = new Label { AutoSize = false, Width = 300, Height = 20, Left = W - M - 300, Top = y + 30, TextAlign = ContentAlignment.MiddleRight, Font = Theme.F(9.5f), ForeColor = Theme.Sub, BackColor = Theme.Bg, Text = s.StatsText() };
            body.Controls.Add(stats);
            y += 70;
            int top = y;

            // ---------------- left column
            Section("TALK KEY", M, ref y);
            var talk = NewCard(M, y, 52 + 58 + 52);
            L("Shortcut", Theme.F(10.5f), Theme.Text, 16, 15, talk);
            pill = new KeyPill { Top = 11 };
            talk.Controls.Add(pill);
            pill.Left = Col - 16 - 10; pill.Width = 10;
            pill.SetKeys(s.Hotkey);
            pill.Click += delegate { StartCapture(false); };
            talk.Seps.Add(52);
            mode = new Segmented(new[] { "Hold to talk", "Tap to start / stop" }, s.Toggle ? 1 : 0) { Left = 16, Top = 52 + 12, Width = Col - 32 };
            mode.Changed += delegate { s.Toggle = mode.Selected == 1; Save(); UpdateTexts(); };
            talk.Controls.Add(mode);
            talk.Seps.Add(110);
            L("Cancel key", Theme.F(10.5f), Theme.Text, 16, 110 + 15, talk);
            cancelPill = new KeyPill { Top = 110 + 11 };
            talk.Controls.Add(cancelPill);
            cancelPill.Left = Col - 16 - 10; cancelPill.Width = 10;
            cancelPill.SetKeys(new[] { s.CancelKey });
            cancelPill.Click += delegate { StartCapture(true); };
            y += talk.Height + 6;
            modeNote = L("", Theme.F(8.75f), Theme.Sub, M + 16, y, body);
            y += 34;

            Section("SPEECH TO TEXT", M, ref y);
            var sp = NewCard(M, y, 58 + 50);
            engine = new Segmented(new[] { "Cloud", LocalEngine.HasNvidia ? "My GPU" : "This PC" }, s.Engine == "local" ? 1 : 0) { Left = 16, Top = 12, Width = Col - 32 };
            engine.Changed += delegate { EngineChanged(); };
            sp.Controls.Add(engine);

            engineLine = new Label { AutoSize = false, Left = 16, Top = 58, Width = Col - 32, Height = 46, TextAlign = ContentAlignment.MiddleLeft, Font = Theme.F(9.75f), ForeColor = Theme.Sub, BackColor = Theme.Card, AutoEllipsis = true };
            sp.Controls.Add(engineLine);
            progress = new ProgressLine { Left = 16, Top = 58 + 40, Width = Col - 32, Visible = false };
            sp.Controls.Add(progress);
            y += sp.Height + 20;
            int leftBottom = y;

            // ---------------- right column
            y = top;
            Section("GENERAL", X2, ref y);
            var gen = NewCard(X2, y, 50 * 5);
            ToggleRow(gen, 0, "Clean up text", s.Cleanup, v => { s.Cleanup = v; Save(); });
            StyleRow(gen, 1);
            ToggleRow(gen, 2, "Sounds", s.Sounds, v => { s.Sounds = v; Save(); });
            bool startup = false; try { startup = Settings.StartWithWindows; } catch { }
            ToggleRow(gen, 3, "Start with Windows", startup, v => { if (!preview) try { Settings.StartWithWindows = v; } catch { } });
            LinkRow(gen, 4, "Vocabulary", "Edit", delegate { Process.Start("notepad.exe", "\"" + Settings.VocabPath + "\""); });
            y += gen.Height + 20;

            Section("GROQ API KEY", X2, ref y);
            var api = NewCard(X2, y, 50);
            keyBox = new TextBox { BorderStyle = BorderStyle.None, Font = Theme.F(10.5f), BackColor = Theme.Card, ForeColor = Theme.Text, Left = 16, UseSystemPasswordChar = true, Text = s.ApiKey };
            keyBox.Width = Col - 16 - 120; keyBox.Top = (50 - keyBox.Height) / 2; keyBox.Tag = 0;
            api.Controls.Add(keyBox);
            keyStatus = new Label { AutoSize = false, Width = 104, Height = 50, Left = Col - 114, Top = 0, TextAlign = ContentAlignment.MiddleRight, Font = Theme.F(9.5f), ForeColor = Theme.Sub, BackColor = Theme.Card };
            api.Controls.Add(keyStatus);
            keyTimer = new Timer { Interval = 900 };
            keyTimer.Tick += async delegate { keyTimer.Stop(); await CheckKey(true); };
            keyBox.TextChanged += delegate { keyStatus.Text = ""; keyTimer.Stop(); keyTimer.Start(); };
            y += 50 + 6;
            var get = new LinkLabel { Text = "Get a key at console.groq.com", Left = X2 + 16, Top = y, AutoSize = true, Font = Theme.F(8.75f), LinkColor = Theme.Accent, ActiveLinkColor = Theme.Accent, LinkBehavior = LinkBehavior.NeverUnderline, BackColor = Theme.Bg };
            get.LinkClicked += delegate { Process.Start("https://console.groq.com/keys"); };
            body.Controls.Add(get);
            y += 34;
            int rightBottom = y;

            // ---------------- advanced (full width, collapsed)
            y = Math.Max(leftBottom, rightBottom) - 8;
            var advRow = NewCard(M, y, 46);
            advRow.Width = W - 2 * M;
            L("Advanced", Theme.F(10.5f), Theme.Text, 16, 13, advRow);
            chevron = new Label { Text = "", Font = Theme.Icons(9f), ForeColor = Theme.Sub, AutoSize = false, Size = new Size(30, 46), Left = advRow.Width - 40, Top = 0, TextAlign = ContentAlignment.MiddleCenter, BackColor = Theme.Card };
            advRow.Controls.Add(chevron);
            var ver = new Label { Text = "Version " + Updater.Version, Font = Theme.F(9.5f), ForeColor = Theme.Sub, AutoSize = true, BackColor = Theme.Card, Top = 14 };
            advRow.Controls.Add(ver); ver.Left = advRow.Width - 48 - ver.PreferredWidth;
            advRow.Cursor = Cursors.Hand;
            foreach (Control c in advRow.Controls) c.Click += delegate { ToggleAdvanced(); };
            advRow.Click += delegate { ToggleAdvanced(); };
            y += 46 + 12;
            baseHeight = y + 8;

            advA = NewCard(M, y, 50 * 3);
            FieldRow(advA, 0, "Language", s.Language, "Auto", v => { s.Language = v; Save(); });
            FieldRow(advA, 1, "Cloud speech model", s.TranscriptionModel, "whisper-large-v3", v => { if (v != "") { s.TranscriptionModel = v; Save(); } });
            FieldRow(advA, 2, "Cleanup model", s.CleanupModel, "openai/gpt-oss-120b", v => { if (v != "") { s.CleanupModel = v; Save(); } });
            advB = NewCard(X2, y, 50 * 3);
            FieldRow(advB, 0, "API URL", s.BaseUrl, "https://api.groq.com/openai/v1", v => { if (v != "") { s.BaseUrl = v; Save(); } });
            LinkRow(advB, 1, "Cleanup prompt", "Edit", delegate { Process.Start("notepad.exe", "\"" + Settings.PromptPath + "\""); });
            updateAction = LinkRow(advB, 2, "Updates", "Check now", async delegate { await Updater.Check(true); });
            advA.Visible = advB.Visible = false;

            body.Height = baseHeight;
            ClientSize = new Size(W, baseHeight);
            // scale the whole layout for the screen's zoom level, then re-centre text boxes in their rows
            float S = Theme.S;
            if (S != 1f) Scale(new SizeF(S, S));
            pill.SetKeys(s.Hotkey); cancelPill.SetKeys(new[] { s.CancelKey });
            foreach (Control card in body.Controls)
                foreach (Control c in card.Controls)
                    if (c is TextBox && c.Tag is int) c.Top = (int)(((int)c.Tag) * S) + ((int)(50 * S) - c.Height) / 2;
            baseHeight = ClientSize.Height;
            UpdateTexts();
            ShowEngine();
            Action eng = delegate { if (!IsDisposed) ShowEngine(); };
            Action upd = delegate { if (!IsDisposed) BeginInvoke((Action)ShowUpdate); };
            LocalEngine.Changed += eng; Updater.Changed += upd;
            Shown += async delegate { ActiveControl = null; await CheckKey(false); };
            FormClosed += delegate { Hotkeys.StopCapture(); LocalEngine.Changed -= eng; Updater.Changed -= upd; };
        }

        void Section(string title, int x, ref int y) { L(title, Theme.F(8.5f), Theme.Sub, x + 16, y, body); y += 22; }

        Card NewCard(int x, int y, int h)
        {
            var c = new Card { Left = x, Top = y, Width = Col, Height = h };
            body.Controls.Add(c); return c;
        }

        void ToggleRow(Card c, int i, string label, bool value, Action<bool> set)
        {
            int top = i * 50;
            if (i > 0) c.Seps.Add(top);
            L(label, Theme.F(10.5f), Theme.Text, 16, top + 14, c);
            var t = new Toggle(value) { Left = c.Width - 16 - 46, Top = top + 11 };
            t.Changed += delegate { set(t.Checked); };
            c.Controls.Add(t);
        }

        void StyleRow(Card c, int i)
        {
            int top = i * 50;
            if (i > 0) c.Seps.Add(top);
            L("In chats", Theme.F(10.5f), Theme.Text, 16, top + 14, c);
            var seg = new Segmented(new[] { "Normal", "Casual", "lowercase" }, s.ChatStyle) { Width = 246, Top = top + 9, Height = 32 };
            seg.Left = c.Width - 16 - seg.Width;
            seg.Changed += delegate { s.ChatStyle = seg.Selected; Save(); };
            c.Controls.Add(seg);
        }

        Label LinkRow(Card c, int i, string label, string action, Action click)
        {
            int top = i * 50;
            if (i > 0) c.Seps.Add(top);
            L(label, Theme.F(10.5f), Theme.Text, 16, top + 14, c);
            var a = new Label { Text = action + "  ›", Font = Theme.F(10.5f), ForeColor = Theme.Sub, AutoSize = true, BackColor = Theme.Card, Cursor = Cursors.Hand };
            c.Controls.Add(a);
            a.Left = c.Width - 16 - a.PreferredWidth; a.Top = top + 14;
            a.Click += delegate { click(); };
            return a;
        }

        void FieldRow(Card c, int i, string label, string value, string cue, Action<string> set)
        {
            int top = i * 50;
            if (i > 0) c.Seps.Add(top);
            L(label, Theme.F(10.5f), Theme.Text, 16, top + 14, c);
            var t = new TextBox { BorderStyle = BorderStyle.None, Font = Theme.F(10f), ForeColor = Theme.Sub, BackColor = Theme.Card, Text = value, TextAlign = HorizontalAlignment.Right, Width = 150 };
            t.Left = c.Width - 16 - t.Width; t.Top = top + (50 - t.Height) / 2; t.Tag = top;
            c.Controls.Add(t);
            Native.SendMessage(t.Handle, 0x1501, (IntPtr)1, cue);
            t.TextChanged += delegate { set(t.Text.Trim()); };
        }

        void ToggleAdvanced()
        {
            advOpen = !advOpen;
            advA.Visible = advB.Visible = advOpen;
            chevron.Text = advOpen ? "" : "";
            int h = advOpen ? advA.Bottom + (int)(20 * Theme.S) : baseHeight;
            body.Height = h;
            ClientSize = new Size(ClientSize.Width, h);
            if (advOpen) ShowUpdate();
        }
        public void ExpandAdvanced() { if (!advOpen) ToggleAdvanced(); }
        public void PreviewToggle() { mode.Selected = 1; s.Toggle = true; UpdateTexts(); }

        void UpdateTexts()
        {
            string k = s.HotkeyText();
            subtitle.Text = s.Toggle ? "Press " + k + " to talk" : "Hold " + k + " to talk";
            modeNote.Text = s.Toggle ? "Press once to start, again to stop and paste." : "Hold while you talk, let go to paste. Double-tap for hands-free.";
        }

        void ShowUpdate()
        {
            if (updateAction == null) return;
            string st = Updater.Status == "" ? "Check now" : Updater.Status;
            updateAction.Text = st + "  ›";
            updateAction.Left = updateAction.Parent.Width - (int)(16 * Theme.S) - updateAction.PreferredWidth;
        }

        // ---- speech engine
        void ShowEngine()
        {
            bool local = engine.Selected == 1;
            progress.Visible = LocalEngine.Installing && LocalEngine.Progress >= 0;
            if (progress.Visible) progress.Value = LocalEngine.Progress;
            string gpu = LocalEngine.GpuName;
            if (!local) { Line("Groq  ·  " + s.TranscriptionModel + (string.IsNullOrEmpty(s.ApiKey) ? "  ·  needs a key" : ""), Theme.Sub); return; }
            if (LocalEngine.Installing) { Line(LocalEngine.Status, Theme.Text); return; }
            if (!LocalEngine.Installed) { Line(LocalEngine.Status.StartsWith("Download failed") ? LocalEngine.Status : (gpu == "" ? "This PC" : gpu) + "  ·  " + LocalEngine.DownloadSize + " download", LocalEngine.Status.StartsWith("Download failed") ? Theme.Red : Theme.Sub); return; }
            string name = LocalEngine.HasNvidia ? gpu : "CPU";
            bool ready = LocalEngine.Running;
            string st = ready ? "●  Ready" : (LocalEngine.Status == "" ? "Sleeping (loads when you talk)" : LocalEngine.Status);
            Line(name + "  ·  " + st, ready ? Theme.Green : Theme.Sub);
        }
        void Line(string t, Color c) { engineLine.Text = t; engineLine.ForeColor = c; }

        async void EngineChanged()
        {
            if (engineBusy) return;
            if (engine.Selected == 0)
            {
                s.Engine = "cloud"; Save();
                if (!preview) LocalEngine.Stop();
                ShowEngine();
                return;
            }
            if (!LocalEngine.Installed)
            {
                string where = LocalEngine.HasNvidia ? "your " + LocalEngine.GpuName : "this PC (no NVIDIA card found, it uses the CPU)";
                var ok = MessageBox.Show(this,
                    "Turn speech into text on " + where + "?\r\n\r\nOne-time download: " + LocalEngine.DownloadSize + ".\r\nWorks offline and your voice never leaves the PC.\r\nClean up still uses Groq if you have a key.",
                    "FreeFlow", MessageBoxButtons.OKCancel, MessageBoxIcon.None);
                if (ok != DialogResult.OK || preview) { engineBusy = true; engine.Selected = 0; engineBusy = false; ShowEngine(); return; }
                s.Engine = "local"; Save();
                ShowEngine();
                bool done = await LocalEngine.Install();
                if (IsDisposed) return;
                if (!done) { engineBusy = true; engine.Selected = 0; engineBusy = false; s.Engine = "cloud"; Save(); }
                ShowEngine();
                return;
            }
            s.Engine = "local"; Save();
            if (!preview) { var t = LocalEngine.Start(); }
            ShowEngine();
        }

        void Save()
        {
            if (preview) return;
            s.Save();
            if (changed != null) changed();
        }

        // ---- record a new talk key
        void StartCapture(bool forCancel)
        {
            if (!Hotkeys.Installed || pill.Capturing || cancelPill.Capturing) return;
            capCancel = forCancel;
            var target = forCancel ? cancelPill : pill;
            target.Capturing = true; target.SetKeys(new int[0]);
            subtitle.Text = forCancel ? "Press the key that should cancel. Esc keeps it as is." : "Press the key or keys you want. Esc to cancel.";
            Hotkeys.CaptureProgress = k => target.SetKeys(forCancel ? new[] { k[k.Length - 1] } : k);
            Hotkeys.CaptureCancelled = delegate { EndCapture(null); };
            Hotkeys.CaptureDone = k => EndCapture(k);
            Hotkeys.StartCapture();
        }

        void EndCapture(int[] keys)
        {
            if (capCancel)
            {
                cancelPill.Capturing = false;
                if (keys != null && keys.Length > 0)
                {
                    int vk = keys.Where(x => !KeyNames.IsModifier(x)).DefaultIfEmpty(keys[keys.Length - 1]).Last();
                    if (KeyNames.IsTypingKey(vk) || Array.IndexOf(s.Hotkey, vk) >= 0)
                        subtitle.Text = KeyNames.Name(vk) + (KeyNames.IsTypingKey(vk) ? " is a typing key. Pick F1-F12, Pause, Insert..." : " is your talk key. Pick another.");
                    else { s.CancelKey = vk; Save(); UpdateTexts(); }
                }
                else UpdateTexts();
                cancelPill.SetKeys(new[] { s.CancelKey });
                return;
            }
            pill.Capturing = false;
            if (keys != null && keys.Length == 1 && KeyNames.IsTypingKey(keys[0]))
            {
                pill.SetKeys(s.Hotkey); UpdateTexts();
                subtitle.Text = KeyNames.Name(keys[0]) + " is a typing key. Add Ctrl or Alt, or pick another.";
                return;
            }
            if (keys != null && keys.Length > 0) { s.Hotkey = keys; Save(); }
            pill.SetKeys(s.Hotkey);
            UpdateTexts();
        }

        // ---- api key
        async Task CheckKey(bool saveIfOk)
        {
            string k = keyBox.Text.Trim();
            if (k.Length == 0) { Status(s.Engine == "local" ? "Optional" : "Missing", s.Engine == "local" ? Theme.Sub : Theme.Red); return; }
            Status("Checking...", Theme.Sub);
            string err;
            try { err = await Api.TestKey(s.BaseUrl, k); } catch (Exception ex) { err = ex.Message; }
            if (IsDisposed) return;
            if (err == null) { Status("● Connected", Theme.Green); if (saveIfOk && k != s.ApiKey) { s.ApiKey = k; Save(); ShowEngine(); } }
            else Status("● Not working", Theme.Red);
        }
        void Status(string t, Color c) { keyStatus.Text = t; keyStatus.ForeColor = c; }

        public void RefreshStats() { if (!IsDisposed) stats.Text = s.StatsText(); }
    }

    // ------------------------------------------------------------------ recording pill (smooth, see-through)
    class Overlay : Form
    {
        public Func<float> LevelSource;
        public string DebugSave; public Action Saved; // QA screenshots only
        string title = "", hint = "";
        bool recording;
        readonly Timer anim = new Timer { Interval = 33 };
        readonly float[] bars = new float[15];   // level history, newest last; drawn mirrored from the centre
        float smooth;
        int tick, pillW;
        const int Pad = 16, H = 46;

        public Overlay()
        {
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
            anim.Tick += delegate { tick++; Shift(); Render(); };
        }
        protected override bool ShowWithoutActivation { get { return true; } }
        protected override CreateParams CreateParams
        {
            get { var cp = base.CreateParams; cp.ExStyle |= 0x80000 | 0x08000000 | 0x80 | 0x8 | 0x20; return cp; } // LAYERED|NOACTIVATE|TOOLWINDOW|TOPMOST|TRANSPARENT
        }

        void Shift()
        {
            float lv = (recording && LevelSource != null) ? Math.Min(1f, (float)Math.Sqrt(LevelSource() * 14f)) : 0f;
            smooth += (lv - smooth) * (lv > smooth ? 0.6f : 0.25f); // quick rise, soft fall
            for (int i = 0; i < bars.Length - 1; i++) bars[i] = bars[i + 1];
            bars[bars.Length - 1] = smooth;
        }

        public void ShowStatus(string title, string hint, bool recording)
        {
            this.title = title; this.hint = hint ?? ""; this.recording = recording;
            using (var bm = new Bitmap(1, 1)) using (var g = Graphics.FromImage(bm)) // 96 dpi units, scaled in Render
            using (var f = Theme.Semi(10.5f)) using (var fh = Theme.F(9.5f))
                pillW = (int)(18 + 12 + 12 + WaveW + 14 + Math.Max(g.MeasureString("Listening", f).Width, g.MeasureString(title, f).Width) + 16);
            if (!Visible) { for (int i = 0; i < bars.Length; i++) bars[i] = 0; Show(); }
            anim.Start();
            Render();
        }

        public void HideOverlay() { anim.Stop(); Hide(); }
        const float BarW = 3f, BarGap = 2.6f;
        float WaveW { get { return (bars.Length * 2 - 1) * (BarW + BarGap) - BarGap; } }

        void Render()
        {
            float S = Theme.S;
            int w = (int)((pillW + Pad * 2) * S), h = (int)((H + Pad * 2) * S);
            var scr = Screen.FromPoint(Cursor.Position).WorkingArea;
            var pos = new Native.POINT(scr.Left + (scr.Width - w) / 2, scr.Bottom - h - (int)(20 * S));
            using (var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.ScaleTransform(S, S);
                    g.SmoothingMode = SmoothingMode.AntiAlias; g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    var pill = new RectangleF(Pad, Pad, pillW, H);
                    for (int i = 8; i >= 1; i--) // soft shadow
                        using (var p = Theme.Round(new RectangleF(pill.X - i, pill.Y - i + 4, pill.Width + 2 * i, pill.Height + 2 * i), H / 2f + i))
                        using (var b = new SolidBrush(Color.FromArgb(6, 0, 0, 0))) g.FillPath(b, p);
                    using (var p = Theme.Round(pill, H / 2f))
                    {
                        using (var b = new SolidBrush(Color.FromArgb(240, 24, 24, 27))) g.FillPath(b, p);
                        using (var pen = new Pen(Color.FromArgb(40, 255, 255, 255))) g.DrawPath(pen, p);
                    }
                    float x = Pad + 18, cy = Pad + H / 2f;
                    // record button
                    if (recording)
                    {
                        float pulse = 0.55f + 0.45f * (float)Math.Abs(Math.Sin(tick / 6.0));
                        using (var b = new SolidBrush(Color.FromArgb((int)(70 * pulse), 255, 69, 58))) g.FillEllipse(b, x - 4, cy - 10, 20, 20);
                        using (var b = new SolidBrush(Color.FromArgb(255, 255, 69, 58))) g.FillEllipse(b, x + 1, cy - 5, 10, 10);
                    }
                    else
                        using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, x + 1, cy - 5, 10, 10);
                    x += 12 + 12;
                    // sound wave: newest level in the middle, older levels spread out to both sides
                    int n = bars.Length;
                    for (int i = 0; i < n * 2 - 1; i++)
                    {
                        int d = Math.Abs(i - (n - 1));            // distance from centre
                        float v;
                        if (recording) v = bars[n - 1 - d] * (1f - d * 0.035f);
                        else v = 0.18f + 0.16f * (float)Math.Sin(tick / 3.0 - i * 0.45);  // writing: calm moving wave
                        float bh = Math.Max(3f, 3f + v * 26f);
                        using (var p = Theme.Round(new RectangleF(x + i * (BarW + BarGap), cy - bh / 2, BarW, bh), BarW / 2))
                        using (var b = new SolidBrush(Color.FromArgb(recording ? 240 : 170, 255, 255, 255))) g.FillPath(b, p);
                    }
                    x += WaveW + 14;
                    using (var f = Theme.Semi(10.5f)) using (var b = new SolidBrush(Color.White))
                    {
                        var sz = g.MeasureString(title, f);
                        g.DrawString(title, f, b, x, cy - sz.Height / 2);
                    }
                }
                if (DebugSave != null) { bmp.Save(DebugSave); DebugSave = null; if (Saved != null) Saved(); }
                Apply(bmp, pos);
            }
        }

        void Apply(Bitmap bmp, Native.POINT pos)
        {
            IntPtr screen = Native.GetDC(IntPtr.Zero), mem = Native.CreateCompatibleDC(screen);
            IntPtr hb = bmp.GetHbitmap(Color.FromArgb(0)), old = Native.SelectObject(mem, hb);
            try
            {
                var size = new Native.SIZE(bmp.Width, bmp.Height);
                var src = new Native.POINT(0, 0);
                var blend = new Native.BLENDFUNCTION { Op = 0, Flags = 0, Alpha = 255, Format = 1 };
                Native.UpdateLayeredWindow(Handle, screen, ref pos, ref size, mem, ref src, 0, ref blend, 2);
            }
            finally
            {
                Native.SelectObject(mem, old); Native.DeleteObject(hb); Native.DeleteDC(mem); Native.ReleaseDC(IntPtr.Zero, screen);
            }
        }
    }
}
