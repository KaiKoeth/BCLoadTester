using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.Net.Http;
using System.Threading.Tasks;

namespace BCLoadtester.config;

public class CompanySetupForm : Form
{
    private AppConfig _config;
    private DataGridView grid;
    private bool _isDirty = false;
    private bool _suppressDirty = false;

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
        grid.CellClick += Grid_CellClick;

        grid.CellToolTipTextNeeded += Grid_CellToolTipTextNeeded;
        grid.CellMouseEnter += Grid_CellMouseEnter;
        grid.CellMouseLeave += (s, e) => grid.Cursor = Cursors.Default;

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

    void MarkDirty()
    {
        if (_suppressDirty)
            return;

        _isDirty = true;

        if (!Text.EndsWith("*"))
            Text += " *";
    }

    private void CompanySetupForm_FormClosing(object? sender, FormClosingEventArgs e)
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
            SaveChanges();
        }

        this.DialogResult = DialogResult.OK;
    }

    void BuildColumns()
    {
        grid.Columns.Clear();

        // 🔹 Basis-Spalten
        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            Name = "enabled",
            HeaderText = "Enabled"
        });

        grid.Columns.Add("company", "Company");
        grid.Columns.Add("serviceRoot", "Service Root");
        grid.Columns.Add("apiRoot", "API Root");

        // 🔹 API Test
        var apiTestCol = new DataGridViewButtonColumn
        {
            Name = "apiTest",
            HeaderText = "API",
            Text = "Test",
            UseColumnTextForButtonValue = true
        };
        grid.Columns.Add(apiTestCol);

        // 🔹 Worker + ggf. WebOrder Config daneben
        foreach (var worker in _config.workers)
        {
            // Worker-Spalte
            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = worker.type,
                HeaderText = $"{(worker.enabled ? "☑" : "☐")} {worker.type}",
                SortMode = DataGridViewColumnSortMode.NotSortable
            });

            // 🔥 Direkt nach WebOrderCreate: Config-Spalte
            if (worker.type.Equals("WebOrderCreate", StringComparison.OrdinalIgnoreCase))
            {
                var webOrderConfigCol = new DataGridViewButtonColumn
                {
                    Name = "webOrderConfig",
                    HeaderText = "WebOrder Config",
                    Text = "⚙",
                    UseColumnTextForButtonValue = true,
                    Width = 120,
                    FlatStyle = FlatStyle.Flat
                };

                grid.Columns.Add(webOrderConfigCol);
            }
        }

        // 🔹 ReadOnly Einstellungen
        grid.Columns["company"].ReadOnly = true;
        grid.Columns["apiTest"].ReadOnly = true;

        if (grid.Columns.Contains("webOrderConfig"))
            grid.Columns["webOrderConfig"].ReadOnly = true;
    }

    void LoadCompanies()
    {
        grid.Rows.Clear();

        foreach (var c in _config.companies)
        {
            var values = new List<object>
        {
            c.enabled,
            c.name,
            c.serviceRoot ?? "",
            c.apiRoot ?? "",
            "Test"
        };

            foreach (var worker in _config.workers)
            {
                // 👉 Worker-Wert
                values.Add(GetRpm(c, worker.type));

                // 👉 Wenn WebOrderCreate → direkt danach ⚙ einfügen
                if (worker.type.Equals("WebOrderCreate", StringComparison.OrdinalIgnoreCase))
                {
                    values.Add("⚙");
                }
            }

            grid.Rows.Add(values.ToArray());
        }
    }

    int GetRpm(Company company, string worker)
    {
        return company.rpm != null && company.rpm.ContainsKey(worker)
            ? company.rpm[worker]
            : 0;
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
            company.serviceRoot = row.Cells["serviceRoot"].Value?.ToString();
            company.apiRoot = row.Cells["apiRoot"].Value?.ToString();

            int colIndex = 5;

            foreach (var worker in _config.workers)
            {
                var value = row.Cells[colIndex].Value?.ToString() ?? "0";
                SetRpm(company, worker.type, value);
                colIndex++;

                // 🔥 WICHTIG: ⚙-Spalte überspringen
                if (worker.type.Equals("WebOrderCreate", StringComparison.OrdinalIgnoreCase))
                {
                    colIndex++; // skip config column
                }
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

    void Grid_HeaderClick(object sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.RowIndex != -1)
            return;

        int workerStartCol = 5;

        if (e.ColumnIndex >= workerStartCol)
        {
            int workerIndex = e.ColumnIndex - workerStartCol;
            var worker = _config.workers[workerIndex];

            worker.enabled = !worker.enabled;

            grid.Columns[e.ColumnIndex].HeaderText =
                $"{(worker.enabled ? "☑" : "☐")} {worker.type}";

            MarkDirty();
        }
    }


    void Grid_CellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var column = grid.Columns[e.ColumnIndex].Name;
        var company = _config.companies[e.RowIndex];

        // 🔥 API Tooltip
        if (column == "apiTest")
        {
            var serviceRoot = string.IsNullOrWhiteSpace(company.serviceRoot)
                ? _config.serviceRoot
                : company.serviceRoot;

            var apiRoot = string.IsNullOrWhiteSpace(company.apiRoot)
                ? _config.apiRoot
                : company.apiRoot;

            e.ToolTipText = $"{serviceRoot}{apiRoot}";
            return;
        }

        // 🔥 WebOrder Tooltip
        if (column == "webOrderConfig")
        {
            var cfg = company.webOrderConfig;

            if (cfg == null)
            {
                e.ToolTipText = "WebOrder konfigurieren";
                return;
            }

            e.ToolTipText =
                $"⚙ WebOrder Settings\n" +
                $"Lines: {cfg.minLines}-{cfg.maxLines}\n" +
                $"BigOrder: {cfg.bigOrderLines} / {cfg.bigOrderIntervalMinutes} min";

            return;
        }
    }

    void Grid_CellMouseEnter(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        if (grid.Columns[e.ColumnIndex].Name == "webOrderConfig")
            grid.Cursor = Cursors.Hand;
    }

    void AddCompany()
    {
        var company = new Company
        {
            name = Prompt("Mandant Name:"),
            guid = Prompt("GUID:"),
            enabled = false,
            rpm = new Dictionary<string, int>(),
            webOrderConfig = new WebOrderConfig()
        };

        foreach (var w in _config.workers)
            company.rpm[w.type] = 0;

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

        _config.companies.RemoveAt(index);

        LoadCompanies();
        MarkDirty();
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
        var ok = new Button { Text = "OK", Dock = DockStyle.Bottom, DialogResult = DialogResult.OK };

        form.Controls.Add(input);
        form.Controls.Add(ok);
        form.AcceptButton = ok;

        return form.ShowDialog() == DialogResult.OK ? input.Text : "";
    }


    private async Task TestApiForCompany(Company company, int rowIndex)
    {
        var cell = grid.Rows[rowIndex].Cells["apiTest"];

        try
        {
            _suppressDirty = true; // 🔥 verhindert Dirty-Flag

            cell.Value = "...";
            cell.Style.BackColor = Color.LightGray;

            var row = grid.Rows[rowIndex];

            var serviceRootInput = row.Cells["serviceRoot"].Value?.ToString();
            var apiRootInput = row.Cells["apiRoot"].Value?.ToString();

            var hasCompanyService = !string.IsNullOrWhiteSpace(serviceRootInput);
            var hasCompanyApi = !string.IsNullOrWhiteSpace(apiRootInput);

            // ❌ Halb gepflegt → Fehler
            if ((hasCompanyService && !hasCompanyApi) || (!hasCompanyService && hasCompanyApi))
            {
                cell.Value = "CONFIG!";
                cell.Style.BackColor = Color.Orange;

                MessageBox.Show(
                    $"API Setup unvollständig bei {company.name}\n\nBitte entweder beide Felder pflegen oder leer lassen.",
                    "Konfigurationsfehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return;
            }

            // ⚠️ Keine Company API → Hinweis (kein Test!)
            if (!hasCompanyService && !hasCompanyApi)
            {
                cell.Value = "GLOBAL";
                cell.Style.BackColor = Color.LightBlue;

                MessageBox.Show(
                    $"Für {company.name} ist keine eigene API konfiguriert.\n\nBitte globale API Konfiguration testen.",
                    "Hinweis",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                return;
            }

            // ✅ Company API testen
            var url = serviceRootInput!.TrimEnd('/') + apiRootInput!;

            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var auth = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{_config.username}:{_config.password}")
            );

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                cell.Value = "OK";
                cell.Style.BackColor = Color.LightGreen;

                MessageBox.Show(
                    $"API OK\n\nCompany: {company.name}\nURL: {url}",
                    "API Test erfolgreich",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                cell.Value = $"ERR {(int)response.StatusCode}";
                cell.Style.BackColor = Color.IndianRed;

                MessageBox.Show(
                    $"API Fehler {(int)response.StatusCode}\n\nCompany: {company.name}\nURL: {url}",
                    "API Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            cell.Value = "FAIL";
            cell.Style.BackColor = Color.DarkRed;

            MessageBox.Show(
                $"API Test fehlgeschlagen\n\nCompany: {company.name}\n\n{ex.Message}",
                "API Fehler",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            _suppressDirty = false; // 🔥 immer zurücksetzen
        }
    }
    private async void Grid_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0)
            return;

        var columnName = grid.Columns[e.ColumnIndex].Name;

        // 🔥 API Test (bestehend)
        if (columnName == "apiTest")
        {
            var company = _config.companies[e.RowIndex];
            await TestApiForCompany(company, e.RowIndex);
            return;
        }

        // 🔥 NEU: WebOrder Drilldown per Click
        if (columnName == "webOrderConfig")
        {
            OpenWebOrderConfig(e.RowIndex);
            return;
        }
    }

    void OpenWebOrderConfig(int rowIndex)
    {
        var company = _config.companies[rowIndex];

        if (company.webOrderConfig == null)
            company.webOrderConfig = new WebOrderConfig();

        using var form = new WebOrderConfigForm(company.webOrderConfig);

        if (form.ShowDialog(this) == DialogResult.OK)
        {
            MarkDirty(); // 🔥 nur wenn wirklich geändert
        }
    }

}