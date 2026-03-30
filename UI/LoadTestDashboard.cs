namespace BCLoadtester;

using BCLoadtester.Loadtest;
using System.Linq;
using BCLoadtester.config;
using System.Diagnostics;


public partial class LoadTestDashboard : Form
{
    LoadController? _controller;
    AppConfig? _config;
    HttpClient _client;
    Button btnStart;
    Button btnStop;
    Button btnSetup;
    Button btnLoad;
    Button btnReset;
    Button btnShowData;
    Statistics _stats = new Statistics();
    DataGridView statsGrid;
    Label lblStartTime;
    Label lblStopTime;
    Label lblRuntime;
    DateTime? _startTime;
    DateTime? _stopTime;
    Label lblConfiguredRpm;
    Label lblTotalRps;
    Label lblTotalRpm;
    Label lblTotalRequests;
    Label lblTotalErrors;
    Label lblStatus;
    private Label lblEndTime;
    ProgressBar progressLoading;
    private const int PoolWarningThreshold = 500;
    private const int PoolCriticalThreshold = 250;
    private NumericUpDown numTestDuration;
    private NumericUpDown numRemainingMinutes;

    private PerformanceCounter _cpuCounter;
    private Process _process;
    private System.Windows.Forms.Timer _resourceTimer;

    private List<DashboardRow> _allRows = new();
    private List<DashboardRow> _visibleRows = new();
    private List<(string Worker, string Company, long Rpm, long Requests, long Errors, double Rps, int PoolSize, double AvgMs, long MaxMs)> _cachedStats = new();

    private int _remainingMinutes = 0;
    private int _loadedDurationMinutes = 0;
    private System.Windows.Forms.Timer? _runtimeTimer;

    // ✅ SAUBERE CACHES
    private Dictionary<(string Company, string Worker), List<CustomerEntry>> _customerPools = new();
    private Dictionary<string, List<string>> _invoiceCustomerNoCache = new();
    private Dictionary<string, List<string>> _creditMemoCustomerNoCache = new();
    private Dictionary<string, OrderStatusPool> _orderStatusCache = new();
    private Dictionary<string, WebOrderPayloadPool> _webOrderPoolCache = new();
    private Dictionary<(string Company, string Worker), int> _workerCounts = new();

    System.Windows.Forms.Timer? _loadingTimer;
    string _loadingPhase = "Loading";
    int _loadedCompanies = 0;
    int _totalCompanies = 0;

    public LoadTestDashboard()
    {
        _client = new HttpClient(new SocketsHttpHandler
        {
            MaxConnectionsPerServer = 200,
            PooledConnectionLifetime = TimeSpan.FromMinutes(10),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
            EnableMultipleHttp2Connections = true
        });

        InitializeComponent();
        InitializeApp();

        _process = Process.GetCurrentProcess();

        // CPU Counter (gesamt → wir rechnen runter)
        _cpuCounter = new PerformanceCounter(
            "Process",
            "% Processor Time",
            _process.ProcessName,
            true
        );

        // erster Call liefert 0 → warmup
        _cpuCounter.NextValue();

        _resourceTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };

        _resourceTimer.Tick += (s, e) => UpdateResourceUsage();
        _resourceTimer.Start();

        this.MinimumSize = new Size(1000, 600);
        this.Size = new Size(1300, 800);
        this.FormClosing += LoadTestDashboard_FormClosing;

        // =========================
        // 🔷 MAIN LAYOUT (FIX!)
        // =========================
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 5,
            ColumnCount = 1
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // TopBar 1 (Load)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45)); // 🔥 Laufzeit
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Start/Stop
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Status
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Stats
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid

        Controls.Add(layout);

        // =========================
        // 🔷 TOP BAR
        // =========================

        // =========================
        // 🔷 BUTTONS (ZUERST ERZEUGEN!)
        // =========================
        btnSetup = new Button { Text = "⚙ Setup", Width = 120 };
        btnLoad = new Button { Text = "📥 Load Data", Width = 120 };
        btnShowData = new Button { Text = "📊 Show Data", Width = 120 };
        numTestDuration = new NumericUpDown
        {
            Width = 80,
            Minimum = 1,
            Maximum = 10000,
            Value = 60
        };

        numRemainingMinutes = new NumericUpDown
        {
            Width = 80,
            Minimum = 0,
            Maximum = 10000,
            Value = 60
        };

        lblEndTime = new Label
        {
            Text = "",
            AutoSize = true,
            Margin = new Padding(20, 4, 0, 0),
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };


        numRemainingMinutes.ValueChanged += (s, e) =>
        {
            int newValue = (int)numRemainingMinutes.Value;

            // 🔥 immer übernehmen (auch während Test)
            _remainingMinutes = newValue;
            UpdateEndTime();

            // 🔥 wenn auf 0 gesetzt → sofort stoppen
            if (_controller != null && _remainingMinutes <= 0)
            {
                btnStop_Click(this, EventArgs.Empty);
            }
        };

        _remainingMinutes = 60;
        _loadedDurationMinutes = 0; // noch nichts geladen
        btnStart = new Button { Text = "▶ Start", Width = 120, Height = 50, BackColor = Color.LightGreen };
        btnStop = new Button { Text = "■ Stop", Width = 120, Height = 50, BackColor = Color.IndianRed };
        btnReset = new Button { Text = "↺ Reset", Width = 120, Height = 50, BackColor = Color.Khaki };


        btnStart.Enabled = false;
        btnStop.Enabled = false;
        btnReset.Enabled = false;


        // 🔥 MARGINS RESET → wichtig für perfekte Ausrichtung
        btnSetup.Margin = new Padding(0, 0, 10, 0);
        btnLoad.Margin = new Padding(0, 0, 10, 0);
        btnShowData.Margin = new Padding(0, 0, 10, 0);


        btnStart.Margin = new Padding(0, 0, 10, 0);
        btnStop.Margin = new Padding(0, 0, 10, 0);
        btnReset.Margin = new Padding(0, 0, 10, 0);

        // =========================
        // 🔷 EVENTS (🔥 WICHTIG!)
        // =========================
        btnStart.Click += btnStart_Click;
        btnStop.Click += btnStop_Click;
        btnReset.Click += btnReset_Click;

        btnSetup.Click += (s, e) =>
        {
            if (_config == null)
            {
                MessageBox.Show("Config not loaded.");
                return;
            }

            var dlg = new SetupSelectionForm(_config);

            if (dlg.ShowDialog(this) == DialogResult.OK)
            {
                ReloadConfig();
            }
            else
            {
                lblStatus.Text = "No changes";
                lblStatus.ForeColor = Color.Gray;
            }
        };

        btnLoad.Click += async (s, e) => await LoadDataAsync();
        btnShowData.Click += btnShowData_Click;

        // =========================
        // 🔷 STATUS
        // =========================
        lblStatus = new Label
        {
            Text = "Idle",
            AutoSize = false,              // 🔥 WICHTIG!
            Width = 400,                  // 🔥 feste Breite
            Height = 25,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(20, 5, 0, 0),
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            AutoEllipsis = true
        };

