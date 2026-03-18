using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

public class SetupForm : Form
{
    private AppConfig _config;
    private DataGridView grid;
    private Button btnSave;

    void CalculateWindowSize()
    {
        int columnCount = 2 + _config.workers.Count + 2;

        int width = columnCount * 140;

        int rowCount = _config.companies.Count;

        int height = 120 + (rowCount * 35);

        Width = Math.Max(900, width);
        Height = Math.Max(300, height);
    }

    public SetupForm(AppConfig config)
    {
        

        Text = "Loadtest Setup";
        this.Size = new Size(1800, 600);
        this.StartPosition = FormStartPosition.CenterParent;
        _config = config;

        grid = new DataGridView();
        grid.Dock = DockStyle.Fill;
        
        grid.AllowUserToAddRows = false;
        grid.RowHeadersVisible = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

        grid.ColumnHeaderMouseClick += Grid_HeaderClick;

        BuildColumns();

        Controls.Add(grid);

        btnSave = new Button();
        btnSave.Text = "Save";
        btnSave.Dock = DockStyle.Bottom;        
        btnSave.Height = 40;
        btnSave.Click += (s, e) => SaveChanges();

        Controls.Add(btnSave);

        LoadCompanies();
    }

    void BuildColumns()
    {
        grid.Columns.Clear();

        // Company Enabled
        var enabledCol = new DataGridViewCheckBoxColumn();
        enabledCol.Name = "enabled";
        enabledCol.HeaderText = "Enabled";
        grid.Columns.Add(enabledCol);

        grid.Columns.Add("company", "Company");

        foreach (var worker in _config.workers)
        {
            var col = new DataGridViewTextBoxColumn();

            col.Name = worker.type;
            col.HeaderText = $"{(worker.enabled ? "☑" : "☐")} {worker.type}";
            col.SortMode = DataGridViewColumnSortMode.NotSortable;

            grid.Columns.Add(col);
        }
        grid.Columns.Add("minLines", "MinLines");
        grid.Columns.Add("maxLines", "MaxLines");

        grid.Columns["company"].ReadOnly = true;
    }

    void LoadCompanies()
    {
        grid.Rows.Clear();

        foreach (var c in _config.companies)
        {
            var values = new List<object>();

            values.Add(c.enabled);
            values.Add(c.name);

            foreach (var worker in _config.workers)
            {
                values.Add(GetRpm(c, worker.type));
            }

            values.Add(c.webOrderConfig?.minLines ?? 0);
            values.Add(c.webOrderConfig?.maxLines ?? 0);

            grid.Rows.Add(values.ToArray());
        }
    }

    int GetRpm(Company company, string worker)
    {
        if (company.rpm != null && company.rpm.ContainsKey(worker))
            return company.rpm[worker];

        return 0;
    }

    void SaveChanges()
    {
        grid.EndEdit();
        this.Validate();
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            var row = grid.Rows[i];
            var company = _config.companies[i];

            company.enabled = Convert.ToBoolean(row.Cells[0].Value);

            int colIndex = 2;

            foreach (var worker in _config.workers)
            {
                SetRpm(company, worker.type, row.Cells[colIndex].Value);
                colIndex++;
            }

            if (company.webOrderConfig != null)
            {
                company.webOrderConfig.minLines =
                    Convert.ToInt32(row.Cells[colIndex].Value ?? 0);

                company.webOrderConfig.maxLines =
                    Convert.ToInt32(row.Cells[colIndex + 1].Value ?? 0);
            }
        }

        ConfigLoader.Save(_config);

        Close();   // ← Fenster schließen
    }

    void SetRpm(Company company, string worker, object value)
    {
        if (company.rpm == null)
            company.rpm = new Dictionary<string, int>();

        company.rpm[worker] = Convert.ToInt32(value ?? 0);
    }

    // Header Klick → Worker Enabled toggeln
    void Grid_HeaderClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex != -1)
            return;

        int workerStartCol = 2;
        int workerEndCol = workerStartCol + _config.workers.Count - 1;

        if (e.ColumnIndex >= workerStartCol && e.ColumnIndex <= workerEndCol)
        {
            int workerIndex = e.ColumnIndex - workerStartCol;
            var worker = _config.workers[workerIndex];

            worker.enabled = !worker.enabled;

            grid.Columns[e.ColumnIndex].HeaderText =
                $"{(worker.enabled ? "☑" : "☐")} {worker.type}";
        }
    }

    void UpdateWorkerHeader(int columnIndex, WorkerConfig worker)
    {
        string header = (worker.enabled ? "☑ " : "☐ ") + worker.type + " RPM";
        grid.Columns[columnIndex].HeaderText = header;
    }
}