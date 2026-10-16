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

    public PasswordDialog(string fileName, bool wrong)
    {
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(480, 250);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Theme.Surface;
        Font = Theme.Body;
        Text = T("password_title");

        var title = Theme.Label(T("password_title"), Theme.CardTitle, Theme.Text);
        title.SetBounds(24, 18, 432, 28);
        var message = Theme.Label(T("password_message", fileName), Theme.Body, Theme.Muted);
        message.SetBounds(24, 50, 432, 58);
        message.AutoEllipsis = true;
        var error = Theme.Label(wrong ? T("password_wrong") : "", Theme.Body, Theme.Error);
        error.SetBounds(24, 108, 432, 20);
        passwordBox.SetBounds(24, 132, 432, 30);
        var show = new CheckBox { Text = T("password_show"), ForeColor = Theme.Muted, AutoSize = true, Location = new Point(24, 170) };
        show.CheckedChanged += (_, _) => passwordBox.UseSystemPasswordChar = !show.Checked;

        var ok = Theme.AccentButton();
        ok.Text = T("password_ok");
        ok.SetBounds(264, 200, 192, 36);
        ok.DialogResult = DialogResult.OK;
        var cancel = Theme.SecondaryButton();
        cancel.Text = T("cancel");
        cancel.SetBounds(140, 200, 112, 36);
        cancel.DialogResult = DialogResult.Cancel;

        Controls.AddRange([title, message, error, passwordBox, show, cancel, ok]);
        AcceptButton = ok;
        CancelButton = cancel;
        Shown += (_, _) => passwordBox.Focus();
        ResumeLayout(false);
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
