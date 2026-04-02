using System;
using System.Windows.Forms;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

namespace BCLoadtester.config;

public class ConnectionSetupForm : Form
{
    private AppConfig _config;

    private TextBox txtServiceRoot;
    private TextBox txtApiRoot;
    private TextBox txtUser;
    private TextBox txtPassword;
    private TextBox txtSqlServer;
    private TextBox txtSqlPort;
    private TextBox txtDatabase;
    private TextBox txtLoadTestTable;

    private TextBox txtRpmPerWorker;
    private TextBox txtMaxWorkersPerType;
    private TextBox txtMaxConnectionsPerServer;

    private TextBox txtMaxConcurrencyPerWorker;

    private ToolTip _toolTip;
    private Label lblRpm;
    private Label lblConcurrency;

    private bool _isDirty = false;

    public ConnectionSetupForm(AppConfig config)
    {
        _config = config;

        Text = "Connection Setup";
        Width = 600;
        Height = 480;
        StartPosition = FormStartPosition.CenterParent;

        this.FormClosing += ConnectionSetupForm_FormClosing;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10)
        };

        _toolTip = new ToolTip
        {
            IsBalloon = true,
            AutoPopDelay = 8000,
            InitialDelay = 300,
            ReshowDelay = 100
        };

        // 🔥 Tooltip auf gesamtes Fenster
        _toolTip.SetToolTip(this, GetGeneralFlowTooltip());

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < 12; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        txtServiceRoot = new TextBox { Text = _config.serviceRoot, Dock = DockStyle.Fill };
        txtApiRoot = new TextBox { Text = _config.apiRoot, Dock = DockStyle.Fill };
        txtUser = new TextBox { Text = _config.username, Dock = DockStyle.Fill };
        txtPassword = new TextBox { Text = _config.password, Dock = DockStyle.Fill, UseSystemPasswordChar = true };

        txtSqlServer = new TextBox { Text = _config.sqlServer, Dock = DockStyle.Fill };
        txtSqlPort = new TextBox { Text = _config.sqlPort.ToString(), Dock = DockStyle.Fill };
        txtDatabase = new TextBox { Text = _config.database, Dock = DockStyle.Fill };
        txtLoadTestTable = new TextBox { Text = _config.loadTestTableName, Dock = DockStyle.Fill };

        txtRpmPerWorker = new TextBox
        {
            Text = (_config.rpmPerWorker == 0 ? 150 : _config.rpmPerWorker).ToString(),
            Dock = DockStyle.Fill
        };

        txtMaxConcurrencyPerWorker = new TextBox
        {
            Text = (_config.maxConcurrencyPerWorker == 0 ? 20 : _config.maxConcurrencyPerWorker).ToString(),
            Dock = DockStyle.Fill
        };

        txtMaxWorkersPerType = new TextBox
        {
            Text = (_config.maxWorkersPerType == 0 ? 600 : _config.maxWorkersPerType).ToString(),
            Dock = DockStyle.Fill
        };

        txtMaxConnectionsPerServer = new TextBox
        {
            Text = (_config.maxConnectionsPerServer == 0 ? 5000 : _config.maxConnectionsPerServer).ToString(),
            Dock = DockStyle.Fill
        };

        // 🔥 Dirty Tracking
        txtServiceRoot.TextChanged += (s, e) => MarkDirty();
        txtApiRoot.TextChanged += (s, e) => MarkDirty();
        txtUser.TextChanged += (s, e) => MarkDirty();
        txtPassword.TextChanged += (s, e) => MarkDirty();
        txtSqlServer.TextChanged += (s, e) => MarkDirty();
        txtSqlPort.TextChanged += (s, e) => MarkDirty();
        txtDatabase.TextChanged += (s, e) => MarkDirty();
        txtLoadTestTable.TextChanged += (s, e) => MarkDirty();

        txtMaxWorkersPerType.TextChanged += (s, e) => MarkDirty();
        txtMaxConnectionsPerServer.TextChanged += (s, e) => MarkDirty();

        txtRpmPerWorker.TextChanged += (s, e) =>
        {
            MarkDirty();
            UpdateRpmTooltip();
        };

        txtMaxConcurrencyPerWorker.TextChanged += (s, e) =>
        {
            MarkDirty();
            UpdateConcurrencyTooltip();
        };

        int row = 0;

        layout.Controls.Add(new Label { Text = "ServiceRoot" }, 0, row);
        layout.Controls.Add(txtServiceRoot, 1, row++);

        layout.Controls.Add(new Label { Text = "ApiRoot" }, 0, row);
        layout.Controls.Add(txtApiRoot, 1, row++);

        layout.Controls.Add(new Label { Text = "Username" }, 0, row);
        layout.Controls.Add(txtUser, 1, row++);

        layout.Controls.Add(new Label { Text = "Password" }, 0, row);
        layout.Controls.Add(txtPassword, 1, row++);

        layout.Controls.Add(new Label { Text = "SQL Server" }, 0, row);
        layout.Controls.Add(txtSqlServer, 1, row++);

        layout.Controls.Add(new Label { Text = "SQL Port" }, 0, row);
        layout.Controls.Add(txtSqlPort, 1, row++);

        layout.Controls.Add(new Label { Text = "Database" }, 0, row);
        layout.Controls.Add(txtDatabase, 1, row++);

        layout.Controls.Add(new Label { Text = "LoadTest Table" }, 0, row);
        layout.Controls.Add(txtLoadTestTable, 1, row++);

        // 🔥 RPM
        lblRpm = new Label { Text = "RPM per Worker" };
        layout.Controls.Add(lblRpm, 0, row);
        layout.Controls.Add(txtRpmPerWorker, 1, row++);

        // 🔥 Concurrency
        lblConcurrency = new Label { Text = "Max Concurrency per Worker" };
        layout.Controls.Add(lblConcurrency, 0, row);
        layout.Controls.Add(txtMaxConcurrencyPerWorker, 1, row++);

        // 🔥 Worker
        var lblWorkers = new Label { Text = "Max Workers per Type" };
        layout.Controls.Add(lblWorkers, 0, row);
        layout.Controls.Add(txtMaxWorkersPerType, 1, row++);
        _toolTip.SetToolTip(lblWorkers, GetWorkersTooltip());
        _toolTip.SetToolTip(txtMaxWorkersPerType, GetWorkersTooltip());

        // 🔥 Connections
        var lblConn = new Label { Text = "Max Connections per Server" };
        layout.Controls.Add(lblConn, 0, row);
        layout.Controls.Add(txtMaxConnectionsPerServer, 1, row++);
        _toolTip.SetToolTip(lblConn, GetConnectionsTooltip());
        _toolTip.SetToolTip(txtMaxConnectionsPerServer, GetConnectionsTooltip());

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(10)
        };

        var btnTestSql = new Button { Text = "Test SQL", Width = 100 };
        var btnTestApi = new Button { Text = "Test API", Width = 100 };

        btnTestSql.Click += async (s, e) => await TestSqlConnection();
        btnTestApi.Click += async (s, e) => await TestApiConnection();

        buttonPanel.Controls.Add(btnTestApi);
        buttonPanel.Controls.Add(btnTestSql);

        Controls.Add(layout);
        Controls.Add(buttonPanel);

        UpdateRpmTooltip();
        UpdateConcurrencyTooltip();
    }

    void MarkDirty()
    {
        _isDirty = true;
        if (!Text.EndsWith("*")) Text += " *";
    }

    private void ConnectionSetupForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!_isDirty) return;

        var result = MessageBox.Show(
            "There are unsaved changes.\n\nDo you want to save them?",
            "Save changes",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question);

        if (result == DialogResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (result == DialogResult.Yes) Save();
    }

    private void Save()
    {
        _config.serviceRoot = txtServiceRoot.Text;
        _config.apiRoot = txtApiRoot.Text;
        _config.username = txtUser.Text;
        _config.password = txtPassword.Text;

        _config.sqlServer = txtSqlServer.Text;
        _config.sqlPort = int.Parse(txtSqlPort.Text);
        _config.database = txtDatabase.Text;
        _config.loadTestTableName = txtLoadTestTable.Text;

        _config.rpmPerWorker = int.Parse(txtRpmPerWorker.Text);
        _config.maxConcurrencyPerWorker = int.Parse(txtMaxConcurrencyPerWorker.Text);
        _config.maxWorkersPerType = int.Parse(txtMaxWorkersPerType.Text);
        _config.maxConnectionsPerServer = int.Parse(txtMaxConnectionsPerServer.Text);

        ConfigLoader.Save(_config);

        _isDirty = false;
        Text = "Connection Setup";
    }

    private async Task TestSqlConnection()
    {
        try
        {
            var connectionString =
                $"Server={txtSqlServer.Text},{txtSqlPort.Text};" +
                $"Database={txtDatabase.Text};" +
                $"User Id={_config.dbUser};" +
                $"Password={_config.dbPassword};" +
                $"TrustServerCertificate=True;" +
                $"Connection Timeout=3;";

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            MessageBox.Show("✅ SQL connection successful", "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ SQL connection failed:\n{ex.Message}", "Error");
        }
    }

    private async Task TestApiConnection()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

            var url = txtServiceRoot.Text.TrimEnd('/') + txtApiRoot.Text;

            var auth = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{txtUser.Text}:{txtPassword.Text}"));

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
                MessageBox.Show("✅ API reachable", "Success");
            else
                MessageBox.Show($"⚠️ API responded: {response.StatusCode}", "Warning");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ API failed:\n{ex.Message}", "Error");
        }
    }

    private void UpdateRpmTooltip()
    {
        if (int.TryParse(txtRpmPerWorker.Text, out int rpm))
        {
            var text = GetRpmBaseTooltip(rpm);
            _toolTip.SetToolTip(txtRpmPerWorker, text);
            _toolTip.SetToolTip(lblRpm, text);
        }
    }

    private void UpdateConcurrencyTooltip()
    {
        if (int.TryParse(txtMaxConcurrencyPerWorker.Text, out int c))
        {
            var text = GetConcurrencyBaseTooltip(c);
            _toolTip.SetToolTip(txtMaxConcurrencyPerWorker, text);
            _toolTip.SetToolTip(lblConcurrency, text);
        }
    }

    // 🔥 TOOLTIP HELPERS
    private string GetRpmBaseTooltip(int rpm)
    {
        var rps = rpm / 60.0;

        return
            $"Requests pro Worker pro Minute\n" +
            $"Aktuell: {rpm} RPM ≈ {rps:F2} req/sec\n\n" +
            "Bestimmt wie oft Requests gestartet werden.\n\n" +
            "Wenn Requests lange dauern, wird dieser Wert nicht erreicht.";
    }

    private string GetConcurrencyBaseTooltip(int c)
    {
        return
            $"Maximale parallele Requests\n\n" +
            $"Aktuell: {c}\n\n" +
            "Begrenzt gleichzeitige Requests.\n\n" +
            "Zu hoch = Überlast\n" +
            "Zu niedrig = zu wenig Last";
    }

    private string GetWorkersTooltip()
    {
        return
            "Max Worker pro Typ\n\n" +
            "Skaliert Last über mehrere Worker.";
    }

    private string GetConnectionsTooltip()
    {
        return
            "Max HTTP-Verbindungen\n\n" +
            "Zu niedrig = künstlicher Flaschenhals";
    }

    private string GetGeneralFlowTooltip()
    {
        return
            "System-Logik:\n\n" +
            "RPM → Frequenz\n" +
            "Concurrency → Parallelität\n" +
            "Worker → Skalierung\n" +
            "Connections → Limit";
    }
}