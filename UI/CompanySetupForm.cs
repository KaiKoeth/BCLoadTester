using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace BCLoadtester.config;

public class CompanySetupForm : Form
{
    private AppConfig _config;
    private DataGridView grid;
    private Button btnSave;

    public CompanySetupForm(AppConfig config)
    {
        _config = config;

        Text = "Company Setup";
        Width = 1400;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;

        // 🔥 SAUBERES LAYOUT
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        // 🔹 GRID
        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        grid.ColumnHeaderMouseClick += Grid_HeaderClick;

        // 🔹 SAVE BUTTON
        btnSave = new Button
        {
            Text = "Save",
            Dock = DockStyle.Fill
        };

        btnSave.Click += (s, e) => SaveChanges();

        // 🔥 Button Panel (unten)
        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10)
        };

        var btnAdd = new Button { Text = "➕ Mandant neu", Width = 150 };
        var btnDelete = new Button { Text = "❌ Mandant löschen", Width = 170 };
        btnSave = new Button { Text = "Save", Width = 100 };

        // Events
        btnAdd.Click += (s, e) => AddCompany();
        btnDelete.Click += (s, e) => DeleteCompany();
        btnSave.Click += (s, e) => SaveChanges();

        bottomPanel.Controls.Add(btnAdd);
        bottomPanel.Controls.Add(btnDelete);
        bottomPanel.Controls.Add(new Label { Width = 20 });
        bottomPanel.Controls.Add(btnSave);

        // Layout
        layout.Controls.Add(grid, 0, 0);
        layout.Controls.Add(bottomPanel, 0, 1);
        Controls.Add(layout);

        BuildColumns();
        LoadCompanies();
    }

    void BuildColumns()
    {
        grid.Columns.Clear();

        var enabledCol = new DataGridViewCheckBoxColumn
        {
            Name = "enabled",
            HeaderText = "Enabled"
        };

        grid.Columns.Add(enabledCol);
        grid.Columns.Add("company", "Company");

        foreach (var worker in _config.workers)
        {
            var col = new DataGridViewTextBoxColumn
            {
                Name = worker.type,
                HeaderText = $"{(worker.enabled ? "☑" : "☐")} {worker.type}",
                SortMode = DataGridViewColumnSortMode.NotSortable
            };

            grid.Columns.Add(col);
        }

        grid.Columns.Add("minLines", "MinLines");
        grid.Columns.Add("maxLines", "MaxLines");
        grid.Columns.Add("weborderPoolSize", "Weborder Pool");

        grid.Columns["company"].ReadOnly = true;
    }

    void LoadCompanies()
    {
        grid.Rows.Clear();

        foreach (var c in _config.companies)
        {
            var values = new List<object>
            {
                c.enabled,
                c.name
            };

            foreach (var worker in _config.workers)
            {
                values.Add(GetRpm(c, worker.type));
            }

            values.Add(c.webOrderConfig?.minLines ?? 0);
            values.Add(c.webOrderConfig?.maxLines ?? 0);
            values.Add(c.webOrderConfig?.WeborderPoolSize ?? 0);

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

            // 🔥 WICHTIG: immer sicherstellen
            if (company.webOrderConfig == null)
                company.webOrderConfig = new WebOrderConfig();

            company.webOrderConfig.minLines =
                Convert.ToInt32(row.Cells[colIndex].Value ?? 0);

            company.webOrderConfig.maxLines =
                Convert.ToInt32(row.Cells[colIndex + 1].Value ?? 0);

            company.webOrderConfig.WeborderPoolSize =
                Convert.ToInt32(row.Cells[colIndex + 2].Value ?? 0);
        }

        ConfigLoader.Save(_config);

        Close();
    }

    void SetRpm(Company company, string worker, object value)
    {
        if (company.rpm == null)
            company.rpm = new Dictionary<string, int>();

        company.rpm[worker] = Convert.ToInt32(value ?? 0);
    }

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

    void AddCompany()
    {
        string name = Prompt("Mandant Name:");
        if (string.IsNullOrWhiteSpace(name))
            return;

        string guid = Prompt("GUID:");
        if (string.IsNullOrWhiteSpace(guid))
            return;

        var company = new Company
        {
            name = name,
            guid = guid,
            enabled = false,
            rpm = new Dictionary<string, int>(),
            webOrderConfig = new WebOrderConfig
            {
                minLines = 1,
                maxLines = 1,
                WeborderPoolSize = 1000
            }
        };

        // 🔥 alle Worker auf 0 setzen
        foreach (var w in _config.workers)
        {
            company.rpm[w.type] = 0;
        }

        _config.companies.Add(company);

        LoadCompanies();
    }

    void DeleteCompany()
    {
        if (grid.CurrentRow == null)
            return;

        int index = grid.CurrentRow.Index;

        if (index < 0 || index >= _config.companies.Count)
            return;

        var company = _config.companies[index];

        var result = MessageBox.Show(
            $"Mandant '{company.name}' wirklich löschen?",
            "Bestätigen",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result != DialogResult.Yes)
            return;

        _config.companies.RemoveAt(index);

        LoadCompanies();
    }

    string Prompt(string text)
    {
        var form = new Form
        {
            Width = 400,
            Height = 150,
            Text = text,
            StartPosition = FormStartPosition.CenterParent
        };

        var input = new TextBox { Dock = DockStyle.Top };

        var ok = new Button
        {
            Text = "OK",
            Dock = DockStyle.Bottom,
            DialogResult = DialogResult.OK
        };

        form.Controls.Add(input);
        form.Controls.Add(ok);

        form.AcceptButton = ok;

        return form.ShowDialog() == DialogResult.OK ? input.Text : "";
    }
}