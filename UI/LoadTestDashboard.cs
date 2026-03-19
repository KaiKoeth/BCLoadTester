namespace BCLoadtester;
using BCLoadtester.Loadtest;
using System.Linq;
using BCLoadtester.config;

public partial class LoadTestDashboard : Form
{
    LoadController? _controller;
    AppConfig? _config;
    HttpClient _client;
    Button btnStart;
    Button btnStop;
    Button btnSetup;
    Button btnLoad;
    Button btnShowData;
    Statistics _stats = new Statistics();
    DataGridView statsGrid;
    Label lblStartTime;
    Label lblStopTime;
    Label lblRuntime;
    DateTime? _startTime;
    DateTime? _stopTime;
    Label lblTotalRps;
    Label lblTotalRequests;
    Label lblTotalErrors;
    Label lblStatus;
    ProgressBar progressLoading;

    private List<DashboardRow> _allRows = new();
    private List<DashboardRow> _visibleRows = new();

    // ✅ SAUBERE CACHES
    private Dictionary<string, List<CustomerEntry>> _customersCache = new();
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

            this.MinimumSize = new Size(1000, 600);
            this.Size = new Size(1300, 800);

            // =========================
            // 🔷 MAIN LAYOUT (FIX!)
            // =========================
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // TopBar 1
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // TopBar 2
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // TopBar3
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // StatsBar
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

            btnStart = new Button { Text = "▶ Start", Width = 120,Height = 60, BackColor = Color.LightGreen };
            btnStop = new Button { Text = "■ Stop", Width = 120, Height = 60, BackColor = Color.IndianRed };

            btnStart.Enabled = false;
            btnStop.Enabled = false;

            // 🔥 MARGINS RESET → wichtig für perfekte Ausrichtung
            btnSetup.Margin = new Padding(0, 0, 10, 0);
            btnLoad.Margin = new Padding(0, 0, 10, 0);
            btnShowData.Margin = new Padding(0, 0, 10, 0);

            btnStart.Margin = new Padding(0, 0, 10, 0);
            btnStop.Margin = new Padding(0, 0, 10, 0);

            // =========================
            // 🔷 EVENTS (🔥 WICHTIG!)
            // =========================
            btnStart.Click += btnStart_Click;
            btnStop.Click += btnStop_Click;

            btnSetup.Click += (s, e) =>
            {
                if (_config == null)
                {
                    MessageBox.Show("Config not loaded.");
                    return;
                }

                new SetupSelectionForm(_config).ShowDialog(this);
                ReloadConfig();
            };

            btnLoad.Click += async (s, e) => await LoadDataAsync();
            btnShowData.Click += btnShowData_Click;

            // =========================
            // 🔷 STATUS
            // =========================
            lblStatus = new Label
            {
                Text = "Idle",
                AutoSize = true,
                Padding = new Padding(20, 10, 0, 0),
                Font = new Font("Segoe UI", 9, FontStyle.Bold) // optional schöner
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

            leftPanel.Controls.Add(btnLoad);
            leftPanel.Controls.Add(btnShowData);

            // 🔹 Setup rechts
            btnSetup.Margin = new Padding(0);

            // 🔹 Einfügen
            topBar1.Controls.Add(leftPanel, 0, 0);
            topBar1.Controls.Add(new Panel(), 1, 0);
            topBar1.Controls.Add(btnSetup, 2, 0);


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
            var leftPanel2 = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                WrapContents = false,
                Margin = new Padding(0)
            };

            btnStart.Margin = new Padding(0, 0, 10, 0);
            btnStop.Margin = new Padding(0);

            leftPanel2.Controls.Add(btnStart);
            leftPanel2.Controls.Add(btnStop);

            topBar2.Controls.Add(leftPanel2, 0, 0);


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

            lblTotalRps = new Label { Text = "RPS: 0", Width = 120, Font = new Font("Segoe UI", 9, FontStyle.Bold) };
            lblTotalRequests = new Label { Width = 160 };
            lblTotalErrors = new Label { Width = 140 };

