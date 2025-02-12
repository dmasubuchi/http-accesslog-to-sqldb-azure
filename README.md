
## Overview

1. **FilterFunction**  
   - Monitors the `raw-logs` container for new `.zip` files (HTTP access logs).  
   - Extracts and filters log lines (e.g., only status code 200).  
   - Writes the filtered logs as a new zip file into the `filtered-logs` container.

2. **LoaderFunction**  
   - Monitors the `filtered-logs` container for new `.zip` files (produced by FilterFunction).  
   - Extracts each log line, parses them via regex, and then bulk-inserts them into an Azure SQL Database table.

**High-Level Flow**  
```
Blob (raw-logs)   →   FilterFunction   →   Blob (filtered-logs)   →   LoaderFunction   →   SQL Database
```

## Features

- **Blob Trigger**: Automatically fires on new `.zip` uploads.  
- **Flexible Filtering**: Easily customize which lines are included, which extensions or paths are excluded.  
- **Regex Parsing**: Extract IP addresses, timestamps, HTTP methods, URLs, etc.  
- **Bulk Insert**: Efficiently loads large volumes of logs into SQL DB with `SqlBulkCopy`.  
- **Configurable via Environment Variables** (e.g., `FilterConfig`, `SqlConnectionString`).

## Prerequisites

- Azure Subscription  
- Azure Storage Account (for `raw-logs` and `filtered-logs` containers)  
- Azure SQL Database (with a table for logs, e.g. `HttpLogs`)  
- [Azure Functions Core Tools](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local) (for local dev/testing)  
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)

## Setup & Deployment

1. **Clone this Repository**

   ```bash
   git clone https://github.com/your-organization/azure-zip-log-pipeline.git
   cd azure-zip-log-pipeline
   ```

2. **Create and Configure Azure Resources**  
   - **Storage Account**: Create one and set up two containers: `raw-logs` and `filtered-logs`.  
   - **SQL Database**: Create the table, for example:
     ```sql
     CREATE TABLE [dbo].[HttpLogs] (
         [Id] INT IDENTITY(1,1) PRIMARY KEY,
         [Timestamp] DATETIME2,
         [IpAddress] NVARCHAR(45),
         [Method] NVARCHAR(10),
         [Url] NVARCHAR(2048),
         [StatusCode] INT,
         [BytesSent] BIGINT,
         [Referer] NVARCHAR(2048),
         [UserAgent] NVARCHAR(500),
         [ProcessedDate] DATETIME2
     );
     ```
   - **Allow Azure Services**: Configure firewall rules for your SQL Server.

3. **Set Up Environment Variables**  
   - **`FilterConfig`** (JSON string for file/path exclusion)  
   - **`SqlConnectionString`** (server, DB, user, password, etc.)  
   - **`AzureWebJobsStorage`** (for Blob triggers)

4. **Deploy the Functions**

   ```bash
   # Deploy FilterFunction
   cd FilterFunction
   func azure functionapp publish <your-filter-function-app-name>

   # Deploy LoaderFunction
   cd ../LoaderFunction
   func azure functionapp publish <your-loader-function-app-name>
   ```

5. **Test the Pipeline**

   1. Create a sample log file and zip it:
      ```bash
      echo '192.168.1.1 - - [01/Feb/2025:12:00:00 +0000] "GET /index.html HTTP/1.1" 200 1234 "http://example.com" "Mozilla/5.0"' > sample.log
      zip sample.zip sample.log
      ```
   2. Upload `sample.zip` to the `raw-logs` container:
      ```bash
      az storage blob upload \
          --account-name <your-storage-acct> \
          --container-name raw-logs \
          --name sample.zip \
          --file sample.zip \
          --account-key <your-storage-key>
      ```
   3. Verify the processed logs in your SQL DB:
      ```sql
      SELECT TOP 10 * FROM HttpLogs ORDER BY Timestamp DESC;
      ```

## Code Explanation

### FilterFunction

- **`[BlobTrigger("raw-logs/{name}.zip")]`**:  
  Automatically triggered when a new `.zip` file is uploaded to `raw-logs`.
- **Filtering Logic**:  
  - Environment variable `FilterConfig` loads custom exclusion rules (extensions, paths).  
  - Only lines containing `" 200 "` are retained (as an example).

### LoaderFunction

- **`[BlobTrigger("filtered-logs/out_{name}.zip")]`**:  
  Kicks off when a filtered `.zip` is created by FilterFunction in `filtered-logs`.
- **Log Parsing**:  
  Uses regex to capture IP, date/time, method, URL, etc.  
- **Bulk Insert**:  
  Collects rows in a `DataTable` and uses `SqlBulkCopy` for efficient insertion into `HttpLogs`.

## Contributing

Feel free to submit pull requests for:
- Improved filtering logic  
- Additional log formats or regex patterns  
- Integration with other Azure Services
