using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;

namespace BCLoadtester.config;

public static class LoadtestStatsLoader
{
    public static Dictionary<string, double> LoadAvgResponseTimes(AppConfig config, out bool success)
    {
        var result = new Dictionary<string, double>();
        success = false;

        try
        {
            using var conn = new SqlConnection(config.BuildConnectionString());
            conn.Open();

            var cmd = conn.CreateCommand();

            cmd.CommandText = $@"
            SELECT Company, Worker, AVG(AvgMs) as AvgMs
            FROM [{config.loadTestTableName}] (NOLOCK)
            WHERE AvgMs IS NOT NULL
            GROUP BY Company, Worker
        ";

            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                var company = reader["Company"]?.ToString() ?? "";
                var worker = reader["Worker"]?.ToString() ?? "";

                if (string.IsNullOrWhiteSpace(company) || string.IsNullOrWhiteSpace(worker))
                    continue;

                var avgMs = reader["AvgMs"] != DBNull.Value
                    ? Convert.ToDouble(reader["AvgMs"])
                    : 100;

                var key = $"{company}|{worker}";
                result[key] = avgMs;
            }

            success = true; // 🔥 Erfolg setzen
        }
        catch
        {
            success = false;
        }

        return result;
    }
}