using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using BCLoadtester.Loadtest;

namespace BCLoadtester;

public class WorkerFactoryResult
{
    public List<IWorker> Workers { get; set; } = new();
    public Dictionary<(string Company, string Worker), int> WorkerCounts { get; set; } = new();
}

public static class WorkerFactory
{
    public static WorkerFactoryResult CreateWorkers(
        AppConfig config,
        HttpClient client,
        WorkerDataContext data,
        StatisticsService stats,
        Dictionary<(string Company, string Worker), int> dynamicConcurrency)
    {
        var result = new WorkerFactoryResult();

        foreach (var company in config.companies.Where(c => c.enabled))
        {
            var (serviceRoot, apiRoot) = GetApiConfig(config, company);

            foreach (var worker in config.workers.Where(w => w.enabled))
            {
                if (!company.rpm.TryGetValue(worker.type, out var rpm) || rpm <= 0)
                    continue;

                var key = (company.name, worker.type);

                // =========================
                // 🔹 Daten auflösen
                // =========================
                var context = ResolveWorkerContext(company, worker, data);
                if (!context.IsValid)
                    continue;

                // =========================
                // 🔹 Parallelisierung
                // =========================
                int parallelWorkers = Math.Clamp(
                    (int)Math.Ceiling((double)rpm / config.rpmPerWorker),
                    1,
                    config.maxWorkersPerType);

                int workerRpm = (int)Math.Ceiling((double)rpm / parallelWorkers);

                Func<int> concurrencyFunc = () =>
                {
                    if (dynamicConcurrency.TryGetValue(key, out var val))
                        return val;

                    return ResolveConcurrency(config, worker);
                };

                // =========================
                // 🔹 Instanziierung
                // =========================
                int created = 0;

                for (int i = 0; i < parallelWorkers; i++)
                {
                    var instance = CreateWorkerInstance(
                        worker.type,
                        client,
                        context,
                        serviceRoot,
                        apiRoot,
                        worker,
                        company,
                        workerRpm,
                        stats,
                        concurrencyFunc);

                    // sollte nie null sein (fail-fast), aber defensiv
                    if (instance != null)
                    {
                        result.Workers.Add(instance);
                        created++;
                    }
                }

                // 🔥 garantiert konsistent mit tatsächlich erzeugten Workern
                if (created > 0)
                    result.WorkerCounts[key] = created;
            }
        }

        return result;
    }

    // =========================================================
    // 🔹 Context Resolver
    // =========================================================
    private static WorkerContext ResolveWorkerContext(
        Company company,
        WorkerConfig worker,
        WorkerDataContext data)
    {
        var ctx = new WorkerContext();

        var key = (company.name, worker.type);

        switch (worker.type)
        {
            case WorkerTypes.EmailSearch:
            case WorkerTypes.PMCSearch:
            case WorkerTypes.CustomerCreate:
            case WorkerTypes.CreateShipToAddress:
            case WorkerTypes.CustomerHistory:
                if (!data.CustomerPools.TryGetValue(key, out var customers) || customers.Count == 0)
                    return WorkerContext.Invalid;

                ctx.Customers = customers;
                break;

            case WorkerTypes.GetInvoiceDetails:
                if (!data.InvoiceCustomerNoCache.TryGetValue(company.name, out var inv) || inv.Count == 0)
                    return WorkerContext.Invalid;

                ctx.InvoiceCustomers = inv;
                break;

            case WorkerTypes.GetCreMemoDetails:
                if (!data.CreditMemoCustomerNoCache.TryGetValue(company.name, out var cm) || cm.Count == 0)
                    return WorkerContext.Invalid;

                ctx.CreditMemoCustomers = cm;
                break;

            case WorkerTypes.OrderStatus:
                if (!data.OrderStatusCache.TryGetValue(company.name, out var os) || os.Count == 0)
                    return WorkerContext.Invalid;

                ctx.OrderStatusPool = os;
                break;

            case WorkerTypes.WebOrderCreate:
                if (!data.WebOrderPoolCache.TryGetValue(company.name, out var wo))
                    return WorkerContext.Invalid;

                if (!data.OrderStatusCache.TryGetValue(company.name, out var osPool))
                    return WorkerContext.Invalid;

                ctx.WebOrderPool = wo;
                ctx.OrderStatusPool = osPool;
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown worker type '{worker.type}' in ResolveWorkerContext");
        }

        ctx.IsValid = true;
        return ctx;
    }

