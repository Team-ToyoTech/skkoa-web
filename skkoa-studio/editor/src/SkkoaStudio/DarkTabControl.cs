using System.Drawing.Drawing2D;

namespace SkkoaStudio;

internal sealed class DarkTabControl : TabControl
{
    private const int WmPaint = 0x000F;
    private bool headerRepaintQueued;

    public DarkTabControl()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        Appearance = TabAppearance.FlatButtons;
        SizeMode = TabSizeMode.Fixed;
    }

    public bool DarkTheme { get; set; } = true;
    public Color AccentColor { get; set; } = ColorTranslator.FromHtml("#a259ff");

    public override Rectangle DisplayRectangle
    {
        get
        {
            Rectangle rectangle = base.DisplayRectangle;
            return new Rectangle(
                rectangle.Left - 4,
                rectangle.Top - 5,
                rectangle.Width + 8,
                rectangle.Height + 9);
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        using SolidBrush brush = new(BackColor);
        e.Graphics.FillRectangle(brush, ClientRectangle);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        PaintHeader(e.Graphics);
    }

    protected override void WndProc(ref Message m)
    {
        base.WndProc(ref m);
        if (m.Msg == WmPaint && IsHandleCreated)
        {
            RepaintHeader();
            QueueHeaderRepaint();
        }
    }

    protected override void OnSelectedIndexChanged(EventArgs e)
    {
        base.OnSelectedIndexChanged(e);
        QueueHeaderRepaint();
    }

    protected override void OnControlAdded(ControlEventArgs e)
    {
        base.OnControlAdded(e);
        QueueHeaderRepaint();
    }

    protected override void OnControlRemoved(ControlEventArgs e)
    {
        base.OnControlRemoved(e);
        QueueHeaderRepaint();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        QueueHeaderRepaint();
    }

    private void QueueHeaderRepaint()
    {
        if (!IsHandleCreated || IsDisposed || headerRepaintQueued)
        {
            return;
        }

        headerRepaintQueued = true;
        try
        {
            BeginInvoke((Action)(() =>
            {
                headerRepaintQueued = false;
                if (!IsDisposed && IsHandleCreated)
                {
                    RepaintHeader();
                }
            }));
        }
        catch
        {
            headerRepaintQueued = false;
        }
    }

    private void RepaintHeader()
    {
        if (!IsHandleCreated || IsDisposed)
        {
            return;
        }

        try
        {
            using Graphics graphics = Graphics.FromHwnd(Handle);
            PaintHeader(graphics);
        }
        catch
        {
        }
    }

    private void PaintHeader(Graphics graphics)
    {
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        int headerHeight = ItemSize.Height + 7;
        Rectangle headerBounds = new(0, 0, Width, headerHeight);
        using SolidBrush background = new(BackColor);
        graphics.FillRectangle(background, headerBounds);

        for (int i = 0; i < TabPages.Count; i++)
        {
            DrawTab(graphics, i);
        }
    }

    private void DrawTab(Graphics graphics, int index)
    {
        Rectangle bounds = GetTabRect(index);
        bounds.Inflate(-1, -1);
        bool selected = index == SelectedIndex;
        Color tabBack = selected
            ? (DarkTheme ? ColorTranslator.FromHtml("#24242a") : Color.White)
            : (DarkTheme ? ColorTranslator.FromHtml("#1b1b1f") : ColorTranslator.FromHtml("#eef1f7"));
        Color text = DarkTheme ? ColorTranslator.FromHtml("#f2f2f2") : ColorTranslator.FromHtml("#202026");

        using SolidBrush backBrush = new(tabBack);
        graphics.FillRectangle(backBrush, bounds);
        TextRenderer.DrawText(
            graphics,
            TabPages[index].Text,
            Font,
            bounds,
            text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        if (selected)
        {
            using Pen accent = new(AccentColor, 3) { StartCap = LineCap.Round, EndCap = LineCap.Round };
            int y = bounds.Bottom - 2;
            graphics.DrawLine(accent, bounds.Left + 8, y, bounds.Right - 8, y);
        }
    }
}
