using System;
using System.Windows.Forms;

public class WebOrderConfigForm : Form
{
    private WebOrderConfig _config;

    private NumericUpDown numMin;
    private NumericUpDown numMax;
    private NumericUpDown numPool;
    private NumericUpDown numBigLines;
    private NumericUpDown numInterval;

    public WebOrderConfigForm(WebOrderConfig config)
    {
        _config = config;

        Text = "WebOrder Settings";
        Width = 400;
        Height = 300;
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (int i = 0; i < 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        numMin = CreateNumber(_config.minLines);
        numMax = CreateNumber(_config.maxLines);
        numPool = CreateNumber(_config.WeborderPoolSize, 1000000);
        numBigLines = CreateNumber(_config.bigOrderLines, 1000);
        numInterval = CreateNumber(_config.bigOrderIntervalMinutes, 3600);

        layout.Controls.Add(new Label { Text = "Min Lines" }, 0, 0);
        layout.Controls.Add(numMin, 1, 0);

        layout.Controls.Add(new Label { Text = "Max Lines" }, 0, 1);
        layout.Controls.Add(numMax, 1, 1);

        layout.Controls.Add(new Label { Text = "Pool Size" }, 0, 2);
        layout.Controls.Add(numPool, 1, 2);

        layout.Controls.Add(new Label { Text = "Big Order Lines" }, 0, 3);
        layout.Controls.Add(numBigLines, 1, 3);

        layout.Controls.Add(new Label { Text = "Big Order Interval (s)" }, 0, 4);
        layout.Controls.Add(numInterval, 1, 4);

        var btnOk = new Button
        {
            Text = "OK",
            Dock = DockStyle.Bottom,
            Height = 40
        };

        btnOk.Click += (s, e) =>
        {
            _config.minLines = (int)numMin.Value;
            _config.maxLines = (int)numMax.Value;
            _config.WeborderPoolSize = (int)numPool.Value;
            _config.bigOrderLines = (int)numBigLines.Value;
            _config.bigOrderIntervalMinutes = (int)numInterval.Value;

            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(layout);
        Controls.Add(btnOk);
    }

    private NumericUpDown CreateNumber(int value, int max = 100000)
    {
        return new NumericUpDown
        {
            Minimum = 0,
            Maximum = max,
            Value = value,
            Dock = DockStyle.Fill
        };
    }
}