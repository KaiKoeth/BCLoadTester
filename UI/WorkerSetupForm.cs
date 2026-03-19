using System;
using System.Linq;
using System.Windows.Forms;

public class WorkerSetupForm : Form
{
    private AppConfig _config;
    private DataGridView grid;
    private Button btnSave;

    public WorkerSetupForm(AppConfig config)
    {
        _config = config;

        Text = "Worker Setup";
        Width = 900;
        Height = 500;
        StartPosition = FormStartPosition.CenterParent;

        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AllowUserToAddRows = false
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

        btnSave = new Button
        {
            Text = "Save",
            Dock = DockStyle.Bottom,
            Height = 40
        };

        btnSave.Click += (s, e) => Save();

        Controls.Add(grid);
        Controls.Add(btnSave);
    }

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

        Close();
    }
}