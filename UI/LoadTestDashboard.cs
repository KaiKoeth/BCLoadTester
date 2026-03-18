namespace BCLoadtester;
using BCLoadtester.Loadtest;
using System.Linq;

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

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // TopBar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // StatsBar
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid

            Controls.Add(layout);

            // =========================
            // 🔷 TOP BAR
            // =========================
            var topBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            btnSetup = new Button { Text = "⚙ Setup", Width = 110 };
            btnLoad = new Button { Text = "📥 Load Data", Width = 120 };
            btnShowData = new Button { Text = "📊 Show Data", Width = 120 };

            btnStart = new Button { Text = "▶ Start", Width = 100, BackColor = Color.LightGreen };
            btnStop = new Button { Text = "■ Stop", Width = 100, BackColor = Color.IndianRed };

            btnStart.Enabled = false;
            btnStop.Enabled = false;

            lblStatus = new Label
            {
                Text = "Idle",
                AutoSize = true,
                Padding = new Padding(20, 10, 0, 0)
            };

            progressLoading = new ProgressBar
            {
                Width = 150,
                Visible = false
            };

            // Events (UNVERÄNDERT)
            btnStart.Click += btnStart_Click;
            btnStop.Click += btnStop_Click;
            btnSetup.Click += (s, e) => new SetupForm(_config).ShowDialog(this);
            btnLoad.Click += async (s, e) => await LoadDataAsync();
            btnShowData.Click += btnShowData_Click;

            topBar.Controls.Add(btnSetup);
            topBar.Controls.Add(btnLoad);
            topBar.Controls.Add(btnShowData);

            topBar.Controls.Add(new Label { Width = 40 });

            topBar.Controls.Add(btnStart);
            topBar.Controls.Add(btnStop);

            topBar.Controls.Add(new Label { Width = 40 });

            topBar.Controls.Add(lblStatus);
            topBar.Controls.Add(progressLoading);

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
            layout.Controls.Add(topBar, 0, 0);
            layout.Controls.Add(statsBar, 0, 1);
            layout.Controls.Add(statsGrid, 0, 2);

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
                                    _stats, workerDisplayName));
                                break;

                            case "PMCSearch":
                                if (customers.Count == 0) break;

                                workers.Add(new PhoneticSearchWorker(
                                    _client, customers,
                                    _config.serviceRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerDisplayName));
                                break;

                            case "CustomerCreate":
                                if (customers.Count == 0) break;

                                workers.Add(new CustomerCreateWorker(
                                    _client, customers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerDisplayName));
                                break;

                            case "CreateShipToAddress":
                                if (customers.Count == 0) break;
                                workers.Add(new ShipToAddressCreateWorker(
                                    _client, customers,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerDisplayName));
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
                                    _stats, workerDisplayName));
                                    break;

                            case "GetInvoiceDetails":
                                if (invoiceCustomers.Count == 0) break;

                                workers.Add(new GetInvoiceDetailsWorker(
                                    _client,
                                    invoiceCustomers,
                                    _config.serviceRoot,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerDisplayName));
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
                                    _stats, workerDisplayName));
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
                                    _stats, workerDisplayName));
                                break;

                            case "OrderStatus":
                                workers.Add(new OrderStatusWorker(
                                    _client,
                                    orderStatusPool,
                                    _config.serviceRoot, _config.apiRoot,
                                    worker.endpoint,
                                    company.guid, company.name,
                                    workerRpm,
                                    _stats, workerDisplayName));
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
                    company.webOrderConfig.payloadPoolSize
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
                string workerName = w.Worker;

                // 🔥 Pool nur für OrderStatus anhängen
                if (workerName.StartsWith("OrderStatus") && w.PoolSize > 0)
                {
                    workerName += $"({w.PoolSize})";
                }

                _allRows.Add(new DashboardRow
                {
                    Company = companyName,
                    Worker = workerName,
                    IsGroup = false,

                    RPM = ExtractTotalRpm(w.Worker, w.Rpm), // bleibt korrekt
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
                : ""; // 🔥 leer für Worker

            string workerDisplay = row.IsGroup
                ? ""
                : "    " + row.Worker;

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

            // 🔹 Bold für Gruppen
            if (row.IsGroup)
            {
                statsGrid.Rows[rowIndex].DefaultCellStyle.Font =
                    new Font(statsGrid.Font, FontStyle.Bold);
            }

            // 🔴 Fehler einfärben
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
            if (e.ColumnIndex == 4 && !row.IsGroup) // Column 4 = Errors
            {
                var errors = _stats.GetErrors(row.Worker, row.Company);

                if (errors.Count == 0)
                {
                    MessageBox.Show("No error details available.");
                    return;
                }

                var text = string.Join(Environment.NewLine,
                    errors.OrderByDescending(x => x.Value)
                        .Select(x => $"{x.Key}: {x.Value}"));

                MessageBox.Show(text, $"Errors - {row.Worker}");
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

            sb.AppendLine();
        }

        textbox.Text = sb.ToString();

        form.Show();
    }
}