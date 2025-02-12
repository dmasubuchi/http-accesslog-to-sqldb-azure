using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace FilterFunction
{
    public class FilterLogs
    {
        private readonly FilterConfig _filterConfig;

        public FilterLogs()
        {
            // FilterConfig を環境変数から読み込む
            string configJson = Environment.GetEnvironmentVariable("FilterConfig");
            _filterConfig = string.IsNullOrEmpty(configJson)
                ? new FilterConfig()
                : JsonSerializer.Deserialize<FilterConfig>(configJson);
        }

        [FunctionName("FilterLogs")]
        public async Task Run(
            [BlobTrigger("raw-logs/{name}.zip")] Stream zipStream,
            [Blob("filtered-logs/out_{name}.zip", FileAccess.Write)] Stream outputStream,
            string name,
            ILogger log)
        {
            try
            {
                log.LogInformation($"Started processing zip file: {name}");
                int totalLines = 0;
                int filteredLines = 0;

                using (var outputZip = new ZipArchive(outputStream, ZipArchiveMode.Create, true))
                using (var inputZip = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    // 出力用ZIPに "filtered.log" という名前で書き込み
                    var outputEntry = outputZip.CreateEntry("filtered.log");
                    using (var outputWriter = new StreamWriter(outputEntry.Open()))
                    {
                        foreach (var entry in inputZip.Entries)
                        {
                            log.LogInformation($"Processing entry: {entry.FullName}");

                            using (var reader = new StreamReader(entry.Open()))
                            {
                                string line;
                                while ((line = await reader.ReadLineAsync()) != null)
                                {
                                    totalLines++;
                                    if (ShouldProcessLine(line))
                                    {
                                        await outputWriter.WriteLineAsync(line);
                                        filteredLines++;
                                    }
                                }
                            }
                        }
                    }
                }

                log.LogInformation($"Completed processing file {name}. " +
                    $"Total lines: {totalLines}, Filtered lines: {filteredLines}");
            }
            catch (Exception ex)
            {
                log.LogError($"Error processing file {name}: {ex.Message}");
                log.LogError(ex.StackTrace);
                throw;
            }
        }

        private bool ShouldProcessLine(string line)
        {
            // 例えばHTTPステータス200を含む行だけをフィルタしたい
            if (!line.Contains(" 200 ")) return false;

            // 画像拡張子や静的リソースを除外
            foreach (var ext in _filterConfig.ExcludeExtensions)
            {
                if (line.Contains(ext)) return false;
            }

            // 特定パスを除外
            foreach (var path in _filterConfig.ExcludePaths)
            {
                if (line.Contains(path)) return false;
            }

            return true;
        }
    }

    public class FilterConfig
    {
        public string[] ExcludeExtensions { get; set; } = new[]
        {
            ".jpg", ".jpeg", ".png", ".gif", ".css", ".js"
        };

        public string[] ExcludePaths { get; set; } = new[]
        {
            "/favicon.ico",
            "/robots.txt"
        };
    }
}

