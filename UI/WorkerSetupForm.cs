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
        Width = 1100;
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

        // 🔥 NEW: Concurrency Column
        var concurrencyCol = new DataGridViewTextBoxColumn
        {
            Name = "maxConcurrency",
            HeaderText = "Max Concurrency"
        };
        grid.Columns.Add(concurrencyCol);

        // 🔥 Buffer Factor
        var bufferCol = new DataGridViewTextBoxColumn
        {
            Name = "buffer",
            HeaderText = "Buffer Factor",
        };
        grid.Columns.Add(bufferCol);

        grid.Columns["type"].ReadOnly = true;

        // 🔥 TOOLTIP (Header)
        grid.Columns["maxConcurrency"].HeaderCell.ToolTipText =
            "Maximale parallele Requests pro Worker\n\n" +
            "Leer = globaler Wert wird verwendet\n" +
            "Hoch = mehr Last / mehr Druck auf Backend\n" +
            "Niedrig = stabiler aber weniger Durchsatz\n\n" +
            "Typisch:\n" +
            "Search: 10–20\n" +
            "Order: 20–50\n" +
            "Posting: 5–10";

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
                w.maxConcurrency?.ToString() ?? "", // 🔥 leer = global
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

            // 🔥 Concurrency (optional!)
            var concText = row.Cells["maxConcurrency"].Value?.ToString();

            if (string.IsNullOrWhiteSpace(concText))
            {
                worker.maxConcurrency = null; // 🔥 global fallback
            }
            else if (int.TryParse(concText, out int c))
            {
                worker.maxConcurrency = Math.Max(1, c);
            }
            else
            {
                worker.maxConcurrency = null;
            }

            // 🔥 Buffer sauber parsen
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