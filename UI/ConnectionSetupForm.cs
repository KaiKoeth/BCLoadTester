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

    private bool _isDirty = false;

    public ConnectionSetupForm(AppConfig config)
    {
        _config = config;

        Text = "Connection Setup";
        Width = 600;
        Height = 350;
        StartPosition = FormStartPosition.CenterParent;

        // 🔥 FormClosing Hook
        this.FormClosing += ConnectionSetupForm_FormClosing;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < 8; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));

        txtServiceRoot = new TextBox { Text = _config.serviceRoot, Dock = DockStyle.Fill };
        txtApiRoot = new TextBox { Text = _config.apiRoot, Dock = DockStyle.Fill };
        txtUser = new TextBox { Text = _config.username, Dock = DockStyle.Fill };
        txtPassword = new TextBox { Text = _config.password, Dock = DockStyle.Fill, UseSystemPasswordChar = true };

        txtSqlServer = new TextBox { Text = _config.sqlServer, Dock = DockStyle.Fill };
        txtSqlPort = new TextBox { Text = _config.sqlPort.ToString(), Dock = DockStyle.Fill };
        txtDatabase = new TextBox { Text = _config.database, Dock = DockStyle.Fill };

        txtLoadTestTable = new TextBox { Text = _config.loadTestTableName, Dock = DockStyle.Fill };

        // 🔥 Dirty Tracking
        txtServiceRoot.TextChanged += (s, e) => MarkDirty();
        txtApiRoot.TextChanged += (s, e) => MarkDirty();
        txtUser.TextChanged += (s, e) => MarkDirty();
        txtPassword.TextChanged += (s, e) => MarkDirty();
        txtSqlServer.TextChanged += (s, e) => MarkDirty();
        txtSqlPort.TextChanged += (s, e) => MarkDirty();
        txtDatabase.TextChanged += (s, e) => MarkDirty();

        layout.Controls.Add(new Label { Text = "ServiceRoot" }, 0, 0);
        layout.Controls.Add(txtServiceRoot, 1, 0);

        layout.Controls.Add(new Label { Text = "ApiRoot" }, 0, 1);
        layout.Controls.Add(txtApiRoot, 1, 1);

        layout.Controls.Add(new Label { Text = "Username" }, 0, 2);
        layout.Controls.Add(txtUser, 1, 2);

        layout.Controls.Add(new Label { Text = "Password" }, 0, 3);
        layout.Controls.Add(txtPassword, 1, 3);

        layout.Controls.Add(new Label { Text = "SQL Server" }, 0, 4);
        layout.Controls.Add(txtSqlServer, 1, 4);

        layout.Controls.Add(new Label { Text = "SQL Port" }, 0, 5);
        layout.Controls.Add(txtSqlPort, 1, 5);

        layout.Controls.Add(new Label { Text = "Database" }, 0, 6);
        layout.Controls.Add(txtDatabase, 1, 6);

        layout.Controls.Add(new Label { Text = "LoadTest Table" }, 0, 7);
        layout.Controls.Add(txtLoadTestTable, 1, 7);

        // 🔥 Buttons (kein Save mehr!)
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

    private void ConnectionSetupForm_FormClosing(object? sender, FormClosingEventArgs e)
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
    // 🔧 SAVE (intern)
    // =========================
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

        ConfigLoader.Save(_config);

        _isDirty = false;
        Text = "Connection Setup";
    }

    // =========================
    // 🔧 TEST SQL
    // =========================
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

            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);

            await conn.OpenAsync();

            MessageBox.Show("✅ SQL connection successful", "Success");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ SQL connection failed:\n{ex.Message}", "Error");
        }
    }

    // =========================
    // 🔧 TEST API
    // =========================
    private async Task TestApiConnection()
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var url = txtServiceRoot.Text.TrimEnd('/') + txtApiRoot.Text;

            var auth = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{txtUser.Text}:{txtPassword.Text}")
            );

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            var response = await client.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                MessageBox.Show("✅ API reachable", "Success");
            }
            else
            {
                MessageBox.Show($"⚠️ API responded: {response.StatusCode}", "Warning");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"❌ API failed:\n{ex.Message}", "Error");
        }
    }
}