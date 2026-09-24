// Shown after a successful export - always displays the password (even
// if the user typed their own, as confirmation of what was actually
// used) with a Copy button, since the file is now unconditionally
// encrypted (see IO/KeyFile.cs) and the recipient needs this password
// through a separate channel.

using System.Drawing;
using System.Windows.Forms;

namespace KeePassCopyKey.UI;

internal sealed class ExportResultDialog : Form
{
    private const int ButtonHeight = 30;

    public ExportResultDialog(string password, string savedPath, int entryCount)
    {
        Text = "Copy Keys — done";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ClientSize = new Size(440, 190);

        var label = new Label
        {
            Text = $"Saved {entryCount} key(s) to:\n{savedPath}\n\nGive this password to the recipient through a separate channel (call, another chat):",
            AutoSize = true,
            Location = new Point(12, 12),
        };

        var textBox = new TextBox
        {
            Text = password,
            ReadOnly = true,
            Font = new Font(FontFamily.GenericMonospace, 12),
            Location = new Point(12, 105),
            Width = 310,
        };

        var copyButton = new Button
        {
            Text = "Copy",
            Location = new Point(330, 103),
            Size = new Size(98, ButtonHeight),
        };
        copyButton.Click += (_, _) => Clipboard.SetText(password);

        var okButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(330, 145),
            Size = new Size(98, ButtonHeight),
        };

        Controls.Add(label);
        Controls.Add(textBox);
        Controls.Add(copyButton);
        Controls.Add(okButton);
        AcceptButton = okButton;
    }
}