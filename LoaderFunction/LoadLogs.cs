using System;
using System.IO;
using System.IO.Compression;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace LoaderFunction
{
    public class LoadLogs
    {
        private readonly string _connectionString;

        public LoadLogs()
        {
            _connectionString = Environment.GetEnvironmentVariable("SqlConnectionString")
                ?? throw new ArgumentNullException("SqlConnectionString environment variable is not set");
        }

        [FunctionName("LoadLogs")]
        public async Task Run(
            [BlobTrigger("filtered-logs/out_{name}.zip")] Stream zipStream,
            string name,
            ILogger log)
        {
            try
            {
                log.LogInformation($"Started processing zip file: {name}");
                var startTime = DateTime.UtcNow;
                int totalEntries = 0;
                int successEntries = 0;
                int errorEntries = 0;

                using (var inputZip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    foreach (var entry in inputZip.Entries)
                    {
                        log.LogInformation($"Processing entry: {entry.FullName}");

                        using (var reader = new StreamReader(entry.Open()))
                        using (var connection = new SqlConnection(_connectionString))
                        {
                            await connection.OpenAsync();

                            // DataTable準備
                            var dataTable = new DataTable();
                            dataTable.Columns.Add("Timestamp", typeof(DateTime));
                            dataTable.Columns.Add("IpAddress", typeof(string));
                            dataTable.Columns.Add("Method", typeof(string));
                            dataTable.Columns.Add("Url", typeof(string));
                            dataTable.Columns.Add("StatusCode", typeof(int));
                            dataTable.Columns.Add("BytesSent", typeof(long));
                            dataTable.Columns.Add("Referer", typeof(string));
                            dataTable.Columns.Add("UserAgent", typeof(string));
                            dataTable.Columns.Add("ProcessedDate", typeof(DateTime)); 

                            string line;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                try
                                {
                                    var logEntry = ParseLogLine(line);
                                    if (logEntry != null)
                                    {
                                        dataTable.Rows.Add(
                                            logEntry.Timestamp,
                                            logEntry.IpAddress,
                                            logEntry.Method,
                                            logEntry.Url,
                                            logEntry.StatusCode,
                                            logEntry.BytesSent,
                                            logEntry.Referer,
                                            logEntry.UserAgent,
                                            DateTime.UtcNow
                                        );
                                        successEntries++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    errorEntries++;
                                    log.LogError($"Error parsing line: {line}. Error: {ex.Message}");
                                }
                                totalEntries++;
                            }

                            if (dataTable.Rows.Count > 0)
                            {
                                await BulkInsertLogEntries(connection, dataTable, log);
                            }
                        }
                    }
                }

                var duration = DateTime.UtcNow - startTime;
                log.LogInformation($"Completed processing file {name}. " +
                    $"Total: {totalEntries}, Success: {successEntries}, Errors: {errorEntries}, Duration: {duration.TotalSeconds:F2}s");
            }
            catch (Exception ex)
            {
                log.LogError($"Critical error processing file {name}: {ex.Message}");
                throw;
            }
        }

        private LogEntry ParseLogLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return null;

            var regex = new Regex(
                @"^(\S{1,15}) \S+ \S+ \[([\w:/]+\s[+\-]\d{4})\] ""(\S{1,10}) ([^""]*) (\S{1,10})"" (\d{3}) (\d+) ""([^""]*)"" ""([^""]*)"""
            );
            var match = regex.Match(line);

            if (!match.Success) return null;

            try
            {
                if (!DateTime.TryParseExact(match.Groups[2].Value,
                                            "dd/MMM/yyyy:HH:mm:ss zzz",
                                            CultureInfo.InvariantCulture,
                                            DateTimeStyles.AdjustToUniversal,
                                            out DateTime timestamp))
                    return null;

                if (!int.TryParse(match.Groups[6].Value, out int statusCode)) return null;
                if (!long.TryParse(match.Groups[7].Value, out long bytesSent)) return null;

                return new LogEntry
                {
                    Timestamp  = timestamp,
                    IpAddress  = match.Groups[1].Value.Truncate(45),
                    Method     = match.Groups[3].Value.Truncate(10),
                    Url        = match.Groups[4].Value.Truncate(2048),
                    Protocol   = match.Groups[5].Value.Truncate(10),
                    StatusCode = statusCode,
                    BytesSent  = bytesSent,
                    Referer    = match.Groups[8].Value.Truncate(2048),
                    UserAgent  = match.Groups[9].Value.Truncate(500)
                };
            }
            catch (Exception ex)
            {
                // パース中の例外はログ出力してnullを返す
                Console.WriteLine($"Error parsing line: {line}. Error: {ex.Message}");
                return null;
            }
        }

        private async Task BulkInsertLogEntries(SqlConnection connection, DataTable dataTable, ILogger log)
        {
            // ここが切れていた部分を追記
            using (var transaction = connection.BeginTransaction())
            {
                try
                {
                    // SqlBulkCopyを使ってHttpLogsに対して一括挿入する例
                    using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, transaction))
                    {
                        bulkCopy.DestinationTableName = "HttpLogs";
                        bulkCopy.ColumnMappings.Add("Timestamp", "Timestamp");
                        bulkCopy.ColumnMappings.Add("IpAddress", "IpAddress");
                        bulkCopy.ColumnMappings.Add("Method", "Method");
                        bulkCopy.ColumnMappings.Add("Url", "Url");
                        bulkCopy.ColumnMappings.Add("StatusCode", "StatusCode");
                        bulkCopy.ColumnMappings.Add("BytesSent", "BytesSent");
                        bulkCopy.ColumnMappings.Add("Referer", "Referer");
                        bulkCopy.ColumnMappings.Add("UserAgent", "UserAgent");
                        bulkCopy.ColumnMappings.Add("ProcessedDate", "ProcessedDate");

                        await bulkCopy.WriteToServerAsync(dataTable);
                    }
                    await transaction.CommitAsync();
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    log.LogError($"Error in bulk insert: {ex.Message}");
                    throw;
                }
            }
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string IpAddress { get; set; }
        public string Method { get; set; }
        public string Url { get; set; }
        public string Protocol { get; set; }
        public int StatusCode { get; set; }
        public long BytesSent { get; set; }
        public string Referer { get; set; }
        public string UserAgent { get; set; }
    }

    public static class StringExtensions
    {
        public static string Truncate(this string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return value;
            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