            statsBar.Controls.Add(lblStartTime);
            statsBar.Controls.Add(lblStopTime);
            statsBar.Controls.Add(lblRuntime);

            statsBar.Controls.Add(new Label { Width = 30 });

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
            layout.Controls.Add(topBar1, 0, 0);
            layout.Controls.Add(topBar2, 0, 1);
            layout.Controls.Add(topBar3, 0, 2);
            layout.Controls.Add(statsBar, 0, 2);
            layout.Controls.Add(statsGrid, 0, 3);

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

                var stats = _stats.GetStats()
                    .OrderBy(s => s.Company)
                    .ThenBy(s => s.Worker)
                    .ToList();

                BuildRows(stats);
                RefreshGrid(runtime);
            };
            timer.Start();
        }

    private async void btnStart_Click(object sender, EventArgs e)
    {

        if (_config == null || _customersCache.Count == 0)
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

        foreach (var company in enabledCompanies)
        {
            if (!_customersCache.TryGetValue(company.name, out var customers) ||
                !_invoiceCustomerNoCache.TryGetValue(company.name, out var invoiceCustomers) ||
                !_creditMemoCustomerNoCache.TryGetValue(company.name, out var creditMemoCustomers) ||
                !_orderStatusCache.TryGetValue(company.name, out var orderStatusPool) ||
                !_webOrderPoolCache.TryGetValue(company.name, out var webOrderPool))
            {
                lblStatus.Text = $"Skipping company {company.name} (missing data)";
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
                                if (customers.Count == 0) break;

                                workers.Add(new EmailSearchWorker(
                                    _client, customers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "PMCSearch":
                                if (customers.Count == 0) break;

                                workers.Add(new PhoneticSearchWorker(
                                    _client, customers,
                                    _config.serviceRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "CustomerCreate":
                                if (customers.Count == 0) break;

                                workers.Add(new CustomerCreateWorker(
                                    _client, customers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "CreateShipToAddress":
                                if (customers.Count == 0) break;
                                workers.Add(new ShipToAddressCreateWorker(
                                    _client, customers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "WebOrderCreate":
                                workers.Add(new WebOrderCreateWorker(
                                    _client,
                                    orderStatusPool,
                                    webOrderPool,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                    break;

                            case "GetInvoiceDetails":
                                if (invoiceCustomers.Count == 0) break;

                                workers.Add(new GetInvoiceDetailsWorker(
                                    _client,
                                    invoiceCustomers,
                                    _config.serviceRoot,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "GetCreMemoDetails":
                                if (creditMemoCustomers.Count == 0) break;

                                workers.Add(new GetCreMemoDetailsWorker(
                                    _client,
                                    creditMemoCustomers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "CustomerHistory":
                                if (customers.Count == 0) break;

                                workers.Add(new CustomerHistoryWorker(
                                    _client,
                                    customers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerKey));
                                break;

                            case "OrderStatus":
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

        _controller = new LoadController();
        _controller.Start(workers);

        lblStatus.Text = "Test running";
    }

    private void btnStop_Click(object sender, EventArgs e)
    {
        _stopTime = DateTime.Now;
        _controller?.Stop();
        lblStatus.Text = "Test stopped";
        lblStopTime.Text = $"Stop: {_stopTime:HH:mm:ss}";
        SetUiState(false);
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

        try
        {
            btnStart.Enabled = false;
            btnStop.Enabled = false;
            btnSetup.Enabled = false;
            btnLoad.Enabled = false;

            lblStatus.ForeColor = Color.DarkOrange;

            var customerProvider = new CustomerDataProvider(_config.connectionString);
            var invoiceProvider = new InvoiceCustomerDataProvider(_config.connectionString);
            var creditProvider = new CreditMemoCustomerDataProvider(_config.connectionString);
            var orderProvider = new OrderStatusCustomerDataProvider(_config.connectionString);
            var webOrderProvider = new WebOrderDataProvider(_config.connectionString);

            var companies = _config.companies.Where(c => c.enabled).ToList();

            _totalCompanies = companies.Count;
            _loadedCompanies = 0;

            progressLoading.Visible = true;
            progressLoading.Style = ProgressBarStyle.Marquee;
            progressLoading.MarqueeAnimationSpeed = 30;

            foreach (var company in companies)
            {
                lblStatus.Text = $"Loading data ({company.name})...";
                Application.DoEvents();

                // 🔹 Customers
                _customersCache[company.name] =
                    await customerProvider.LoadCustomers(company.name);

                // 🔹 Invoice (List<string>)
                _invoiceCustomerNoCache[company.name] =
                    await invoiceProvider.LoadCustomers(company.name);

                // 🔹 CreditMemo (List<string>)
                _creditMemoCustomerNoCache[company.name] =
                    await creditProvider.LoadCustomers(company.name);

                // 🔹 OrderStatus
                var orderCustomers =
                    await orderProvider.LoadCustomers(company.name);

                var pool = new OrderStatusPool();
                pool.AddRange(orderCustomers);
                _orderStatusCache[company.name] = pool;

                // 🔹 WebOrder
                var profile = await webOrderProvider.LoadProfile(company.name);

                _webOrderPoolCache[company.name] = new WebOrderPayloadPool(
                    company.name,
                    profile,
                    company.webOrderConfig.minLines,
                    company.webOrderConfig.maxLines,
                    company.webOrderConfig.WeborderPoolSize
                );

                // 🔥 Fortschritt
                _loadedCompanies++;                
            }

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

        btnStart.Enabled = true;
        btnStop.Enabled = false;
        btnSetup.Enabled = true;
        btnLoad.Enabled = true;
    }

    private void BuildRows(IEnumerable<(string Worker, string Company, long Rpm, long Requests, long Errors, double Rps, int PoolSize, double AvgMs, long MaxMs)> stats)
        {
            var previousState = _allRows
                .Where(r => r.IsGroup)
                .ToDictionary(r => r.Company, r => r.IsExpanded);

            _allRows.Clear();

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

                    RPM = companyGroup.Sum(x => ExtractTotalRpm(x.Worker, x.Rpm)),
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

                    // ✅ Pool anzeigen
                    if (w.Worker == "OrderStatus" && w.PoolSize > 0)
                    {
                        displayWorker += $" ({w.PoolSize})";
                    }

                    _allRows.Add(new DashboardRow
                    {
                        Company = companyName,

                        // 🔥 CLEAN KEY (für Stats!)
                        Worker = w.Worker,

                        // 🔥 NUR Anzeige
                        DisplayWorker = displayWorker,

                        IsGroup = false,

                        RPM = ExtractTotalRpm(w.Worker, w.Rpm),
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
    }   

    private void btnShowData_Click(object sender, EventArgs e)
    {
        if (_customersCache.Count == 0)
        {
            MessageBox.Show("No data loaded.");
            return;
        }

        var form = new Form
        {
            Text = "Loaded Data",
            Width = 800,
            Height = 600
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

        foreach (var company in _customersCache.Keys)
        {
            sb.AppendLine($"=== {company} ===");

            sb.AppendLine($"Customers: {_customersCache[company].Count}");

            if (_invoiceCustomerNoCache.TryGetValue(company, out var inv))
                sb.AppendLine($"Invoice Customers: {inv.Count}");

            if (_creditMemoCustomerNoCache.TryGetValue(company, out var cm))
                sb.AppendLine($"Credit Memo Customers: {cm.Count}");

            if (_orderStatusCache.TryGetValue(company, out var os))
                sb.AppendLine($"OrderStatus Pool: {os.Count}");

            if (_webOrderPoolCache.TryGetValue(company, out var wp))
                sb.AppendLine($"Weborder Pool: {wp.Count}");
            

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
            _customersCache.Clear();
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

    
}