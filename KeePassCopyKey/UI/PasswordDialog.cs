// UI-layer only, no KeePassLib dependency. Reused for both the export
// ("set a password") and import ("enter a password") flows.
//
// Export mode pre-fills a random password immediately when the dialog
// opens - the user sees and can copy/note it right away, before the
// file is even saved. Password is always mandatory (see IO/KeyFile.cs):
// clicking OK with an empty field just re-shows a warning instead of
// closing.
//
// Layout is computed from the label's actual (possibly wrapped) height
// instead of hardcoded Y offsets - same fix as ExportResultDialog used
// before it was removed. The label text length varies between the
// export and import wording, and a fixed offset for the password box
// assumed a single line; when the text wrapped to two lines, the
// password box ended up overlapping the second line instead of sitting
// below it.

using System.Drawing;
using System.Windows.Forms;
using KeePassCopyKey.Crypto;

namespace KeePassCopyKey.UI;

internal sealed class PasswordDialog : Form
{
    private const int ButtonHeight = 30;
    private const int DialogWidth = 400;
    private const int Margin = 12;
    private const int LabelMaxWidth = DialogWidth - 2 * Margin;

    private readonly TextBox _passwordBox;

    public string Password => _passwordBox.Text;

    public PasswordDialog(bool forExport)
    {
        Text = forExport ? "Export Keys — password" : "Import Keys — enter password";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var label = new Label
        {
            Text = forExport ? "Password (a random one is pre-filled - you can change it):" : "Password for this file:",
            AutoSize = true,
            MaximumSize = new Size(LabelMaxWidth, 0),
            Location = new Point(Margin, Margin),
        };
        label.Size = label.GetPreferredSize(new Size(LabelMaxWidth, 0));

        int passwordBoxY = label.Bottom + 12;
        _passwordBox = new TextBox
        {
            Location = new Point(Margin, passwordBoxY),
            Width = forExport ? DialogWidth - Margin - 118 - Margin : LabelMaxWidth,
            UseSystemPasswordChar = false,
        };

        if (forExport)
            _passwordBox.Text = KeyFileCrypto.GeneratePassword();

        Controls.Add(label);
        Controls.Add(_passwordBox);

        if (forExport)
        {
            var generateButton = new Button
            {
                Text = "Generate",
                Location = new Point(_passwordBox.Right + 10, passwordBoxY - 2),
                Size = new Size(108, ButtonHeight),
            };
            generateButton.Click += (_, _) => _passwordBox.Text = KeyFileCrypto.GeneratePassword();
            Controls.Add(generateButton);
        }

        int buttonRowY = passwordBoxY + _passwordBox.Height + 20;

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(DialogWidth - Margin - 80 - 8 - 80, buttonRowY),
            Size = new Size(80, ButtonHeight),
        };
        okButton.Click += (_, e) =>
        {
            if (_passwordBox.Text.Length == 0)
            {
                MessageBox.Show(this, "Password must not be empty.",
                    forExport ? "Export Keys" : "Import Keys",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(DialogWidth - Margin - 80, buttonRowY),
            Size = new Size(80, ButtonHeight),
        };

        ClientSize = new Size(DialogWidth, buttonRowY + ButtonHeight + Margin);

        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }
}