using System.Windows.Forms;

namespace BCLoadtester.config;

public class SetupSelectionForm : Form
{
    public SetupSelectionForm(AppConfig config)
    {
        Text = "Setup";
        Width = 300;
        Height = 200;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(20)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 34));

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

        btnWorker.Click += (s, e) =>
        {
            new WorkerSetupForm(config).ShowDialog(this);
        };

        btnConnection.Click += (s, e) =>
        {
            new ConnectionSetupForm(config).ShowDialog(this);
        };

        btnCompany.Click += (s, e) =>
        {
            new CompanySetupForm(config).ShowDialog(this);
        };

        layout.Controls.Add(btnConnection, 0, 0);
        layout.Controls.Add(btnCompany, 0, 1);
        layout.Controls.Add(btnWorker, 0, 2); 

        Controls.Add(layout);
    }
}