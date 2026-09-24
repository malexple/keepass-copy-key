// Plugin entry point. Thin: no logging, no protocol logic here - that
// lives in Crypto/IO. This file's job beyond Plugin lifecycle is (a) the
// PwEntry <-> KeyEntryRecord mapping and (b) wiring up both the Tools
// menu and the main toolbar buttons.
//
// User-facing wording is "Export Keys" / "Import Keys", not "Copy" /
// "Load" - see README for why. Internal field/method names below match
// that same terminology now (Export.../Import...) rather than mixing
// vocabularies.
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
//
// Password is always mandatory now (see IO/KeyFile.cs) - PasswordDialog
// itself blocks OK on an empty field, so by the time ShowDialog returns
// OK here, Password is guaranteed non-empty. No post-save "here's your
// password" dialog either: the (editable, pre-filled) password is shown
// *before* the file is written, so repeating it afterward was redundant.
//
// UI refresh after import: KeePass does NOT automatically refresh the
// group tree / entry list after a plugin modifies the database
// in-memory (PwGroup.AddEntry etc.) - this is deliberate, for
// performance, per the KeePass author's own forum guidance. The fix is
// to call MainForm.UpdateUI explicitly; the exact parameter order below
// is confirmed from other real KeePass plugins' source
// (bRecreateTabBar, dsSelect, bUpdateGroupList, pgSelect,
// bUpdateEntryList, pgEntrySource, bSetModified). Passing db.RootGroup
// as both pgSelect and pgEntrySource selects the Root group in the tree
// AND shows its entry list.

using System;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using KeePass.Plugins;
using KeePassCopyKey.IO;
using KeePassCopyKey.UI;
using KeePassLib;
using KeePassLib.Security;

namespace KeePassCopyKey;

public sealed class KeePassCopyKeyExt : Plugin
{
    private IPluginHost? _host;

    private ToolStripSeparator? _toolbarSeparator;
    private ToolStripButton? _exportButton;
    private ToolStripButton? _importButton;
    private Bitmap? _exportIcon;
    private Bitmap? _importIcon;

    private static bool IsRussianUi =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("ru", StringComparison.OrdinalIgnoreCase);

    public override bool Initialize(IPluginHost host)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        AddToolbarButtons();
        return true;
    }

    public override void Terminate()
    {
        RemoveToolbarButtons();
        _exportIcon?.Dispose();
        _importIcon?.Dispose();
    }

    public override ToolStripMenuItem? GetMenuItem(PluginMenuType t)
    {
        if (t != PluginMenuType.Main) return null;

        var root = new ToolStripMenuItem { Text = "Copy Key" };

        var exportItem = new ToolStripMenuItem { Text = "Export Keys..." };
        exportItem.Click += (_, _) => RunExport();
        root.DropDownItems.Add(exportItem);

        var importItem = new ToolStripMenuItem { Text = "Import Keys..." };
        importItem.Click += (_, _) => RunImport();
        root.DropDownItems.Add(importItem);

        return root;
    }

    // Inserted right after the Quick Find combo box on the main
    // toolbar, if one is found; otherwise appended at the end.
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

        _exportIcon = ToolbarIcons.CreateExportIcon();
        _importIcon = ToolbarIcons.CreateImportIcon();

        _toolbarSeparator = new ToolStripSeparator();

        _exportButton = new ToolStripButton
        {
            Image = _exportIcon,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = IsRussianUi ? "Экспортировать ключи" : "Export Keys",
        };
        _exportButton.Click += (_, _) => RunExport();

        _importButton = new ToolStripButton
        {
            Image = _importIcon,
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            ToolTipText = IsRussianUi ? "Импортировать ключи" : "Import Keys",
        };
        _importButton.Click += (_, _) => RunImport();

        toolMain.Items.Insert(insertIndex, _toolbarSeparator);
        toolMain.Items.Insert(insertIndex + 1, _exportButton);
        toolMain.Items.Insert(insertIndex + 2, _importButton);
    }

    private void RemoveToolbarButtons()
    {
        var toolMain = FindMainToolStrip();
        if (toolMain is null) return;

        if (_toolbarSeparator is not null) toolMain.Items.Remove(_toolbarSeparator);
        if (_exportButton is not null) toolMain.Items.Remove(_exportButton);
        if (_importButton is not null) toolMain.Items.Remove(_importButton);
    }

    private ToolStrip? FindMainToolStrip() =>
        _host?.MainWindow.Controls.Find("m_toolMain", true).FirstOrDefault() as ToolStrip;

    private void RunExport()
    {
        PwDatabase? db = _host!.Database;
        if (db is null || !db.IsOpen)
        {
            Info(IsRussianUi ? "Сначала откройте базу." : "Open a database first.", "Export Keys");
            return;
        }

        using var selectionDialog = new EntrySelectionDialog(db.RootGroup);
        if (selectionDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

        PwEntry[] chosen = selectionDialog.SelectedEntries;
        if (chosen.Length == 0)
        {
            Info(IsRussianUi ? "Ничего не выбрано." : "No entries selected.", "Export Keys");
            return;
        }

        // PasswordDialog blocks OK on an empty field, so Password here
        // is guaranteed non-empty.
        using var passwordDialog = new PasswordDialog(forExport: true);
        if (passwordDialog.ShowDialog(_host.MainWindow) != DialogResult.OK) return;

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
            KeyFile.Write(saveDialog.FileName, payload, passwordDialog.Password);
        }
        catch (Exception ex)
        {
            Error($"Failed to write the file: {ex.Message}", "Export Keys");
            return;
        }
        finally
        {
            Array.Clear(payload, 0, payload.Length);
        }

        Info(
            IsRussianUi
                ? $"Сохранено ключей: {chosen.Length}\n{saveDialog.FileName}"
                : $"Saved {chosen.Length} key(s) to:\n{saveDialog.FileName}",
            "Export Keys");
    }

    private void RunImport()
    {
        PwDatabase? db = _host!.Database;
        if (db is null || !db.IsOpen)
        {
            Info(IsRussianUi ? "Сначала откройте базу." : "Open a database first.", "Import Keys");
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
            Error(IsRussianUi ? "Неверный пароль или повреждённый файл." : "Wrong password or corrupted file.", "Import Keys");
            return;
        }
        catch (Exception ex)
        {
            Error($"Failed to read the file: {ex.Message}", "Import Keys");
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

        _host.MainWindow.UpdateUI(false, null, true, db.RootGroup, true, db.RootGroup, true);

        Info(
            IsRussianUi
                ? $"Импортировано ключей: {records.Length}. Нажмите Ctrl+S, чтобы сохранить базу."
                : $"Imported {records.Length} key(s). Save the database (Ctrl+S) to keep them.",
            "Import Keys");
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