namespace FaceRecognition;

internal static class Ui
{
    public static readonly Color Ink = ColorTranslator.FromHtml("#1B383E");
    public static readonly Color Paper = ColorTranslator.FromHtml("#EFF4F2");
    public static readonly Color Accent = ColorTranslator.FromHtml("#24656A");
    public static readonly Color Soft = ColorTranslator.FromHtml("#DAE9E4");

    public static TableLayoutPanel Rows(params float[] heights)
    {
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, Margin = Padding.Empty, Padding = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowCount = heights.Length;
        foreach (float height in heights)
            grid.RowStyles.Add(new RowStyle(height < 0 ? SizeType.Percent : SizeType.Absolute, Math.Abs(height)));
        return grid;
    }

    public static Label Label(string text, float size = 10, bool bold = false) => new()
    {
        Text = text, AutoSize = false, Dock = DockStyle.Fill, Margin = Padding.Empty,
        TextAlign = ContentAlignment.MiddleLeft, ForeColor = Ink,
        Font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular)
    };

    public static Button Button(string text, Action action, bool primary = false)
    {
        var b = new Button
        {
            Text = text, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            MinimumSize = new Size(84, 34), Padding = new Padding(9, 1, 9, 1),
            FlatStyle = FlatStyle.Flat, BackColor = primary ? Accent : Soft,
            ForeColor = primary ? Color.White : Ink, Margin = new Padding(0, 3, 8, 3),
            Cursor = Cursors.Hand, UseVisualStyleBackColor = false
        };
        b.FlatAppearance.BorderSize = 0;
        b.Click += (_, _) => action();
        return b;
    }

    public static FlowLayoutPanel Bar(params Control[] controls)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
        panel.Controls.AddRange(controls);
        return panel;
    }

    public static Control Header(string title, string subtitle)
    {
        var box = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, BackColor = Ink, Padding = new Padding(16, 6, 12, 6), Margin = Padding.Empty };
        box.RowCount = 1;
        box.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        box.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 58));
        box.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var mark = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 4, 10, 4) };
        using var stream = typeof(Ui).Assembly.GetManifestResourceStream("FaceRecognition.Assets.mark.png");
        if (stream != null) { using var bitmap = new Bitmap(stream); mark.Image = new Bitmap(bitmap); }
        var words = Rows(38, -100);
        var heading = Label(title, 21, true);
        var caption = Label(subtitle, 10);
        heading.ForeColor = Color.White;
        caption.ForeColor = Soft;
        words.Controls.Add(heading, 0, 0);
        words.Controls.Add(caption, 0, 1);
        box.Controls.Add(mark, 0, 0);
        box.Controls.Add(words, 1, 0);
        return box;
    }

    public static void Window(Form form, string title, Size size, Size minimum)
    {
        form.Text = title;
        form.Font = new Font("Segoe UI", 10);
        form.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        form.StartPosition = FormStartPosition.CenterScreen;
        form.ClientSize = size;
        form.MinimumSize = minimum;
        form.BackColor = Paper;
        form.Padding = new Padding(18);
    }
}
