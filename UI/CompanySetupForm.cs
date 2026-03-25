using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;

namespace BCLoadtester.config;

public class CompanySetupForm : Form
{
    private AppConfig _config;
    private DataGridView grid;
    private bool _isDirty = false;

    public CompanySetupForm(AppConfig config)
    {
        _config = config;

        Text = "Company Setup";
        Width = 1400;
        Height = 700;
        StartPosition = FormStartPosition.CenterParent;

        this.FormClosing += CompanySetupForm_FormClosing;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

        // =========================
        // 🔹 GRID
        // =========================
        grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        grid.CellValueChanged += (s, e) => MarkDirty();

        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        grid.ColumnHeaderMouseClick += Grid_HeaderClick;
        grid.CellDoubleClick += Grid_CellDoubleClick;

        // 🔥 Tooltip + Cursor
        grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
        grid.CellMouseEnter += Grid_CellMouseEnter;
        grid.CellMouseLeave += (s, e) => grid.Cursor = Cursors.Default;

        // =========================
        // 🔹 BUTTONS
        // =========================
        var bottomPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10)
        };

        var btnAdd = new Button { Text = "➕ Mandant neu", Width = 150 };
        var btnDelete = new Button { Text = "❌ Mandant löschen", Width = 170 };

        btnAdd.Click += (s, e) => AddCompany();
        btnDelete.Click += (s, e) => DeleteCompany();

        bottomPanel.Controls.Add(btnAdd);
        bottomPanel.Controls.Add(btnDelete);

        layout.Controls.Add(grid, 0, 0);
        layout.Controls.Add(bottomPanel, 0, 1);

        Controls.Add(layout);

        BuildColumns();
        LoadCompanies();
    }

    // =========================
    // 🔥 DIRTY
    // =========================
    void MarkDirty()
    {
        _isDirty = true;

        if (!Text.EndsWith("*"))
            Text += " *";
    }

    private void CompanySetupForm_FormClosing(object? sender, FormClosingEventArgs e)
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
            SaveChanges();
        }
    }

    // =========================
    // 🔧 COLUMNS
    // =========================
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
                SortMode = DataGridViewColumnSortMode.NotSortable
            };


            col.HeaderText = $"{(worker.enabled ? "☑" : "☐")} {worker.type}";

            grid.Columns.Add(col);
        }

        grid.Columns["company"].ReadOnly = true;
    }

    // =========================
    // 🔧 LOAD
    // =========================
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
                if (worker.type == "WebOrderCreate" && c.webOrderConfig != null)
                    values.Add($"{GetRpm(c, worker.type)} ⚙");
                else
                    values.Add(GetRpm(c, worker.type));
            }

            grid.Rows.Add(values.ToArray());
        }
    }

    int GetRpm(Company company, string worker)
    {
        if (company.rpm != null && company.rpm.ContainsKey(worker))
            return company.rpm[worker];

        return 0;
    }

    // =========================
    // 🔧 SAVE
    // =========================
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
                var value = row.Cells[colIndex].Value?.ToString() ?? "0";
                value = value.Replace("⚙", "").Trim();

                SetRpm(company, worker.type, value);
                colIndex++;
            }

            if (company.webOrderConfig == null)
                company.webOrderConfig = new WebOrderConfig();
        }

        ConfigLoader.Save(_config);

        _isDirty = false;
        Text = "Company Setup";
    }

    void SetRpm(Company company, string worker, object value)
    {
        if (company.rpm == null)
            company.rpm = new Dictionary<string, int>();

        company.rpm[worker] = Convert.ToInt32(value ?? 0);
    }

    // =========================
    // 🔧 HEADER CLICK
    // =========================
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


            MarkDirty();
        }
    }

    // =========================
    // 🔥 DRILLDOWN
    // =========================
    void Grid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var column = grid.Columns[e.ColumnIndex];

        if (column.Name != "WebOrderCreate")
            return;

        var company = _config.companies[e.RowIndex];

        if (company.webOrderConfig == null)
            company.webOrderConfig = new WebOrderConfig();

        using var form = new WebOrderConfigForm(company.webOrderConfig);
        form.ShowDialog(this);

        MarkDirty();
    }

    // =========================
    // 🔥 TOOLTIP
    // =========================
    void Grid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name != "WebOrderCreate")
            return;

        var company = _config.companies[e.RowIndex];
        var cfg = company.webOrderConfig;

        if (cfg == null)
        {
            e.ToolTipText = "No WebOrder config\nDouble click to edit";
            return;
        }

        e.ToolTipText =
            $"⚙ WebOrder Settings\n" +
            $"Lines: {cfg.minLines}-{cfg.maxLines}\n" +
            $"BigOrder: {cfg.bigOrderLines} / {cfg.bigOrderIntervalMinutes} min\n\n" +
            $"Double click to edit";
    }

    // =========================
    // 🔥 CURSOR
    // =========================
    void Grid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name == "WebOrderCreate")
            grid.Cursor = Cursors.Hand;
    }

    // =========================
    // 🔧 ADD / DELETE
    // =========================
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
                bigOrderLines = 0,
                bigOrderIntervalMinutes = 0
            }
        };

        foreach (var w in _config.workers)
        {
            company.rpm[w.type] = 0;
        }

        _config.companies.Add(company);

        LoadCompanies();
        MarkDirty();
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
        MarkDirty();
    }

    // =========================
    // 🔧 PROMPT
    // =========================
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