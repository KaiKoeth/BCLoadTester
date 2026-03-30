using System;
using System.Windows.Forms;

namespace BCLoadtester.config;

public class SetupSelectionForm : Form
{
    private AppConfig _config;

    // 🔥 NEU: Change Tracking
    private bool _changed = false;

    public SetupSelectionForm(AppConfig config)
    {
        _config = config;

        Text = "Setup";
        Width = 320;
        Height = 320;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1,
            Padding = new Padding(20)
        };

        for (int i = 0; i < 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

        var btnConnection = new Button
        {
            Text = "🔌 Connection Setup",
            Dock = DockStyle.Fill
        };

        var btnCompany = new Button
        {
            Text = "🏢 Company Setup",
            Dock = DockStyle.Fill
        };

        var btnWorker = new Button
        {
            Text = "🧩 Worker Setup",
            Dock = DockStyle.Fill
        };

        var btnSaveConfig = new Button
        {
            Text = "💾 Save Config",
            Dock = DockStyle.Fill
        };

        var btnLoadConfig = new Button
        {
            Text = "📂 Load Config",
            Dock = DockStyle.Fill
        };

        // =========================
        // 🔥 EVENTS (JETZT MIT CHANGE TRACKING)
        // =========================

        btnConnection.Click += (s, e) =>
        {
            var dlg = new ConnectionSetupForm(_config);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _changed = true; // 🔥 NEU
            }
        };

        btnCompany.Click += (s, e) =>
        {
            var dlg = new CompanySetupForm(_config);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _changed = true; // 🔥 NEU
            }
        };

        btnWorker.Click += (s, e) =>
        {
            var dlg = new WorkerSetupForm(_config);
            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                _changed = true; // 🔥 NEU
            }
        };

        btnSaveConfig.Click += (s, e) => SaveConfig();
        btnLoadConfig.Click += (s, e) => LoadConfig();

        // =========================
        // 🔥 LAYOUT
        // =========================

        layout.Controls.Add(btnConnection, 0, 0);
        layout.Controls.Add(btnCompany, 0, 1);
        layout.Controls.Add(btnWorker, 0, 2);
        layout.Controls.Add(btnSaveConfig, 0, 3);
        layout.Controls.Add(btnLoadConfig, 0, 4);

        Controls.Add(layout);
    }

    // =========================
    // 💾 SAVE CONFIG
    // =========================
    private void SaveConfig()
    {
        using var dialog = new SaveFileDialog
        {
            Filter = "JSON (*.json)|*.json",
            FileName = "config.json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        try
        {
            ConfigLoader.SaveAs(_config, dialog.FileName);

            MessageBox.Show("Config saved successfully.", "Success");

            // 🔥 OPTIONAL: zählt als Änderung
            _changed = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Save Error");
        }
    }

    // =========================
    // 📂 LOAD CONFIG
    // =========================
    private void LoadConfig()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "JSON (*.json)|*.json"
        };

        if (dialog.ShowDialog() != DialogResult.OK)
            return;

        var confirm = MessageBox.Show(
            "Loading a config will overwrite the current configuration.\n\nContinue?",
            "Warning",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning
        );

        if (confirm != DialogResult.Yes)
            return;

        try
        {
            var newConfig = ConfigLoader.LoadFrom(dialog.FileName);

            // 🔥 Werte übernehmen
            _config.serviceRoot = newConfig.serviceRoot;
            _config.apiRoot = newConfig.apiRoot;
            _config.username = newConfig.username;
            _config.password = newConfig.password;

            _config.sqlServer = newConfig.sqlServer;
            _config.sqlPort = newConfig.sqlPort;
            _config.database = newConfig.database;

            _config.connectionString = newConfig.connectionString;
            _config.workers = newConfig.workers;
            _config.companies = newConfig.companies;

            _config.rpmPerWorker = newConfig.rpmPerWorker;
            _config.maxWorkersPerType = newConfig.maxWorkersPerType;
            _config.maxConnectionsPerServer = newConfig.maxConnectionsPerServer;

            ConfigLoader.Save(_config);

            MessageBox.Show(
                "Config loaded successfully.\nPlease reload data in dashboard.",
                "Success");

            // 🔥 WICHTIG: Änderung markieren
            _changed = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Load Error");
        }
    }

    // =========================
    // 🔥 WICHTIG: DialogResult steuern
    // =========================
    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);

        this.DialogResult = _changed
            ? DialogResult.OK
            : DialogResult.Cancel;
    }
}