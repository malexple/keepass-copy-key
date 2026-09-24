// UI-layer only, no KeePassLib dependency. Reused for both the export
// ("set a password") and import ("enter a password") flows.
//
// No "do not encrypt" escape hatch here (see IO/KeyFile.cs header) - on
// export, leaving the field blank means "generate a random password for
// me" (handled by the caller, KeePassCopyKeyExt.RunExport), not "skip
// encryption". The Generate button is a convenience for producing that
// same random password up front, so the user can see and note it down
// before saving, rather than only afterward.

using System.Drawing;
using System.Windows.Forms;
using KeePassCopyKey.Crypto;

namespace KeePassCopyKey.UI;

internal sealed class PasswordDialog : Form
{
    private const int ButtonHeight = 30;

    private readonly TextBox _passwordBox;

    public string Password => _passwordBox.Text;

    public PasswordDialog(bool forExport)
    {
        Text = forExport ? "Copy Keys — set password" : "Load Keys — enter password";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(400, 130);

        var label = new Label
        {
            Text = forExport ? "Password (leave blank to auto-generate one):" : "Password for this file:",
            AutoSize = true,
            Location = new Point(12, 12),
        };

        _passwordBox = new TextBox
        {
            Location = new Point(12, 36),
            Width = forExport ? 260 : 376,
            UseSystemPasswordChar = true,
        };

        Controls.Add(label);
        Controls.Add(_passwordBox);

        if (forExport)
        {
            var generateButton = new Button
            {
                Text = "Generate",
                Location = new Point(280, 34),
                Size = new Size(108, ButtonHeight),
            };
            generateButton.Click += (_, _) =>
            {
                _passwordBox.UseSystemPasswordChar = false;
                _passwordBox.Text = KeyFileCrypto.GeneratePassword();
            };
            Controls.Add(generateButton);
        }

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(224, 82),
            Size = new Size(80, ButtonHeight),
        };
        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(312, 82),
            Size = new Size(80, ButtonHeight),
        };

        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}