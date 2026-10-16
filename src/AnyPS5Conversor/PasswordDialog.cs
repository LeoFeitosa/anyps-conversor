using static AnyPS5Conversor.Strings;

namespace AnyPS5Conversor;

/// <summary>Pede a senha de um arquivo compactado. A senha não é guardada em lugar nenhum.</summary>
sealed class PasswordDialog : Form
{
    readonly TextBox passwordBox = new()
    {
        UseSystemPasswordChar = true, BorderStyle = BorderStyle.FixedSingle, Font = Theme.Subtitle,
        BackColor = Theme.Input, ForeColor = Theme.Text,
    };

    public string Password => passwordBox.Text;

    const int ContentWidth = 432;

    public PasswordDialog(string fileName, bool wrong)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        Text = T("password_title");

        // O texto da mensagem nunca é cortado: se faltar espaço, encurta-se o nome do arquivo, pelo meio.
        var text = T("password_message", Theme.ShortenMiddle(fileName, Theme.Body, (int)(ContentWidth * DeviceDpi / 96f * 0.9f)));
        var textHeight = Math.Min(MeasureHeight(text, Theme.Body, ContentWidth), 120);

        var title = Theme.Label(T("password_title"), Theme.CardTitle, Theme.Text);
        title.SetBounds(24, 18, ContentWidth, 28);
        var message = Theme.Label(text, Theme.Body, Theme.Muted);
        message.SetBounds(24, 50, ContentWidth, textHeight);
        var y = 50 + textHeight + 8;
        var error = Theme.Label(wrong ? T("password_wrong") : "", Theme.Body, Theme.Error);
        error.SetBounds(24, y, ContentWidth, wrong ? 22 : 0);
        y += wrong ? 26 : 0;
        passwordBox.SetBounds(24, y, ContentWidth, 30);
        y += 38;
        var show = new CheckBox { Text = T("password_show"), ForeColor = Theme.Muted, AutoSize = true, Location = new Point(24, y) };
        show.CheckedChanged += (_, _) => passwordBox.UseSystemPasswordChar = !show.Checked;
        y += 32;

        var ok = Theme.AccentButton();
        ok.Text = T("password_ok");
        ok.SetBounds(264, y, 192, 36);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.SecondaryButton();
        cancel.Text = T("cancel");
        cancel.SetBounds(140, y, 112, 36);
        cancel.DialogResult = DialogResult.Cancel;
        ClientSize = new Size(480, y + 36 + 18);

        Controls.AddRange([title, message, error, passwordBox, show, cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => passwordBox.Focus();
        ResumeLayout(false);
    }

    /// <summary>Altura do texto quebrado em linhas, em unidades de 96 dpi como o resto do layout.</summary>
    int MeasureHeight(string text, Font font, int width)
    {
        var scale = DeviceDpi / 96f;
        var size = TextRenderer.MeasureText(text, font, new Size((int)(width * scale), int.MaxValue), TextFormatFlags.WordBreak);
        return (int)Math.Ceiling(size.Height / scale) + 2;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Theme.UseDarkTitleBar(Handle);
    }

    /// <summary>Mostra a janela e devolve a senha digitada, ou nulo se o usuário cancelar.</summary>
    public static string? Ask(IWin32Window owner, string fileName, bool wrong)
    {
        using var dialog = new PasswordDialog(fileName, wrong);
        return dialog.ShowDialog(owner) == DialogResult.OK ? dialog.Password : null;
    }
}