    // =========================================================
    // 🔹 Worker Factory
    // =========================================================
    private static IWorker CreateWorkerInstance(
        string type,
        HttpClient client,
        WorkerContext ctx,
        string serviceRoot,
        string apiRoot,
        WorkerConfig worker,
        Company company,
        int workerRpm,
        StatisticsService stats,
        Func<int> concurrencyFunc)
    {
        return type switch
        {
            WorkerTypes.EmailSearch => new EmailSearchWorker(
                client, ctx.Customers!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.PMCSearch => new PhoneticSearchWorker(
                client, ctx.Customers!,
                serviceRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.CustomerCreate => new CustomerCreateWorker(
                client, ctx.Customers!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.CreateShipToAddress => new ShipToAddressCreateWorker(
                client, ctx.Customers!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.CustomerHistory => new CustomerHistoryWorker(
                client,
                ctx.Customers!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.WebOrderCreate => new WebOrderCreateWorker(
                client,
                ctx.OrderStatusPool!,
                ctx.WebOrderPool!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats,
                type,
                concurrencyFunc,
                company.webOrderConfig?.bigOrderLines ?? 0,
                company.webOrderConfig?.bigOrderIntervalMinutes ?? 0,
                company.webOrderConfig?.promotionMediumNo,
                company.webOrderConfig?.promotionMediumTrgGrpNo,
                company.webOrderConfig?.shippingChargeAmount ?? 0
            ),

            WorkerTypes.GetInvoiceDetails => new GetInvoiceDetailsWorker(
                client,
                ctx.InvoiceCustomers!,
                serviceRoot, apiRoot,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.GetCreMemoDetails => new GetCreMemoDetailsWorker(
                client,
                ctx.CreditMemoCustomers!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            WorkerTypes.OrderStatus => new OrderStatusWorker(
                client,
                ctx.OrderStatusPool!,
                serviceRoot, apiRoot,
                worker.endpoint,
                company.guid, company.name,
                workerRpm,
                stats, type, concurrencyFunc),

            _ => throw new InvalidOperationException(
                $"Unknown worker type '{type}' for company '{company.name}'")
        };
    }

    // =========================================================
    // 🔹 Helper
    // =========================================================
    private static (string serviceRoot, string apiRoot) GetApiConfig(AppConfig config, Company company)
    {
        var serviceRoot = string.IsNullOrWhiteSpace(company.serviceRoot)
            ? config.serviceRoot
            : company.serviceRoot;

        var apiRoot = string.IsNullOrWhiteSpace(company.apiRoot)
            ? config.apiRoot
            : company.apiRoot;

        return (serviceRoot, apiRoot);
    }

    private static int ResolveConcurrency(AppConfig config, WorkerConfig worker)
    {
        if (worker.maxConcurrency.HasValue && worker.maxConcurrency.Value > 0)
            return worker.maxConcurrency.Value;

        if (config.maxConcurrencyPerWorker > 0)
            return config.maxConcurrencyPerWorker;

        return 20;
    }

    // =========================================================
    // 🔹 Worker Type Constants (kein String-Wildwuchs mehr)
    // =========================================================
    private static class WorkerTypes
    {
        public const string EmailSearch = "EmailSearch";
        public const string PMCSearch = "PMCSearch";
        public const string CustomerCreate = "CustomerCreate";
        public const string CreateShipToAddress = "CreateShipToAddress";
        public const string CustomerHistory = "CustomerHistory";
        public const string WebOrderCreate = "WebOrderCreate";
        public const string GetInvoiceDetails = "GetInvoiceDetails";
        public const string GetCreMemoDetails = "GetCreMemoDetails";
        public const string OrderStatus = "OrderStatus";
    }

    // =========================================================
    // 🔹 Context Object
    // =========================================================
    private class WorkerContext
    {
        public bool IsValid { get; set; }

        public List<CustomerEntry>? Customers { get; set; }
        public List<string>? InvoiceCustomers { get; set; }
        public List<string>? CreditMemoCustomers { get; set; }
        public OrderStatusPool? OrderStatusPool { get; set; }
        public WebOrderPayloadPool? WebOrderPool { get; set; }

        public static WorkerContext Invalid => new WorkerContext { IsValid = false };
    }
}