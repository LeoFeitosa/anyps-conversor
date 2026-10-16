using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AnyPSConversor;

/// <summary>Tema escuro azul-marinho com destaque azul. Toda a arte é desenhada em código.</summary>
static class Theme
{
    public static readonly Color Background = Color.FromArgb(0x0F, 0x14, 0x1C);
    public static readonly Color Surface = Color.FromArgb(0x17, 0x1E, 0x29);
    public static readonly Color Input = Color.FromArgb(0x11, 0x17, 0x20);
    public static readonly Color Border = Color.FromArgb(0x24, 0x30, 0x42);
    public static readonly Color Text = Color.FromArgb(0xE6, 0xEB, 0xF2);
    public static readonly Color Muted = Color.FromArgb(0x8C, 0x98, 0xA9);
    public static readonly Color Accent = Color.FromArgb(0x3B, 0x82, 0xF6);
    public static readonly Color AccentHover = Color.FromArgb(0x2F, 0x6F, 0xD8);
    public static readonly Color AccentSoft = Color.FromArgb(0x7A, 0xB0, 0xFF);
    public static readonly Color Cancel = Color.FromArgb(0x2A, 0x34, 0x45);
    public static readonly Color Success = Color.FromArgb(0x34, 0xC7, 0x7B);
    public static readonly Color Warning = Color.FromArgb(0xF0, 0xB4, 0x29);
    public static readonly Color Error = Color.FromArgb(0xF0, 0x5D, 0x5E);
    public static readonly Color HeaderLeft = Color.FromArgb(0x0B, 0x10, 0x18);
    public static readonly Color HeaderRight = Color.FromArgb(0x0B, 0x10, 0x18);
    public static readonly Color HeaderMuted = Color.FromArgb(0x8C, 0x98, 0xA9);
    public static readonly Color Footer = Color.FromArgb(0x0B, 0x10, 0x18);

    public static readonly Font Body = new("Segoe UI", 9.75f);
    public static readonly Font Small = new("Segoe UI", 8.5f);
    public static readonly Font Subtitle = new("Segoe UI", 10.5f);
    public static readonly Font CardTitle = new("Segoe UI Semibold", 12.5f);
    public static readonly Font GameName = new("Segoe UI Semibold", 14f);
    public static readonly Font BigButton = new("Segoe UI Semibold", 12f);
    public static readonly Font AppTitle = new("Segoe UI Semibold", 19f);

    public static Label Label(string text, Font font, Color color, ContentAlignment align = ContentAlignment.TopLeft) =>
        new() { Text = text, Font = font, ForeColor = color, TextAlign = align, AutoSize = false, BackColor = Color.Transparent };

    public static LinkLabel Link() => new()
    {
        Font = Small, AutoSize = false, LinkColor = AccentSoft, ActiveLinkColor = Color.White,
        LinkBehavior = LinkBehavior.HoverUnderline, BackColor = Color.Transparent,
    };

    public static RoundButton AccentButton() => new() { Primary = true, Font = Body };

    public static RoundButton SecondaryButton() => new() { Primary = false, Font = Body };

