// Shown after a successful export - always displays the password (even
// if the user typed their own, as confirmation of what was actually
// used) with a Copy button, since the file is now unconditionally
// encrypted (see IO/KeyFile.cs) and the recipient needs this password
// through a separate channel.
//
// Layout is computed dynamically from the wrapped label height instead
// of hardcoded Y coordinates: the message includes a full file path,
// whose length varies a lot (short filename vs. deep folder structure),
// and a fixed layout either wasted space or clipped the text off the
// right edge of the window for longer paths.

using System.Drawing;
using System.Windows.Forms;

namespace KeePassCopyKey.UI;

internal sealed class ExportResultDialog : Form
{
    private const int ButtonHeight = 30;
    private const int DialogWidth = 460;
    private const int Margin = 12;
    private const int LabelMaxWidth = DialogWidth - 2 * Margin;

    public ExportResultDialog(string password, string savedPath, int entryCount)
    {
        Text = "Copy Keys — done";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;

        var label = new Label
        {
            Text = $"Saved {entryCount} key(s) to:\n{savedPath}\n\nGive this password to the recipient through a separate channel (call, another chat):",
            AutoSize = true,
            MaximumSize = new Size(LabelMaxWidth, 0),
            Location = new Point(Margin, Margin),
        };
        label.Size = label.GetPreferredSize(new Size(LabelMaxWidth, 0));

        int textBoxY = label.Bottom + 15;
        var textBox = new TextBox
        {
            Text = password,
            ReadOnly = true,
            Font = new Font(FontFamily.GenericMonospace, 11),
            Location = new Point(Margin, textBoxY),
            Width = 300,
        };

        var copyButton = new Button
        {
            Text = "Copy",
            Location = new Point(320, textBoxY - 2),
            Size = new Size(DialogWidth - 320 - Margin, ButtonHeight),
        };
        copyButton.Click += (_, _) => Clipboard.SetText(password);

        int closeButtonY = textBoxY + ButtonHeight + 15;
        var okButton = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(320, closeButtonY),
            Size = new Size(DialogWidth - 320 - Margin, ButtonHeight),
        };

        ClientSize = new Size(DialogWidth, closeButtonY + ButtonHeight + Margin);

        Controls.Add(label);
        Controls.Add(textBox);
        Controls.Add(copyButton);
        Controls.Add(okButton);
        AcceptButton = okButton;
    }
}