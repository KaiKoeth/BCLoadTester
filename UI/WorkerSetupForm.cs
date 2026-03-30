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
        Width = 1000;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;

        this.FormClosing += WorkerSetupForm_FormClosing;

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AllowUserToAddRows = false
        };

        // 🔥 Dirty Tracking
        grid.CellValueChanged += (s, e) => MarkDirty();
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        // =========================
        // 🔷 COLUMNS
        // =========================

        grid.Columns.Add("type", "Worker");
        grid.Columns.Add("endpoint", "Endpoint");

        var enabledCol = new DataGridViewCheckBoxColumn
        {
            Name = "enabled",
            HeaderText = "Enabled"
        };
        grid.Columns.Add(enabledCol);

        // 🔥 Buffer Factor
        var bufferCol = new DataGridViewTextBoxColumn
        {
            Name = "buffer",
            HeaderText = "Buffer Factor",
        };
        grid.Columns.Add(bufferCol);

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
        // 🔥 KEINE Änderungen → kein Reload
        if (!_isDirty)
        {
            this.DialogResult = DialogResult.Cancel;
            return;
        }

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

        // 🔥 WICHTIG: Änderungen vorhanden → OK zurückgeben
        this.DialogResult = DialogResult.OK;
    }

    // =========================
    // 🔧 LOAD
    // =========================
    void LoadWorkers()
    {
        grid.Rows.Clear();

        foreach (var w in _config.workers)
        {
            var buffer = w.bufferFactor <= 0 ? 1.2 : w.bufferFactor;

            grid.Rows.Add(
                w.type,
                w.endpoint,
                w.enabled,
                buffer.ToString("0.00")
            );
        }
    }

    // =========================
    // 🔧 SAVE
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

            // 🔥 Buffer sauber parsen + validieren
            var bufferText = row.Cells["buffer"].Value?.ToString();

            if (double.TryParse(bufferText, out double buffer))
            {
                worker.bufferFactor = Math.Max(1.0, buffer);
            }
            else
            {
                worker.bufferFactor = 1.2;
            }
        }

        ConfigLoader.Save(_config);

        _isDirty = false;
        Text = "Worker Setup";
    }
}