    public static GraphicsPath Rounded(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    /// <summary>Marca do app: controle genérico sobre quadrado arredondado. Mesma geometria de build/generate-icon.ps1.</summary>
    public static void DrawMark(Graphics g, RectangleF r)
    {
        using (var tile = Rounded(r, r.Width * 0.22f))
        using (var fill = new SolidBrush(Accent))
            g.FillPath(fill, tile);
        DrawGamepad(g, r, Color.White, Accent);
    }

    /// <summary>Controle genérico em coordenadas relativas ao quadrado <paramref name="r"/>.</summary>
    public static void DrawGamepad(Graphics g, RectangleF r, Color body, Color holes)
    {
        float X(float v) => r.X + v * r.Width;
        float Y(float v) => r.Y + v * r.Height;
        float S(float v) => v * r.Width;

        using var bodyBrush = new SolidBrush(body);
        g.FillEllipse(bodyBrush, X(0.13f), Y(0.41f), S(0.34f), S(0.38f));
        g.FillEllipse(bodyBrush, X(0.53f), Y(0.41f), S(0.34f), S(0.38f));
        using (var top = Rounded(new RectangleF(X(0.12f), Y(0.30f), S(0.76f), S(0.32f)), S(0.14f)))
            g.FillPath(bodyBrush, top);

        using var holeBrush = new SolidBrush(holes);
        g.FillRectangle(holeBrush, X(0.235f), Y(0.445f), S(0.15f), S(0.05f));
        g.FillRectangle(holeBrush, X(0.285f), Y(0.395f), S(0.05f), S(0.15f));
        g.FillEllipse(holeBrush, X(0.615f), Y(0.475f), S(0.07f), S(0.07f));
        g.FillEllipse(holeBrush, X(0.695f), Y(0.395f), S(0.07f), S(0.07f));
    }

    /// <summary>
    /// Encurta o texto pelo meio ("início…final.rar") até caber em <paramref name="limit"/> pixels, preservando o
    /// fim (a extensão). Nomes sem espaço não quebram linha; sem isso um nome comprido empurraria o resto para fora.
    /// </summary>
    public static string ShortenMiddle(string name, Font font, int limit)
    {
        bool Fits(string s) => TextRenderer.MeasureText(s, font).Width <= limit;
        if (Fits(name))
            return name;
        var tail = Math.Min(name.Length / 3, 18);
        for (var head = name.Length - tail - 1; head > 4; head--)
        {
            var candidate = name[..head] + "…" + name[^tail..];
            if (Fits(candidate))
                return candidate;
        }
        return name.Length > tail + 4 ? name[..4] + "…" + name[^tail..] : name;
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void UseDarkTitleBar(IntPtr handle)
    {
        var enabled = 1;
        _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
    }
}

sealed class HeaderPanel : Panel
{
    public HeaderPanel() => DoubleBuffered = true;

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Theme.HeaderLeft);
        var scale = DeviceDpi / 96f;
        using (var line = new SolidBrush(Theme.Border))
            g.FillRectangle(line, 0, Height - scale, Width, scale);

        Theme.DrawMark(g, new RectangleF(26 * scale, 16 * scale, 40 * scale, 40 * scale));
        var title = new Point((int)(78 * scale), (int)(12 * scale));
        TextRenderer.DrawText(g, "AnyPS", Theme.AppTitle, title, Color.White);
        var width = TextRenderer.MeasureText(g, "AnyPS", Theme.AppTitle).Width;
        TextRenderer.DrawText(g, Strings.Current == Language.Portuguese ? "Conversor" : "Converter", Theme.AppTitle,
            new Point(title.X + width - (int)(6 * scale), title.Y), Theme.HeaderMuted);
    }
}

class Card : Panel
{
    public Card()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Background);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Rounded(rect, 12 * DeviceDpi / 96f);
        using var fill = new SolidBrush(Theme.Surface);
        using var border = new Pen(Theme.Border);
        g.FillPath(fill, path);
        g.DrawPath(border, path);
    }
}

sealed class DropZone : Panel
{
    bool highlight;

