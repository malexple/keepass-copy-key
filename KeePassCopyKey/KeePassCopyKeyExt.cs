// Plugin entry point. Thin: no logging, no protocol logic here - that
// lives in Crypto/IO. This file's job beyond Plugin lifecycle is (a) the
// PwEntry <-> KeyEntryRecord mapping and (b) wiring up both the Tools
// menu and the main toolbar buttons.
//
// Toolbar button placement: KeePass's Plugin base class has no official
// "add a toolbar button" extension point (only GetMenuItem for menus).
// The community-standard workaround - documented on the KeePass
// SourceForge forum and used by several existing plugins - is to find
// the main ToolStrip by its internal control name via Controls.Find
// (works because Control.Name is public even though the field holding
// it in KeePass's MainForm is private) and insert items into it
// directly. Per that same guidance, anything added this way must be
// removed again in Terminate() - KeePass does not do this for you. The
// two icon Bitmaps (see UI/ToolbarIcons.cs) are owned by this class and
// must be disposed there too.

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassCopyKey.Crypto;
using KeePassCopyKey.IO;
using KeePassCopyKey.UI;
using KeePassLib;
using KeePassLib.Security;

namespace KeePassCopyKey;

public sealed class KeePassCopyKeyExt : Plugin
{
    private IPluginHost? _host;

    private ToolStripSeparator? _toolbarSeparator;
    private ToolStripButton? _copyButton;
    private ToolStripButton? _loadButton;
    private Bitmap? _copyIcon;
    private Bitmap? _loadIcon;

    public override bool Initialize(IPluginHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        AddToolbarButtons();
        return true;
    }

    public override void Terminate()
    {
        RemoveToolbarButtons();
        _copyIcon?.Dispose();
        _loadIcon?.Dispose();
    }

    public override ToolStripMenuItem? GetMenuItem(PluginMenuType t)
    {
        if (t != PluginMenuType.Main) return null;

        var root = new ToolStripMenuItem { Text = "Copy Key" };

        var exportItem = new ToolStripMenuItem { Text = "Copy Keys..." };
        exportItem.Click += (_, _) => RunExport();
        root.DropDownItems.Add(exportItem);

        var importItem = new ToolStripMenuItem { Text = "Load Keys..." };
        importItem.Click += (_, _) => RunImport();
        root.DropDownItems.Add(importItem);

        return root;
    }

    // Inserted right after the Quick Find combo box on the main
    // toolbar, if one is found; otherwise appended at the end. The
    // Quick Find box is the only ToolStripComboBox on that toolbar, so
    // it's located by type rather than by (undocumented, version-
    // dependent) control name.
    private void AddToolbarButtons()
    {
        var toolMain = FindMainToolStrip();
        if (toolMain is null) return;

        int insertIndex = toolMain.Items.Count;
        for (int i = 0; i < toolMain.Items.Count; i++)
        {
            if (toolMain.Items[i] is ToolStripComboBox)
            {
                insertIndex = i + 1;
                break;
            }
        }

        _copyIcon = ToolbarIcons.CreateKeyIcon();
        _loadIcon = ToolbarIcons.CreateImportIcon();

        _toolbarSeparator = new ToolStripSeparator();

        _copyButton = new ToolStripButton
        {
            Image = _copyIcon,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "Copy Keys — select entries and export them to an encrypted file",
        };
        _copyButton.Click += (_, _) => RunExport();

        _loadButton = new ToolStripButton
        {
            Image = _loadIcon,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = "Load Keys — import entries from an encrypted keepass-copy-key file",
        };
        _loadButton.Click += (_, _) => RunImport();

        toolMain.Items.Insert(insertIndex, _toolbarSeparator);
        toolMain.Items.Insert(insertIndex + 1, _copyButton);
        toolMain.Items.Insert(insertIndex + 2, _loadButton);
    }

    private void RemoveToolbarButtons()
    {
        var toolMain = FindMainToolStrip();
        if (toolMain is null) return;

        if (_toolbarSeparator is not null) toolMain.Items.Remove(_toolbarSeparator);
        if (_copyButton is not null) toolMain.Items.Remove(_copyButton);
        if (_loadButton is not null) toolMain.Items.Remove(_loadButton);
    }

