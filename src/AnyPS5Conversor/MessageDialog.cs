using System.Drawing.Drawing2D;
using static AnyPS5Conversor.Strings;

namespace AnyPS5Conversor;

/// <summary>
/// Aviso no visual do programa, no lugar da caixa de mensagem do Windows. Textos curtos aparecem inteiros;
/// textos longos (como trechos de registro) ganham barra de rolagem em vez de esticar a janela.
/// Sem <c>warning</c>, é só informativo: sem o triângulo e mais largo, para leitura (como as licenças).
/// </summary>
sealed class MessageDialog : Form
{
    const int MaxTextHeight = 260;

    MessageDialog(string title, string message, bool warning)
    {
        var textLeft = warning ? 76 : 24;
        var textWidth = warning ? 380 : 560;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        Text = title;

        var icon = new WarningIcon();
        icon.SetBounds(24, 22, 36, 36);
        var heading = Theme.Label(title, Theme.CardTitle, Theme.Text);
        heading.SetBounds(textLeft, 20, textWidth, 28);

        var scale = DeviceDpi / 96f;
        var measured = TextRenderer.MeasureText(message, Theme.Body, new Size((int)(textWidth * scale), int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);
        var textHeight = (int)Math.Ceiling(measured.Height / scale) + 4;
        Control body;
        if (textHeight <= MaxTextHeight)
        {
            body = Theme.Label(message, Theme.Body, Theme.Muted);
        }
        else
        {
            body = new TextBox
            {
                Text = message.ReplaceLineEndings("\r\n"), Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.None, BackColor = Theme.Input, ForeColor = Theme.Muted, Font = Theme.Body,
            };
            textHeight = MaxTextHeight;
        }
        body.SetBounds(textLeft, 54, textWidth, textHeight);

        var y = 54 + textHeight + 20;
        var ok = Theme.AccentButton();
        ok.Text = "OK";
        ok.SetBounds(textLeft + textWidth - 120, y, 120, 36);
        ok.DialogResult = DialogResult.OK;
        ClientSize = new Size(textLeft + textWidth + 24, y + 36 + 18);

        Controls.AddRange(warning ? [icon, heading, body, ok] : [heading, body, ok]);
        AcceptButton = CancelButton = ok;
        Shown += (_, _) => ok.Focus();
        ResumeLayout(false);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    public static void Show(IWin32Window owner, string title, string message, bool warning = true)
    {
        using var dialog = new MessageDialog(title, message, warning);
        dialog.ShowDialog(owner);
    }

    /// <summary>Triângulo de aviso desenhado em código, na cor de alerta do tema.</summary>
    sealed class WarningIcon : Control
    {
        public WarningIcon()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Theme.Surface;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Surface);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float w = Width - 1, h = Height - 1;
            using (var fill = new SolidBrush(Theme.Warning))
            using (var outline = new Pen(Theme.Warning, w * 0.08f) { LineJoin = LineJoin.Round })
            {
                PointF[] triangle = [new(w / 2, h * 0.06f), new(w * 0.96f, h * 0.92f), new(w * 0.04f, h * 0.92f)];
                g.FillPolygon(fill, triangle);
                g.DrawPolygon(outline, triangle);
            }
            using var mark = new SolidBrush(Theme.Surface);
            g.FillRectangle(mark, w * 0.455f, h * 0.34f, w * 0.09f, h * 0.32f);
            g.FillEllipse(mark, w * 0.445f, h * 0.71f, w * 0.11f, h * 0.11f);
        }
    }
}
