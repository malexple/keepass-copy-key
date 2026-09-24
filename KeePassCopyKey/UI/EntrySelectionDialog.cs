// UI-layer only - touches KeePassLib types (PwGroup/PwEntry) directly
// since it mirrors the real group/entry tree of the open database.
// Checking a group cascades the check state down to all its descendants
// (both subgroups and entries); checking/unchecking a single entry does
// not affect its parent group's own check state (no tri-state/
// indeterminate handling - keeping this simple on purpose).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using KeePassLib;

namespace KeePassCopyKey.UI;

internal sealed class EntrySelectionDialog : Form
{
    private const int ButtonHeight = 30;

    private readonly TreeView _tree;
    private bool _suppressCheckEvents;

    public PwEntry[] SelectedEntries { get; private set; } = Array.Empty<PwEntry>();

    public EntrySelectionDialog(PwGroup rootGroup)
    {
        Text = "Copy Keys — select entries";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(480, 430);
        MinimumSize = new Size(380, 320);

        _tree = new TreeView
        {
            Dock = DockStyle.Top,
            Height = 335,
            CheckBoxes = true,
        };
        _tree.AfterCheck += Tree_AfterCheck;

        PopulateGroup(_tree.Nodes, rootGroup);
        _tree.ExpandAll();

        var selectAllButton = new Button
        {
            Text = "Select All",
            Location = new Point(12, 345),
            Size = new Size(100, ButtonHeight),
        };
        selectAllButton.Click += (_, _) => SetAllChecked(true);

        var selectNoneButton = new Button
        {
            Text = "Select None",
            Location = new Point(120, 345),
            Size = new Size(110, ButtonHeight),
        };
        selectNoneButton.Click += (_, _) => SetAllChecked(false);

        var okButton = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(312, 390),
            Size = new Size(80, ButtonHeight),
        };
        okButton.Click += (_, _) => CollectSelection();

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(400, 390),
            Size = new Size(80, ButtonHeight),
        };

        Controls.Add(_tree);
        Controls.Add(selectAllButton);
        Controls.Add(selectNoneButton);
        Controls.Add(okButton);
        Controls.Add(cancelButton);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private static void PopulateGroup(TreeNodeCollection parentNodes, PwGroup group)
    {
        var groupNode = new TreeNode(group.Name) { Tag = group };
        parentNodes.Add(groupNode);

        foreach (var subGroup in group.Groups)
            PopulateGroup(groupNode.Nodes, subGroup);

        foreach (var entry in group.Entries)
        {
            string title = entry.Strings.ReadSafe(PwDefs.TitleField);
            string username = entry.Strings.ReadSafe(PwDefs.UserNameField);
            string label = string.IsNullOrEmpty(username) ? title : $"{title} ({username})";
            groupNode.Nodes.Add(new TreeNode(label) { Tag = entry });
        }
    }

    private void Tree_AfterCheck(object? sender, TreeViewEventArgs e)
    {
        if (_suppressCheckEvents || e.Node is null) return;

        _suppressCheckEvents = true;
        try
        {
            SetChildrenChecked(e.Node, e.Node.Checked);
        }
        finally
        {
            _suppressCheckEvents = false;
        }
    }

    private static void SetChildrenChecked(TreeNode node, bool value)
    {
        foreach (TreeNode child in node.Nodes)
        {
            child.Checked = value;
            SetChildrenChecked(child, value);
        }
    }

    private void SetAllChecked(bool value)
    {
        _suppressCheckEvents = true;
        try
        {
            SetAllCheckedRecursive(_tree.Nodes, value);
        }
        finally
        {
            _suppressCheckEvents = false;
        }
    }

    private static void SetAllCheckedRecursive(TreeNodeCollection nodes, bool value)
    {
        foreach (TreeNode node in nodes)
        {
            node.Checked = value;
            SetAllCheckedRecursive(node.Nodes, value);
        }
    }

    private void CollectSelection()
    {
        var chosen = new List<PwEntry>();
        CollectChecked(_tree.Nodes, chosen);
        SelectedEntries = chosen.ToArray();
    }

    private static void CollectChecked(TreeNodeCollection nodes, List<PwEntry> chosen)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Checked && node.Tag is PwEntry entry)
                chosen.Add(entry);
            CollectChecked(node.Nodes, chosen);
        }
    }
}