    private ToolStrip? FindMainToolStrip() =>
        _host?.MainWindow.Controls.Find("m_toolMain", true).FirstOrDefault() as ToolStrip;

    private void RunExport()
    {
        PwDatabase? db = _host!.Database;
        if (db is null || !db.IsOpen)
        {
            Info("Open a database first.", "Copy Keys");
            return;
        }

        using var selectionDialog = new EntrySelectionDialog(db.RootGroup);
        if (selectionDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

        PwEntry[] chosen = selectionDialog.SelectedEntries;
        if (chosen.Length == 0)
        {
            Info("No entries selected.", "Copy Keys");
            return;
        }

        using var passwordDialog = new PasswordDialog(forExport: true);
        if (passwordDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

        string password = string.IsNullOrEmpty(passwordDialog.Password)
            ? KeyFileCrypto.GeneratePassword()
            : passwordDialog.Password;

        using var saveDialog = new SaveFileDialog
        {
            Filter = "KeePass Copy Key file (*.kck)|*.kck",
            FileName = $"keys-{DateTime.Now:yyyyMMdd-HHmmss}.kck",
        };
        if (saveDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

        var records = new KeyEntryRecord[chosen.Length];
        for (int i = 0; i < chosen.Length; i++)
            records[i] = ToRecord(chosen[i]);

        byte[] payload = KeyFile.SerializeEntries(records);
        try
        {
            KeyFile.Write(saveDialog.FileName, payload, password);
        }
        catch (Exception ex)
        {
            Error($"Failed to write the file: {ex.Message}", "Copy Keys");
            return;
        }
        finally
        {
            Array.Clear(payload, 0, payload.Length);
        }

        using var resultDialog = new ExportResultDialog(password, saveDialog.FileName, chosen.Length);
        resultDialog.ShowDialog(_host.MainWindow);
    }

    private void RunImport()
    {
        PwDatabase? db = _host!.Database;
        if (db is null || !db.IsOpen)
        {
            Info("Open a database first.", "Load Keys");
            return;
        }

        using var openDialog = new OpenFileDialog { Filter = "KeePass Copy Key file (*.kck)|*.kck" };
        if (openDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

        using var passwordDialog = new PasswordDialog(forExport: false);
        if (passwordDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

        byte[] payload;
        try
        {
            payload = KeyFile.Read(openDialog.FileName, passwordDialog.Password);
        }
        catch (CryptographicException)
        {
            Error("Wrong password or corrupted file.", "Load Keys");
            return;
        }
        catch (Exception ex)
        {
            Error($"Failed to read the file: {ex.Message}", "Load Keys");
            return;
        }

        KeyEntryRecord[] records;
        try
        {
            records = KeyFile.DeserializeEntries(payload);
        }
        finally
        {
            Array.Clear(payload, 0, payload.Length);
        }

        foreach (var record in records)
            db.RootGroup.AddEntry(ToPwEntry(record), true);

        Info($"Imported {records.Length} key(s). Save the database (Ctrl+S) to keep them.", "Load Keys");
    }

    private static KeyEntryRecord ToRecord(PwEntry entry) => new(
        entry.Strings.ReadSafe(PwDefs.TitleField),
        entry.Strings.ReadSafe(PwDefs.UserNameField),
        entry.Strings.ReadSafe(PwDefs.PasswordField),
        entry.Strings.ReadSafe(PwDefs.UrlField),
        entry.Strings.ReadSafe(PwDefs.NotesField));

    private static PwEntry ToPwEntry(KeyEntryRecord record)
    {
        var entry = new PwEntry(true, true);
        entry.Strings.Set(PwDefs.TitleField, new ProtectedString(false, record.Title));
        entry.Strings.Set(PwDefs.UserNameField, new ProtectedString(false, record.UserName));
        entry.Strings.Set(PwDefs.PasswordField, new ProtectedString(true, record.Password));
        entry.Strings.Set(PwDefs.UrlField, new ProtectedString(false, record.Url));
        entry.Strings.Set(PwDefs.NotesField, new ProtectedString(false, record.Notes));
        return entry;
    }

    private void Info(string message, string title) =>
        MessageBox.Show(_host!.MainWindow, message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);

    private void Error(string message, string title) =>
        MessageBox.Show(_host!.MainWindow, message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
}