    public DropZone()
    {
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Highlight
    {
        get => highlight;
        set
        {
            if (highlight == value)
                return;
            highlight = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(1, 1, Width - 3, Height - 3);
        using var path = Theme.Rounded(rect, 10 * DeviceDpi / 96f);
        using var fill = new SolidBrush(highlight ? Color.FromArgb(0x16, 0x24, 0x3A) : Theme.Input);
        using var pen = new Pen(highlight ? Theme.Accent : Color.FromArgb(0x33, 0x40, 0x55), 1.5f) { DashStyle = DashStyle.Dash };
        g.FillPath(fill, path);
        g.DrawPath(pen, path);
    }
}

sealed class StepBadge : Control
{
    readonly int number;
    bool done;

    public StepBadge(int number)
    {
        this.number = number;
        DoubleBuffered = true;
        BackColor = Theme.Surface;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Done
    {
        get => done;
        set
        {
            done = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var r = new RectangleF(0, 0, Width - 1, Height - 1);
        using var fill = new SolidBrush(done ? Theme.Success : Theme.Accent);
        g.FillEllipse(fill, r);
        TextRenderer.DrawText(g, done ? "✓" : number.ToString(), Theme.CardTitle, ClientRectangle, Color.White,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
    }
}

/// <summary>
/// Barra de carregamento no estilo dos jogos: trilho escuro, preenchimento com listras diagonais que
/// andam enquanto há trabalho e um brilho no topo.
/// </summary>
sealed class GameProgress : Control
{
    readonly System.Windows.Forms.Timer timer = new() { Interval = 16 };
    int value;
    float offset;

    public GameProgress()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Theme.Surface;
        timer.Tick += (_, _) =>
        {
            offset = (offset + 0.6f) % 24f;
            Invalidate();
        };
    }

    /// <summary>De 0 a 1000.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public int Value
    {
        get => value;
        set
        {
            this.value = Math.Clamp(value, 0, 1000);
            Invalidate();
        }
    }

    /// <summary>Listras em movimento enquanto há trabalho.</summary>
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Animated
    {
        get => timer.Enabled;
        set
        {
            timer.Enabled = value;
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var scale = DeviceDpi / 96f;
        var radius = Height / 2f;
        var track = new RectangleF(0, 0, Width - 1, Height - 1);
        using (var path = Theme.Rounded(track, radius))
        using (var brush = new SolidBrush(Theme.Input))
        using (var pen = new Pen(Theme.Border))
        {
            g.FillPath(brush, path);
            g.DrawPath(pen, path);
        }

        var width = (Width - 1) * value / 1000f;
        if (width < Height)
            return;
        var bar = new RectangleF(0, 0, width, Height - 1);
        using var barPath = Theme.Rounded(bar, radius);
        using (var fill = new SolidBrush(Theme.Accent))
            g.FillPath(fill, barPath);

        var state = g.Save();
        g.SetClip(barPath);
        var step = 24 * scale;
        using (var stripe = new SolidBrush(Color.FromArgb(46, 255, 255, 255)))
        {
            for (var x = -2 * step + offset * scale; x < width + step; x += step)
                g.FillPolygon(stripe, [new PointF(x, Height), new PointF(x + step / 2, Height), new PointF(x + step / 2 + Height, 0), new PointF(x + Height, 0)]);
        }
        using (var shine = new SolidBrush(Color.FromArgb(40, 255, 255, 255)))
            g.FillRectangle(shine, 0, 0, width, Height / 2.5f);
        g.Restore(state);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            timer.Dispose();
        base.Dispose(disposing);
    }
}

/// <summary>Botão arredondado: principal (preenchido) ou secundário (contorno).</summary>
sealed class RoundButton : Button
{
    bool hover, pressed, primary;

    public RoundButton()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool Primary
    {
        get => primary;
        set
        {
            primary = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover = pressed = false; Invalidate(); base.OnMouseLeave(e); }
    protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
    protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(Parent?.BackColor ?? Theme.Surface);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        var rect = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
        using var path = Theme.Rounded(rect, Math.Min(8 * DeviceDpi / 96f, Height / 2f));

        Color fill, text;
        if (primary)
        {
            fill = !Enabled ? Theme.Cancel : pressed ? Color.FromArgb(0x25, 0x60, 0xC2) : hover ? Theme.AccentHover : Theme.Accent;
            text = Enabled ? Color.White : Theme.Muted;
        }
        else
        {
            fill = pressed ? Color.FromArgb(0x22, 0x2D, 0x3E) : hover ? Color.FromArgb(0x1E, 0x28, 0x37) : Theme.Surface;
            text = Enabled ? Theme.Text : Theme.Muted;
        }
        using (var brush = new SolidBrush(fill))
            g.FillPath(brush, path);
        if (!primary)
        {
            using var pen = new Pen(hover ? Theme.Accent : Color.FromArgb(0x33, 0x40, 0x55));
            g.DrawPath(pen, path);
        }
        TextRenderer.DrawText(g, Text, Font, ClientRectangle, text,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
    }
}
