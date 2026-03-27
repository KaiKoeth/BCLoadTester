using System;
using System.Windows.Forms;

public class WebOrderConfigForm : Form
{
    private WebOrderConfig _config;

    private NumericUpDown numMin;
    private NumericUpDown numMax;
    private NumericUpDown numBigLines;
    private NumericUpDown numInterval;

    // 🔥 NEU
    private TextBox txtPromotion;
    private TextBox txtTargetGroup;

    // 🔥 NEU
    private NumericUpDown numShipping;

    public WebOrderConfigForm(WebOrderConfig config)
    {
        _config = config;

        Text = "WebOrder Settings";
        Width = 400;
        Height = 420; // 🔥 größer für neues Feld
        StartPosition = FormStartPosition.CenterParent;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 🔥 vorher 7 → jetzt 8 Reihen
        for (int i = 0; i < 8; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

        numMin = CreateNumber(_config.minLines);
        numMax = CreateNumber(_config.maxLines);
        numBigLines = CreateNumber(_config.bigOrderLines, 1000);
        numInterval = CreateNumber(_config.bigOrderIntervalMinutes, 3600);

        // 🔥 NEU
        txtPromotion = new TextBox
        {
            Text = _config.promotionMediumNo ?? "",
            Dock = DockStyle.Fill,
            PlaceholderText = "z.B. DIVA"
        };

        txtTargetGroup = new TextBox
        {
            Text = _config.promotionMediumTrgGrpNo ?? "",
            Dock = DockStyle.Fill,
            PlaceholderText = "z.B. STD"
        };

        // 🔥 NEU: Shipping
        numShipping = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 10000,
            DecimalPlaces = 2,
            Increment = 0.5M,
            Value = _config.shippingChargeAmount,
            Dock = DockStyle.Fill
        };

        // =========================
        // 🔹 BESTEHEND
        // =========================
        layout.Controls.Add(new Label { Text = "Min Lines" }, 0, 0);
        layout.Controls.Add(numMin, 1, 0);

        layout.Controls.Add(new Label { Text = "Max Lines" }, 0, 1);
        layout.Controls.Add(numMax, 1, 1);

        layout.Controls.Add(new Label { Text = "Big Order Lines" }, 0, 2);
        layout.Controls.Add(numBigLines, 1, 2);

        layout.Controls.Add(new Label { Text = "Big Order Interval (min)" }, 0, 3); // 🔥 korrigiert
        layout.Controls.Add(numInterval, 1, 3);

        // =========================
        // 🔥 NEU
        // =========================
        layout.Controls.Add(new Label { Text = "Promotion Medium" }, 0, 4);
        layout.Controls.Add(txtPromotion, 1, 4);

        layout.Controls.Add(new Label { Text = "Target Group" }, 0, 5);
        layout.Controls.Add(txtTargetGroup, 1, 5);

        layout.Controls.Add(new Label { Text = "Shipping Charge" }, 0, 6);
        layout.Controls.Add(numShipping, 1, 6);

        // =========================
        // 🔘 BUTTON
        // =========================
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
            _config.bigOrderLines = (int)numBigLines.Value;
            _config.bigOrderIntervalMinutes = (int)numInterval.Value;

            // 🔥 NEU
            _config.promotionMediumNo = txtPromotion.Text?.Trim();
            _config.promotionMediumTrgGrpNo = txtTargetGroup.Text?.Trim();
            _config.shippingChargeAmount = numShipping.Value;

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