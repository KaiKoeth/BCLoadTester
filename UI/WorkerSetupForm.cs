using System;
using System.Linq;
using System.Windows.Forms;

public class WorkerSetupForm : Form
{
    private AppConfig _config;
    private DataGridView grid;
    private bool _isDirty = false;

    public WorkerSetupForm(AppConfig config)
    {
        _config = config;

        Text = "Worker Setup";
        Width = 900;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;

        // 🔥 FormClosing Hook
        this.FormClosing += WorkerSetupForm_FormClosing;

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AllowUserToAddRows = false
        };

        // 🔥 Dirty Tracking (wichtig für Checkbox + Text)
        grid.CellValueChanged += (s, e) => MarkDirty();
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        // Columns
        grid.Columns.Add("type", "Worker");
        grid.Columns.Add("endpoint", "Endpoint");

        var enabledCol = new DataGridViewCheckBoxColumn
        {
            Name = "enabled",
            HeaderText = "Enabled"
        };
        grid.Columns.Add(enabledCol);

        grid.Columns["type"].ReadOnly = true;

        LoadWorkers();

        Controls.Add(grid);
    }

    // =========================
    // 🔥 DIRTY HANDLING
    // =========================
    void MarkDirty()
    {
        _isDirty = true;

        if (!Text.EndsWith("*"))
            Text += " *";
    }

    private void WorkerSetupForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isDirty)
            return;

        var result = MessageBox.Show(
            "There are unsaved changes.\n\nDo you want to save them?",
            "Save changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question
        );

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes)
        {
            Save();
        }
        // No = schließen ohne speichern
    }

    // =========================
    // 🔧 LOAD
    // =========================
    void LoadWorkers()
    {
        grid.Rows.Clear();

        foreach (var w in _config.workers)
        {
            grid.Rows.Add(
                w.type,
                w.endpoint,
                w.enabled
            );
        }
    }

    // =========================
    // 🔧 SAVE (intern)
    // =========================
    void Save()
    {
        grid.EndEdit();

        for (int i = 0; i < grid.Rows.Count; i++)
        {
            var row = grid.Rows[i];
            var worker = _config.workers[i];

            worker.endpoint = row.Cells["endpoint"].Value?.ToString() ?? "";
            worker.enabled = Convert.ToBoolean(row.Cells["enabled"].Value);
        }

        ConfigLoader.Save(_config);

        _isDirty = false;
        Text = "Worker Setup";
    }
}