        progressLoading = new ProgressBar
        {
            Width = 150,
            Visible = false
        };
        // =========================
        // 🔷 TOP BAR 1 (Load / Show links, Setup rechts)
        // =========================
        var topBar1 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(10, 10, 10, 0),
            Margin = new Padding(0)
        };

        topBar1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topBar1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        topBar1.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // 🔹 Linke Seite (Load + Show)
        var leftPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0)
        };

        btnLoad.Margin = new Padding(0, 0, 10, 0);
        btnShowData.Margin = new Padding(0);


        // 🔥 Reihenfolge
        leftPanel.Controls.Add(btnLoad);
        leftPanel.Controls.Add(btnShowData);

        // 🔹 Setup rechts
        btnSetup.Margin = new Padding(0);

        // 🔹 Einfügen
        topBar1.Controls.Add(leftPanel, 0, 0);
        topBar1.Controls.Add(new Panel(), 1, 0);
        topBar1.Controls.Add(btnSetup, 2, 0);

        var durationBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 5, 10, 0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        // 🔹 Testlaufzeit
        durationBar.Controls.Add(new Label
        {
            Text = "Testlaufzeit:",
            Width = 110,
            TextAlign = ContentAlignment.MiddleLeft
        });

        durationBar.Controls.Add(numTestDuration);

        // 🔹 Restlaufzeit
        durationBar.Controls.Add(new Label
        {
            Text = "Restlaufzeit:",
            Width = 110,
            Margin = new Padding(30, 0, 0, 0),
            TextAlign = ContentAlignment.MiddleLeft
        });

        durationBar.Controls.Add(numRemainingMinutes);

        // 🔹 Endzeit (GANZ RECHTS!)
        durationBar.Controls.Add(lblEndTime);

        numTestDuration.Margin = new Padding(0, 2, 10, 0);
        numRemainingMinutes.Margin = new Padding(0, 2, 0, 0);
        numRemainingMinutes.Visible = false;
        lblEndTime.Visible = false;


        // =========================
        // 🔷 TOP BAR 2 (Start/Stop links)
        // =========================
        var topBar2 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0)
        };

        topBar2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        topBar2.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // 🔹 Linke Seite (Start/Stop)
        // =========================
        // 🔹 BUTTON GRID (FIXED SPACING)
        // =========================
        var buttonGrid = new TableLayoutPanel
        {
            ColumnCount = 5,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };

        // 🔥 feste Abstände über Spalten
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10)); // Abstand
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 10)); // Abstand
        buttonGrid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // 🔥 Margins neutralisieren (wichtig!)
        btnStart.Margin = new Padding(0);
        btnStop.Margin = new Padding(0);
        btnReset.Margin = new Padding(0);

        // 🔥 hinzufügen
        buttonGrid.Controls.Add(btnStart, 0, 0);
        buttonGrid.Controls.Add(btnStop, 2, 0);
        buttonGrid.Controls.Add(btnReset, 4, 0);

        // 🔥 in TopBar einfügen
        topBar2.Controls.Add(buttonGrid, 0, 0);


        // =========================
        // 🔷 TOP BAR 3 (Status links)
        // =========================
        var topBar3 = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0),
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };

        // 🔹 schöner Abstand
        lblStatus.Margin = new Padding(0, 8, 10, 0);
        progressLoading.Margin = new Padding(0, 8, 0, 0);

        topBar3.Controls.Add(lblStatus);
        topBar3.Controls.Add(progressLoading);
        // =========================
        // 🔷 KPI BAR
        // =========================
        var statsBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            FlowDirection = FlowDirection.LeftToRight
        };

        lblStartTime = new Label { Text = "Start: -", Width = 150 };
        lblStopTime = new Label { Text = "Stop: -", Width = 150 };
        lblRuntime = new Label { Text = "Runtime: 00:00:00", Width = 180 };
        lblConfiguredRpm = new Label { Text = "Configured RPM: 0", Width = 180, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        lblTotalRpm = new Label { Text = "Total RPM: 0", Width = 140, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        lblTotalRps = new Label { Text = "RPS: 0", Width = 120, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
        lblTotalRequests = new Label { Width = 160 };
        lblTotalErrors = new Label { Width = 140 };

        statsBar.Controls.Add(lblStartTime);
        statsBar.Controls.Add(lblStopTime);
        statsBar.Controls.Add(lblRuntime);
        statsBar.Controls.Add(lblConfiguredRpm);

        statsBar.Controls.Add(new Label { Width = 30 });

        statsBar.Controls.Add(lblTotalRpm);
        statsBar.Controls.Add(lblTotalRps);
        statsBar.Controls.Add(lblTotalRequests);
        statsBar.Controls.Add(lblTotalErrors);

        // =========================
        // 🔷 GRID
        // =========================
        statsGrid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            RowHeadersVisible = false
        };

        statsGrid.ColumnCount = 9;
        statsGrid.Columns[0].Name = "Company";
        statsGrid.Columns[1].Name = "Worker";
        statsGrid.Columns[2].Name = "RPM";
        statsGrid.Columns[3].Name = "Requests";
        statsGrid.Columns[4].Name = "Errors";
        statsGrid.Columns[5].Name = "Target RPS";
        statsGrid.Columns[6].Name = "Actual RPS";
        statsGrid.Columns[7].Name = "Avg ms";
        statsGrid.Columns[8].Name = "Max ms";

        statsGrid.CellClick += statsGrid_CellClick;

        // 🔥 HIER IST DER FIX!
        layout.Controls.Add(topBar1, 0, 0);      // Load
        layout.Controls.Add(durationBar, 0, 1);  // 🔥 NEU
        layout.Controls.Add(topBar2, 0, 2);      // Start/Stop
        layout.Controls.Add(topBar3, 0, 3);      // Status
        layout.Controls.Add(statsBar, 0, 4);
        layout.Controls.Add(statsGrid, 0, 5);

        // =========================
        // 🔷 TIMER (UNVERÄNDERT)
        // =========================
        var timer = new System.Windows.Forms.Timer { Interval = 1000 };
        timer.Tick += (s, e) =>
        {
            var runtime = _startTime == null
                ? TimeSpan.Zero
                : (_stopTime ?? DateTime.Now) - _startTime.Value;

            lblRuntime.Text = $"Runtime: {runtime:hh\\:mm\\:ss}";

            var stats = _stats.GetStats();

            // 🔥 Snapshot + reuse
            _cachedStats.Clear();

            foreach (var stat in stats)
            {
                _cachedStats.Add(stat);
            }

            // 🔥 schneller als LINQ
            _cachedStats.Sort(static (a, b) =>
            {
                int cmp = string.Compare(a.Company, b.Company, StringComparison.Ordinal);
                if (cmp != 0)
                    return cmp;

                return string.Compare(a.Worker, b.Worker, StringComparison.Ordinal);
            });

            if (_cachedStats.Count > 0)
            {
                BuildRows(_cachedStats);
            }
            RefreshGrid(runtime);

            UpdatePoolWarnings();
        };
        timer.Start();
    }

    private async void btnStart_Click(object sender, EventArgs e)
    {

        if (_loadedDurationMinutes != (int)numTestDuration.Value)
        {
            MessageBox.Show("Test duration changed. Please reload data.");
            return;
        }

        if (_config == null ||
            (_customerPools.Count == 0 &&
            _invoiceCustomerNoCache.Count == 0 &&
            _creditMemoCustomerNoCache.Count == 0 &&
            _orderStatusCache.Count == 0 &&
            _webOrderPoolCache.Count == 0))
        {
            MessageBox.Show("Please load data first.");
            return;
        }

        // 🔥 Pre-Check
        lblStatus.Text = "Testing connections...";
        lblStatus.ForeColor = Color.DarkOrange;

        if (!await TestSqlConnectionAsync())
        {
            lblStatus.Text = "SQL connection failed";
            lblStatus.ForeColor = Color.Red;
            return;
        }

        if (!await TestApiConnectionAsync())
        {
            lblStatus.Text = "API connection failed";
            lblStatus.ForeColor = Color.Red;
            return;
        }
        SetUiState(true);

        var authToken = Convert.ToBase64String(
            System.Text.Encoding.ASCII.GetBytes($"{_config.username}:{_config.password}")
        );

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", authToken);

        _stats = new Statistics();
        _startTime = DateTime.Now;
        _stopTime = null; // 👈 wichtig

        lblStartTime.Text = $"Start: {_startTime:HH:mm:ss}";
        lblStopTime.Text = "Stop: -";

        var workers = new List<IWorker>();
        var enabledCompanies = _config.companies.Where(c => c.enabled).ToList();
        btnReset.Enabled = false;


        foreach (var company in enabledCompanies)
        {
            List<string>? invoiceCustomers = null;
            List<string>? creditMemoCustomers = null;
            OrderStatusPool? orderStatusPool = null;
            WebOrderPayloadPool? webOrderPool = null;

            bool hasRequiredData = true;

            foreach (var worker in _config.workers.Where(w => w.enabled))
            {
                switch (worker.type)
                {
                    case "GetInvoiceDetails":
                        if (!_invoiceCustomerNoCache.TryGetValue(company.name, out invoiceCustomers))
                            hasRequiredData = false;
                        break;

                    case "GetCreMemoDetails":
                        if (!_creditMemoCustomerNoCache.TryGetValue(company.name, out creditMemoCustomers))
                            hasRequiredData = false;
                        break;

                    case "OrderStatus":
                        if (!_orderStatusCache.TryGetValue(company.name, out orderStatusPool))
                            hasRequiredData = false;
                        break;

                    case "WebOrderCreate":
                        if (!_webOrderPoolCache.TryGetValue(company.name, out webOrderPool))
                            hasRequiredData = false;
                        break;
                }

                if (!hasRequiredData)
                    break;
            }

            if (!hasRequiredData)
            {
                lblStatus.Text = $"Skipping company {company.name} (missing required data)";
                continue;
            }

            foreach (var worker in _config.workers.Where(w => w.enabled))
            {
                if (!company.rpm.ContainsKey(worker.type))
                    continue;

                int rpm = company.rpm[worker.type];

                // 🔥 WICHTIG: Parallelisierung wieder rein
                int parallelWorkers = Math.Clamp(
                    rpm / _config.rpmPerWorker,
                    1,
                    _config.maxWorkersPerType);

                int workerRpm = Math.Max(1, rpm / parallelWorkers);
                string workerKey = worker.type;
                _workerCounts[(company.name, workerKey)] = parallelWorkers;
                string workerDisplayName = $"{worker.type} (x{parallelWorkers})";
                for (int i = 0; i < parallelWorkers; i++)
                {
                    switch (worker.type)
                    {
                        case "EmailSearch":
                            {
                                if (!_customerPools.TryGetValue((company.name, worker.type), out var emailCustomers) || emailCustomers.Count == 0)
                                    break;

                                workers.Add(new EmailSearchWorker(
                                    _client, emailCustomers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "PMCSearch":
                            {
                                if (!_customerPools.TryGetValue((company.name, worker.type), out var pmcCustomers) || pmcCustomers.Count == 0)
                                    break;

                                workers.Add(new PhoneticSearchWorker(
                                    _client, pmcCustomers,
                                    _config.serviceRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "CustomerCreate":
                            {
                                if (!_customerPools.TryGetValue((company.name, worker.type), out var createCustomers) || createCustomers.Count == 0)
                                    break;

                                workers.Add(new CustomerCreateWorker(
                                    _client, createCustomers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "CreateShipToAddress":
                            {
                                if (!_customerPools.TryGetValue((company.name, worker.type), out var shipToCustomers) || shipToCustomers.Count == 0)
                                    break;

                                workers.Add(new ShipToAddressCreateWorker(
                                    _client, shipToCustomers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "CustomerHistory":
                            {
                                if (!_customerPools.TryGetValue((company.name, worker.type), out var historyCustomers) || historyCustomers.Count == 0)
                                    break;

                                workers.Add(new CustomerHistoryWorker(
                                    _client,
                                    historyCustomers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "WebOrderCreate":
                            {
                                workers.Add(new WebOrderCreateWorker(
                                    _client,
                                    orderStatusPool,
                                    webOrderPool,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats,
                                    workerKey,
                                    company.webOrderConfig?.bigOrderLines ?? 0,
                                    company.webOrderConfig?.bigOrderIntervalMinutes ?? 0,
                                    company.webOrderConfig?.promotionMediumNo,
                                    company.webOrderConfig?.promotionMediumTrgGrpNo,
                                    company.webOrderConfig?.shippingChargeAmount ?? 0
                                ));
                                break;
                            }

                        case "GetInvoiceDetails":
                            {
                                if (invoiceCustomers.Count == 0)
                                    break;

                                workers.Add(new GetInvoiceDetailsWorker(
                                    _client,
                                    invoiceCustomers,
                                    _config.serviceRoot,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "GetCreMemoDetails":
                            {
                                if (creditMemoCustomers.Count == 0)
                                    break;

                                workers.Add(new GetCreMemoDetailsWorker(
                                    _client,
                                    creditMemoCustomers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }

                        case "OrderStatus":
                            {
                                workers.Add(new OrderStatusWorker(
                                    _client,
                                    orderStatusPool,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;
                            }
                    }
                }

            }

        }

        _controller = new LoadController();
        _controller.Start(workers);

        StartRuntimeCountdown();
        numTestDuration.Enabled = false;
        _remainingMinutes = (int)numTestDuration.Value;
        numRemainingMinutes.Value = _remainingMinutes;
        numRemainingMinutes.Visible = true;
        lblEndTime.Visible = true;

        // 🔥 wichtig
        UpdateEndTime();
        lblStatus.Text = "Test running";
    }

    private async void btnStop_Click(object sender, EventArgs e)
    {
        try
        {
            _stopTime = DateTime.Now;

            _controller?.Stop();
            _controller = null; // 🔥 DAS FEHLT!

            if (_runtimeTimer != null)
            {
                _runtimeTimer.Stop();
                _runtimeTimer.Tick -= RuntimeTimer_Tick;
            }

            lblStopTime.Text = $"Stop: {_stopTime:HH:mm:ss}";

            // 🔥 UI sofort deaktivieren (kein Doppel-Stop möglich)
            btnStop.Enabled = false;
            btnReset.Enabled = true;

            // 🔥 Status: Speichern läuft
            lblStatus.Text = "Saving results...";
            lblStatus.ForeColor = Color.DarkOrange;

            progressLoading.Visible = true;
            progressLoading.Style = ProgressBarStyle.Marquee;
            progressLoading.MarqueeAnimationSpeed = 30;

            // =========================
            // 💾 SAVE
            // =========================
            await SaveResultsToDatabaseAsync();

            progressLoading.Visible = false;

            // 🔥 UI zurücksetzen
            SetUiState(false);
            numTestDuration.Enabled = true;

            lblStatus.Text = "Test stopped";
            lblStatus.ForeColor = Color.Black;
            numRemainingMinutes.Visible = false;
            lblEndTime.Visible = false;
        }
        catch (Exception ex)
        {
            progressLoading.Visible = false; // 🔥 safety
            MessageBox.Show($"Fehler beim Stop:\n{ex.Message}", "Error");
        }
    }

    private async void InitializeApp()
    {
        try
        {
            _config = ConfigLoader.Load();
            _client = new HttpClient(new SocketsHttpHandler
            {
                MaxConnectionsPerServer = _config.maxConnectionsPerServer
            });

        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message);
        }
    }

    private async Task LoadDataAsync()
    {
        if (_config == null)
            return;

        int durationMinutes = (int)numTestDuration.Value;

        _loadedDurationMinutes = durationMinutes;
        _remainingMinutes = durationMinutes;

        // 🔥 UI synchronisieren
        numRemainingMinutes.Value = durationMinutes;

        // =========================
        // 🔥 PRE-CHECK
        // =========================
        lblStatus.Text = "Testing connections...";
        lblStatus.ForeColor = Color.DarkOrange;

        if (!await TestSqlConnectionAsync())
        {
            lblStatus.Text = "SQL connection failed";
            lblStatus.ForeColor = Color.Red;
            return;
        }

        if (!await TestApiConnectionAsync())
        {
            lblStatus.Text = "API connection failed";
            lblStatus.ForeColor = Color.Red;
            return;
        }

        try
        {
            // =========================
            // 🔥 UI LOCK
            // =========================
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            btnSetup.Enabled = false;
            btnLoad.Enabled = false;

            lblStatus.ForeColor = Color.DarkOrange;

            // =========================
            // 🔥 PROVIDER
            // =========================
            var customerProvider = new CustomerDataProvider(_config.connectionString);
            var invoiceProvider = new InvoiceCustomerDataProvider(_config.connectionString);
            var creditProvider = new CreditMemoCustomerDataProvider(_config.connectionString);
            var orderProvider = new OrderStatusCustomerDataProvider(_config.connectionString);
            var webOrderProvider = new WebOrderDataProvider(_config.connectionString);

            var companies = _config.companies.Where(c => c.enabled).ToList();

            var enabledWorkers = _config.workers
                .Where(w => w.enabled)
                .Select(w => w.type)
                .ToHashSet();

            _totalCompanies = companies.Count;
            _loadedCompanies = 0;

            progressLoading.Visible = true;
            progressLoading.Style = ProgressBarStyle.Marquee;
            progressLoading.MarqueeAnimationSpeed = 30;

            // =========================
            // 🔁 COMPANIES
            // =========================
            foreach (var company in companies)
            {
                lblStatus.Text = $"Loading data ({company.name})...";
                Application.DoEvents();

                // =========================
                // 🔹 CUSTOMER-BASED WORKER
                // =========================
                foreach (var workerType in new[]
                {
                "EmailSearch",
                "PMCSearch",
                "CustomerCreate",
                "CreateShipToAddress",
                "CustomerHistory"
            })
                {
                    if (!enabledWorkers.Contains(workerType))
                        continue;

                    int rpm = company.rpm.GetValueOrDefault(workerType, 0);
                    if (rpm <= 0)
                        continue;

                    int required = CalculateRequiredData(workerType, rpm, durationMinutes);

                    var data = await customerProvider.LoadCustomers(company.name, required);

                    _customerPools[(company.name, workerType)] = data;

                    lblStatus.Text = $"Loading {company.name} - {workerType} ({required})";
                    Application.DoEvents();
                }

                // =========================
                // 🔹 INVOICE
                // =========================
                if (enabledWorkers.Contains("GetInvoiceDetails"))
                {
                    int rpm = company.rpm.GetValueOrDefault("GetInvoiceDetails", 0);
                    if (rpm > 0)
                    {
                        int required = CalculateRequiredData("GetInvoiceDetails", rpm, durationMinutes);

                        _invoiceCustomerNoCache[company.name] =
                            await invoiceProvider.LoadCustomers(company.name, required);
                    }
                }

                // =========================
                // 🔹 CREDIT MEMO
                // =========================
                if (enabledWorkers.Contains("GetCreMemoDetails"))
                {
                    int rpm = company.rpm.GetValueOrDefault("GetCreMemoDetails", 0);
                    if (rpm > 0)
                    {
                        int required = CalculateRequiredData("GetCreMemoDetails", rpm, durationMinutes);

                        _creditMemoCustomerNoCache[company.name] =
                            await creditProvider.LoadCustomers(company.name, required);
                    }
                }

                // =========================
                // 🔹 ORDER STATUS
                // =========================
                if (enabledWorkers.Contains("OrderStatus"))
                {
                    int rpm = company.rpm.GetValueOrDefault("OrderStatus", 0);
                    if (rpm > 0)
                    {
                        int required = CalculateRequiredData("OrderStatus", rpm, durationMinutes);

                        var orderCustomers = await orderProvider.LoadCustomers(company.name, required);

                        var pool = new OrderStatusPool();
                        pool.AddRange(orderCustomers);

                        _orderStatusCache[company.name] = pool;
                    }
                }

                // =========================
                // 🔹 WEB ORDER
                // =========================
                if (enabledWorkers.Contains("WebOrderCreate"))
                {
                    int rpm = company.rpm.GetValueOrDefault("WebOrderCreate", 0);

                    if (rpm > 0)
                    {
                        int orders = rpm * durationMinutes;
                        var factor = GetBufferFactor("WebOrderCreate");
                        int requiredOrders = (int)(orders * factor);

                        int avgLines =
                            (company.webOrderConfig.minLines + company.webOrderConfig.maxLines) / 2;

                        int requiredCustomers = requiredOrders;
                        int requiredItems = requiredOrders * avgLines;

                        lblStatus.Text = $"Loading {company.name} - WebOrder ({requiredOrders})";
                        Application.DoEvents();

                        var profile = await webOrderProvider.LoadProfile(
                            company.name,
                            requiredCustomers,
                            requiredItems
                        );

                        _webOrderPoolCache[company.name] = new WebOrderPayloadPool(
                            company.name,
                            profile,
                            company.webOrderConfig.minLines,
                            company.webOrderConfig.maxLines,
                            requiredOrders,
                            company.webOrderConfig?.shippingChargeAmount ?? 0
                        );
                    }
                }

                _loadedCompanies++;
            }

            // =========================
            // 🔥 DONE
            // =========================
            progressLoading.Visible = false;

            lblStatus.Text = "Data loaded successfully";
            lblStatus.ForeColor = Color.Green;
        }
        catch (Exception ex)
        {
            progressLoading.Visible = false;

            lblStatus.Text = "Error while loading data";
            lblStatus.ForeColor = Color.Red;

            MessageBox.Show(ex.ToString(), "LoadData Error");
        }

        // =========================
        // 🔥 UI RESET
        // =========================
        btnStart.Enabled = true;
        btnStop.Enabled = false;
        btnSetup.Enabled = true;
        btnLoad.Enabled = true;

        BuildInitialRows();
        RefreshGrid(TimeSpan.Zero);
        UpdateConfiguredRpmLabel();
    }
    private void BuildRows(IEnumerable<(string Worker, string Company, long Rpm, long Requests, long Errors, double Rps, int PoolSize, double AvgMs, long MaxMs)> stats)
    {
        var previousState = _allRows
            .Where(r => r.IsGroup)
            .ToDictionary(r => r.Company, r => r.IsExpanded);

        _allRows.Clear();

        var enabledWorkers = _config.workers
            .Where(w => w.enabled)
            .Select(w => w.type)
            .ToHashSet();

        foreach (var companyGroup in stats.GroupBy(s => s.Company))
        {
            var companyName = companyGroup.Key;

            bool isExpanded = previousState.ContainsKey(companyName)
                ? previousState[companyName]
                : false;


            // 🔹 Gruppenzeile
            _allRows.Add(new DashboardRow
            {
                Company = companyName,
                IsGroup = true,
                IsExpanded = isExpanded,

                RPM = GetConfiguredCompanyRpm(companyName),

                Requests = companyGroup.Sum(x => x.Requests),
                Errors = companyGroup.Sum(x => x.Errors),
                AvgMs = companyGroup.Average(x => x.AvgMs),
                MaxMs = companyGroup.Max(x => x.MaxMs)
            });

            // 🔹 Worker-Zeilen
            foreach (var w in companyGroup)
            {
                string displayWorker = w.Worker;

                // ✅ WICHTIG: (xN) aus Cache holen (NICHT aus String!)
                if (_workerCounts.TryGetValue((companyName, w.Worker), out var count) && count > 1)
                {
                    displayWorker += $" (x{count})";
                }

                // ✅ Pool anzeigen (bestehendes Verhalten bleibt)
                // 🔥 Pool anzeigen (bestehendes Verhalten beibehalten)
                if ((w.Worker == "OrderStatus" || w.Worker == "WebOrderCreate") && w.PoolSize > 0)
                {
                    displayWorker += $" ({w.PoolSize})";
                }

                // 🔥 NEU: BigOrders anzeigen (nur für WebOrder)
                if (w.Worker == "WebOrderCreate")
                {
                    var bigOrders = _stats.GetCustomMetric(w.Worker, companyName, "BigOrders");
                    displayWorker += $" [{bigOrders}]";

                }

                _allRows.Add(new DashboardRow
                {
                    Company = companyName,
                    Worker = w.Worker,
                    DisplayWorker = displayWorker,
                    IsGroup = false,

                    RPM = w.Rpm * (
                         _workerCounts.TryGetValue((companyName, w.Worker), out count)
                             ? count
                             : 1
                     ),

                    Requests = w.Requests,
                    Errors = w.Errors,
                    AvgMs = w.AvgMs,
                    MaxMs = w.MaxMs
                });
            }
        }
    }

    private long ExtractTotalRpm(string workerName, long workerRpm)
    {
        // erwartet Format: "OrderStatus (x3)"
        var start = workerName.IndexOf("(x");
        var end = workerName.IndexOf(")");

        if (start >= 0 && end > start)
        {
            var numberText = workerName.Substring(start + 2, end - start - 2);

            if (int.TryParse(numberText, out int count))
            {
                return workerRpm * count;
            }
        }

        return workerRpm;
    }

    private void RefreshGrid(TimeSpan runtime)
    {
        UpdateVisibleRows();

        statsGrid.Rows.Clear();

        double totalRps = 0;
        long totalRequests = 0;
        long totalErrors = 0;

        foreach (var row in _visibleRows)
        {
            double targetRps = row.RPM / 60.0;

            double actualRps = runtime.TotalSeconds > 0
                ? row.Requests / runtime.TotalSeconds
                : 0;

            totalRps += actualRps;
            totalRequests += row.Requests;
            totalErrors += row.Errors;

            string companyDisplay = row.IsGroup
                ? (row.IsExpanded ? "▼ " : "▶ ") + row.Company
                : "";

            // ✅ HIER war dein Fehler
            string workerDisplay = row.IsGroup
                ? ""
                : "    " + (row.DisplayWorker ?? row.Worker);

            int rowIndex = statsGrid.Rows.Add(
                companyDisplay,
                workerDisplay,
                row.RPM,
                row.Requests,
                row.Errors,
                targetRps.ToString("0.00"),
                actualRps.ToString("0.00"),
                row.AvgMs.ToString("0"),
                row.MaxMs.ToString("0")
            );

            if (row.IsGroup)
            {
                statsGrid.Rows[rowIndex].DefaultCellStyle.Font =
                    new Font(statsGrid.Font, FontStyle.Bold);
            }

            var errorCell = statsGrid.Rows[rowIndex].Cells[4];
            errorCell.Style.ForeColor = row.Errors > 0 ? Color.Red : Color.Black;


        }
        var totalRpm = totalRps * 60;

        lblTotalRpm.Text = $"Total RPM: {totalRpm:0}";
        lblTotalRps.Text = $"Total RPS: {totalRps:0.00}";
        lblTotalRequests.Text = $"Total Requests: {totalRequests}";
        lblTotalErrors.Text = $"Total Errors: {totalErrors}";
    }

    private void UpdateVisibleRows()
    {
        _visibleRows.Clear();

        bool expanded = false;

        foreach (var row in _allRows)
        {
            if (row.IsGroup)
            {
                expanded = row.IsExpanded;
                _visibleRows.Add(row);
            }
            else if (expanded)
            {
                _visibleRows.Add(row);
            }
        }
    }

    private void statsGrid_CellClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
            return;

        if (e.RowIndex >= _visibleRows.Count)
            return;

        var row = _visibleRows[e.RowIndex];

        // =========================
        // 👉 1. GROUP TOGGLE (wie bisher)
        // =========================
        if (e.ColumnIndex == 0 && row.IsGroup)
        {
            row.IsExpanded = !row.IsExpanded;

            var group = _allRows.FirstOrDefault(r => r.IsGroup && r.Company == row.Company);
            if (group != null)
                group.IsExpanded = row.IsExpanded;

            RefreshGrid(
                _startTime == null
                    ? TimeSpan.Zero
                    : (_stopTime ?? DateTime.Now) - _startTime.Value
            );

            return;
        }

        // =========================
        // 👉 2. ERROR CLICK
        // =========================
        if (e.ColumnIndex == 4 && !row.IsGroup)
        {
            var workerKey = NormalizeWorkerName(row.Worker);
            var errors = _stats.GetErrors(workerKey, row.Company);

            if (errors.Count == 0)
            {
                MessageBox.Show("No error details available.");
                return;
            }

            var text = string.Join(Environment.NewLine,
                errors.OrderByDescending(x => x.Value)
                    .Select(x => $"{x.Key}: {x.Value}"));

            MessageBox.Show(text, $"Errors - {row.DisplayWorker ?? row.Worker}");
        }
    }

    private void SetUiState(bool isRunning)
    {
        btnStart.Enabled = !isRunning;
        btnStop.Enabled = isRunning;
        // diese Buttons musst du als Felder speichern!
        btnSetup.Enabled = !isRunning;
        btnLoad.Enabled = !isRunning;
        btnReset.Enabled = !isRunning && _startTime != null && _stopTime != null;
    }

    private void btnShowData_Click(object sender, EventArgs e)
    {
        if (_customerPools.Count == 0
            && _invoiceCustomerNoCache.Count == 0
            && _creditMemoCustomerNoCache.Count == 0
            && _orderStatusCache.Count == 0
            && _webOrderPoolCache.Count == 0)
        {
            MessageBox.Show("No data loaded.");
            return;
        }

        var form = new Form
        {
            Text = "Loaded Data",
            Width = 900,
            Height = 650
        };

        var textbox = new TextBox
        {
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 10),
            ReadOnly = true
        };

        form.Controls.Add(textbox);

        var sb = new System.Text.StringBuilder();

        var companies = _config.companies
        .Where(c => c.enabled)
        .Select(c => c.name);

        foreach (var company in companies)
        {
            sb.AppendLine($"=================================================");
            sb.AppendLine($"Company: {company}");
            sb.AppendLine($"=================================================");

            var cfg = _config.companies.First(c => c.name == company);

            // =========================
            // 🔹 CUSTOMER WORKER POOLS
            // =========================
            sb.AppendLine("Customer-based Workers:");

            var workerPools = _customerPools
                .Where(x => x.Key.Company == company)
                .OrderBy(x => x.Key.Worker)
                .ToList();

            foreach (var wp in workerPools)
            {
                int rpm = cfg.rpm.GetValueOrDefault(wp.Key.Worker, 0);

                sb.AppendLine(
                    $"  {wp.Key.Worker,-25} | Loaded: {wp.Value.Count,6} | RPM: {rpm,4}"
                );
            }

            sb.AppendLine();

            // =========================
            // 🔹 INVOICE
            // =========================
            if (_invoiceCustomerNoCache.TryGetValue(company, out var inv))
            {
                int rpm = cfg.rpm.GetValueOrDefault("GetInvoiceDetails", 0);

                sb.AppendLine(
                    $"  {"Invoice Customers",-25} | Loaded: {inv.Count,6} | RPM: {rpm,4}"
                );
            }

            // =========================
            // 🔹 CREDIT MEMO
            // =========================
            if (_creditMemoCustomerNoCache.TryGetValue(company, out var cm))
            {
                int rpm = cfg.rpm.GetValueOrDefault("GetCreMemoDetails", 0);

                sb.AppendLine(
                    $"  {"Credit Memo Customers",-25} | Loaded: {cm.Count,6} | RPM: {rpm,4}"
                );
            }

            // =========================
            // 🔹 ORDER STATUS
            // =========================
            if (_orderStatusCache.TryGetValue(company, out var os))
            {
                int rpm = cfg.rpm.GetValueOrDefault("OrderStatus", 0);

                sb.AppendLine(
                    $"  {"OrderStatus Pool",-25} | Loaded: {os.Count,6} | RPM: {rpm,4}"
                );
            }

            // =========================
            // 🔹 WEB ORDER (NEU!)
            // =========================
            if (_webOrderPoolCache.TryGetValue(company, out var wp2))
            {
                int rpm = cfg.rpm.GetValueOrDefault("WebOrderCreate", 0);

                sb.AppendLine(
                    $"  {"WebOrders (required)",-25} | Loaded: {wp2.Count,6} | RPM: {rpm,4}"
                );
            }

            sb.AppendLine();
        }

        textbox.Text = sb.ToString();

        form.Show();
    }

    private string NormalizeWorkerName(string worker)
    {
        if (string.IsNullOrWhiteSpace(worker))
            return worker;

        var idx = worker.IndexOf(" (");
        return idx > 0 ? worker.Substring(0, idx) : worker;
    }

    private int ExtractParallelCount(string workerName)
    {
        var start = workerName.IndexOf("(x");
        var end = workerName.IndexOf(")");

        if (start >= 0 && end > start)
        {
            var numberText = workerName.Substring(start + 2, end - start - 2);

            if (int.TryParse(numberText, out int count))
                return count;
        }

        return 1;
    }

    private void ReloadConfig()
    {
        try
        {
            _config = ConfigLoader.Load();

            // 🔥 HttpClient neu erstellen (EXTREM wichtig!)
            _client = new HttpClient(new SocketsHttpHandler
            {
                MaxConnectionsPerServer = _config.maxConnectionsPerServer,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                EnableMultipleHttp2Connections = true
            });

            // 🔥 Caches leeren (DB / Port könnte sich geändert haben!)
            _customerPools.Clear();
            _invoiceCustomerNoCache.Clear();
            _creditMemoCustomerNoCache.Clear();
            _orderStatusCache.Clear();
            _webOrderPoolCache.Clear();

            // 🔥 UI reset
            btnStart.Enabled = false;

            lblStatus.Text = "Config reloaded - reload data required";
            lblStatus.ForeColor = Color.DarkOrange;
        }
        catch (Exception ex)
        {
            lblStatus.Text = "Config reload failed";
            lblStatus.ForeColor = Color.Red;

            MessageBox.Show(ex.Message, "Reload Error");
        }
    }

    private async Task<bool> TestSqlConnectionAsync()
    {
        try
        {
            var connString = _config.connectionString + "Connection Timeout=3;";

            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connString);
            await conn.OpenAsync();

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"SQL connection failed:\n{ex.Message}", "Error");
            return false;
        }
    }

    private async Task<bool> TestApiConnectionAsync()
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(5)
            };

            var url = _config.serviceRoot.TrimEnd('/') + _config.apiRoot;

            var auth = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes($"{_config.username}:{_config.password}")
            );

            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", auth);

            var response = await client.GetAsync(url);

            if (!response.IsSuccessStatusCode)
            {
                MessageBox.Show($"API responded with {response.StatusCode}", "Warning");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"API connection failed:\n{ex.Message}", "Error");
            return false;
        }
    }

    private int ExtractPoolSize(string workerDisplay)
    {
        if (string.IsNullOrWhiteSpace(workerDisplay))
            return int.MaxValue;

        var start = workerDisplay.LastIndexOf("(");
        var end = workerDisplay.LastIndexOf(")");

        if (start >= 0 && end > start)
        {
            var number = workerDisplay.Substring(start + 1, end - start - 1);

            if (int.TryParse(number, out int value))
                return value;
        }

        return int.MaxValue;
    }

    private void LoadTestDashboard_FormClosing(object? sender, FormClosingEventArgs e)
    {
        bool isRunning = _controller != null;

        // 🔥 1. Test läuft → NICHT beenden erlauben
        if (isRunning)
        {
            MessageBox.Show(
                "A test is currently running.\n\nPlease stop the test before closing the application.",
                "Test running",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning
            );

            e.Cancel = true;
            return;
        }

        // 🔥 2. Kein Test → Sicherheitsabfrage
        var result = MessageBox.Show(
            "Do you really want to exit?",
            "Exit",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question
        );

        if (result != DialogResult.Yes)
        {
            e.Cancel = true;
        }
    }

    private void UpdateResourceUsage()
    {
        try
        {
            // 🧠 RAM (MB)
            _process.Refresh(); // 🔥 wichtig!

            double ramMb = _process.PrivateMemorySize64 / 1024.0 / 1024.0;

            // ⚙️ CPU (%)
            double cpu = _cpuCounter.NextValue() / Environment.ProcessorCount;

            // 🌐 Netzwerk (optional simpel: Requests/sec als Proxy)
            var stats = _stats.GetStats();
            double rps = stats.Sum(s => s.Rps);

            // 🔥 Anzeige im Titel
            this.Text =
                $"BC LoadTester | CPU: {cpu:0.0}% | RAM: {ramMb:0} MB | RPS: {rps:0.0}";
        }
        catch
        {
            // ignore (PerformanceCounter kann selten zicken)
        }
    }

    private async Task SaveResultsToDatabaseAsync()
    {
        if (_config == null)
            return;

        try
        {
            var connectionString =
                $"Server={_config.sqlServer},{_config.sqlPort};" +
                $"Database={_config.database};" +
                $"User Id={_config.dbUser};" +
                $"Password={_config.dbPassword};" +
                $"TrustServerCertificate=True;";

            using var conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString);
            await conn.OpenAsync();

            string? currentRun = null;

            var runCmd = conn.CreateCommand();
            runCmd.CommandText = @"
                    SELECT TOP(1) EventClass 
                    FROM [Test Protocol MAC] 
                    WHERE EventClass LIKE 'START%' 
                    ORDER BY [Entry No_] DESC";

            var result = await runCmd.ExecuteScalarAsync();
            currentRun = result?.ToString();
            currentRun ??= "UNKNOWN";

            // =========================
            // 🔥 TABLE NAME (DYNAMIC)
            // =========================
            var tableName = string.IsNullOrWhiteSpace(_config.loadTestTableName)
                ? "BC Loadtest Protocol"
                : _config.loadTestTableName.Trim();

            // 🔒 Minimaler Schutz
            tableName = tableName.Replace("[", "").Replace("]", "");

            // =========================
            // 🔥 CREATE TABLE IF NOT EXISTS
            // =========================
            var createCmd = conn.CreateCommand();
            createCmd.CommandText = $@"
        IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = '{tableName}')
        BEGIN
            CREATE TABLE [{tableName}](
                [Id] INT IDENTITY(1,1) PRIMARY KEY,
                [Act. Run] NVARCHAR(20),
                [Timestamp] DATETIME2,
                [StartTime] DATETIME2,
                [StopTime] DATETIME2,
                [Company] NVARCHAR(100),
                [Worker] NVARCHAR(100),
                [RPM] BIGINT,
                [Requests] BIGINT,
                [Errors] BIGINT,
                [RPS] FLOAT,
                [AvgMs] FLOAT,
                [MaxMs] BIGINT,
                [PoolSize] INT,
                [BigOrders] BIGINT
            )
        END";

            await createCmd.ExecuteNonQueryAsync();

            // =========================
            // 🔥 INSERT DATA
            // =========================
            var stats = _stats.GetStats().ToList();

            int inserted = 0;

            foreach (var s in stats)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = $@"
        INSERT INTO [{tableName}]
        ([Act. Run],[Timestamp],[StartTime],[StopTime],[Company],[Worker],[RPM],[Requests],[Errors],[RPS],[AvgMs],[MaxMs],[PoolSize],[BigOrders])
        VALUES
        (@run,@ts,@start,@stop,@company,@worker,@rpm,@req,@err,@rps,@avg,@max,@pool,@big)";

                cmd.Parameters.AddWithValue("@run", (object?)currentRun ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ts", DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@start", _startTime ?? DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@stop", _stopTime ?? DateTime.UtcNow);
                cmd.Parameters.AddWithValue("@company", s.Company);
                cmd.Parameters.AddWithValue("@worker", s.Worker);
                cmd.Parameters.AddWithValue("@rpm", s.Rpm);
                cmd.Parameters.AddWithValue("@req", s.Requests);
                cmd.Parameters.AddWithValue("@err", s.Errors);
                cmd.Parameters.AddWithValue("@rps", s.Rps);
                cmd.Parameters.AddWithValue("@avg", s.AvgMs);
                cmd.Parameters.AddWithValue("@max", s.MaxMs);
                cmd.Parameters.AddWithValue("@pool", s.PoolSize);

                // 🔥 BigOrders aus custom metrics
                var bigOrders = _stats.GetCustomMetric(s.Worker, s.Company, "BigOrders");
                cmd.Parameters.AddWithValue("@big", bigOrders);

                await cmd.ExecuteNonQueryAsync();
                inserted++;
            }

            MessageBox.Show($"{inserted} Zeilen in [{tableName}] protokolliert", "Save Results");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Fehler beim Speichern:\n{ex.Message}", "DB Error");
        }
    }

    private void btnReset_Click(object sender, EventArgs e)
    {

        if (MessageBox.Show("Reset current results?", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes)
            return;
        // 🔥 Grid leeren            
        statsGrid.Rows.Clear();

        // 🔥 interne Daten löschen
        _allRows.Clear();
        _visibleRows.Clear();

        // 🔥 Stats zurücksetzen
        _stats = new Statistics();

        // 🔥 Zeiten zurücksetzen
        _startTime = null;
        _stopTime = null;
        _controller = null; // 🔥 WICHTIG!

        lblStartTime.Text = "Start: -";
        lblStopTime.Text = "Stop: -";
        lblRuntime.Text = "Runtime: 00:00:00";

        // 🔥 KPI reset
        lblTotalRpm.Text = "Total RPM: 0";
        lblTotalRps.Text = "Total RPS: 0";
        lblTotalRequests.Text = "Total Requests: 0";
        lblTotalErrors.Text = "Total Errors: 0";


        lblStatus.Text = "Reset done";
        lblStatus.ForeColor = Color.Black;

        // 🔥 Buttons
        btnReset.Enabled = false;
        btnStart.Enabled = true;

        if (_runtimeTimer != null)
        {
            _runtimeTimer.Stop();
            _runtimeTimer.Tick -= RuntimeTimer_Tick;
        }
        _remainingMinutes = 60;
        _loadedDurationMinutes = 0;
        numTestDuration.Value = 60;
        numRemainingMinutes.Value = 60;
    }

    private int CalculateConfiguredRpm()
    {
        if (_config == null)
            return 0;

        var enabledWorkers = _config.workers
            .Where(w => w.enabled)
            .Select(w => w.type)
            .ToHashSet();

        return _config.companies
            .Where(c => c.enabled)
            .Sum(c =>
                c.rpm
                    .Where(r => enabledWorkers.Contains(r.Key))
                    .Sum(r => r.Value)
            );
    }
    private void UpdateConfiguredRpmLabel()
    {
        if (lblConfiguredRpm == null)
            return;

        lblConfiguredRpm.Text = $"Configured RPM: {CalculateConfiguredRpm()}";
    }

    private void BuildInitialRows()
    {
        if (_config == null)
            return;

        _allRows.Clear();

        foreach (var company in _config.companies.Where(c => c.enabled))
        {
            bool isExpanded = false;

            // 🔹 Gruppenzeile
            _allRows.Add(new DashboardRow
            {
                Company = company.name,
                IsGroup = true,
                IsExpanded = isExpanded,

                RPM = company.rpm
                    .Where(r => _config.workers.Any(w => w.enabled && w.type == r.Key))
                    .Sum(r => r.Value),
                Requests = 0,
                Errors = 0,
                AvgMs = 0,
                MaxMs = 0
            });

            // 🔹 Worker-Zeilen
            foreach (var worker in _config.workers.Where(w => w.enabled))
            {
                if (!company.rpm.TryGetValue(worker.type, out var rpm))
                    continue;

                // Parallelisierung simulieren wie später
                int parallelWorkers = Math.Clamp(
                    rpm / _config.rpmPerWorker,
                    1,
                    _config.maxWorkersPerType);

                string displayWorker = worker.type;

                if (parallelWorkers > 1)
                    displayWorker += $" (x{parallelWorkers})";

                _workerCounts[(company.name, worker.type)] = parallelWorkers;

                _allRows.Add(new DashboardRow
                {
                    Company = company.name,
                    Worker = worker.type,
                    DisplayWorker = displayWorker,
                    IsGroup = false,

                    RPM = rpm,
                    Requests = 0,
                    Errors = 0,
                    AvgMs = 0,
                    MaxMs = 0
                });
            }
        }

        UpdateVisibleRows();
    }

    private int CalculateRequiredData(string workerType, int rpm, int minutes)
    {
        var baseCount = rpm * minutes;
        var factor = GetBufferFactor(workerType);

        return (int)(baseCount * factor);
    }

    private void UpdatePoolWarnings()
    {
        var stats = _stats.GetStats().ToList();

        // 🔥 alle Worker berücksichtigen (die überhaupt Pool haben)
        var poolStats = stats
            .Where(s => s.PoolSize > 0 && s.Rps > 0)
            .Select(s =>
            {
                double secondsLeft = s.PoolSize / s.Rps;

                return new
                {
                    s.Company,
                    s.Worker,
                    s.PoolSize,
                    s.Rps,
                    SecondsLeft = secondsLeft
                };
            })
            .OrderBy(x => x.SecondsLeft)
            .ToList();

        if (!poolStats.Any())
            return;

        var critical = poolStats.First();

        double minutes = critical.SecondsLeft / 60;

        // 🔥 Status TEXT (immer setzen!)
        lblStatus.Text =
            $"⚠ {critical.Worker} pool empty in {minutes:0.0} min ({critical.Company})";

        // 🔥 Farbe (bestehende Logik + erweitert)
        if (critical.SecondsLeft < 60 || critical.PoolSize < PoolCriticalThreshold)
        {
            lblStatus.ForeColor = Color.Red;
        }
        else if (critical.SecondsLeft < 180 || critical.PoolSize < PoolWarningThreshold)
        {
            lblStatus.ForeColor = Color.DarkOrange;
        }
        else
        {
            lblStatus.ForeColor = Color.Black;
        }
    }

    private void StartRuntimeCountdown()
    {
        // 🔥 alten Timer sauber entfernen
        if (_runtimeTimer != null)
        {
            _runtimeTimer.Stop();
            _runtimeTimer.Tick -= RuntimeTimer_Tick;
        }

        // 🔥 neuen Timer erstellen
        _runtimeTimer = new System.Windows.Forms.Timer
        {
            Interval = 60000
        };

        _runtimeTimer.Tick += RuntimeTimer_Tick;

        _runtimeTimer.Start();
    }

    private void RuntimeTimer_Tick(object? sender, EventArgs e)
    {
        if (_remainingMinutes <= 0)
        {
            _runtimeTimer?.Stop();
            btnStop_Click(this, EventArgs.Empty);
            return;
        }
        _remainingMinutes--;

        if (numRemainingMinutes.Value != _remainingMinutes)
        {
            numRemainingMinutes.Value = _remainingMinutes;
        }

        // 🔥 NACH Update
        UpdateEndTime();
    }

    private void UpdateEndTime()
    {
        if (_startTime == null)
        {
            lblEndTime.Text = "";
            return;
        }

        var endTime = _startTime.Value.AddMinutes(_loadedDurationMinutes);

        lblEndTime.Text = $"Ende: {endTime:HH:mm}";
    }

    private double GetBufferFactor(string workerType)
    {
        var worker = _config.workers.FirstOrDefault(w => w.type == workerType);

        return worker?.bufferFactor > 0 ? worker.bufferFactor : 1.2;
    }

    private long GetConfiguredCompanyRpm(string companyName)
    {
        if (_config == null)
            return 0;

        var company = _config.companies.FirstOrDefault(c => c.name == companyName);
        if (company == null)
            return 0;

        var enabledWorkers = _config.workers
            .Where(w => w.enabled)
            .Select(w => w.type)
            .ToHashSet();

        return company.rpm
            .Where(r => enabledWorkers.Contains(r.Key))
            .Sum(r => (long)r.Value);
    }

}