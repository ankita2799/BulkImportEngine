namespace BulkImport.Infrastructure.Repositories;

using BulkImport.Core.Enums;
using BulkImport.Core.Interfaces;
using BulkImport.Core.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Data;

public class ImportJobRepository : IImportRepository
{
    private readonly string _connectionString;

    public ImportJobRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BulkImportDb")
            ?? throw new InvalidOperationException("Connection string 'BulkImportDb' not found.");
    }

    // Creates a new import job record and returns the generated JobId
    public async Task<int> CreateJobAsync(string fileName, CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("usp_CreateImportJob", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@FileName", fileName);

        var jobIdParam = new SqlParameter("@JobId", SqlDbType.Int)
        {
            Direction = ParameterDirection.Output
        };
        command.Parameters.Add(jobIdParam);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);

        return (int)jobIdParam.Value;
    }

    // Updates job status + row counts at end of pipeline
    public async Task UpdateJobStatusAsync(
        int jobId,
        ImportStatus status,
        int totalRows,
        int validRows,
        int failedRows,
        CancellationToken cancellationToken = default)
    {
        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("usp_UpdateImportJobStatus", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@JobId", jobId);
        command.Parameters.AddWithValue("@Status", status.ToString());
        command.Parameters.AddWithValue("@TotalRows", totalRows);
        command.Parameters.AddWithValue("@ValidRows", validRows);
        command.Parameters.AddWithValue("@FailedRows", failedRows);
        command.Parameters.AddWithValue("@CompletedAt", DateTime.UtcNow);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Bulk logs failed rows via TVP — same pattern as your current work
    public async Task LogFailedRowsAsync(
        int jobId,
        IEnumerable<ImportRowResult> failedRows,
        CancellationToken cancellationToken = default)
    {
        // Build DataTable to pass as TVP
        var table = new DataTable();
        table.Columns.Add("RowNumber", typeof(int));
        table.Columns.Add("RawData", typeof(string));
        table.Columns.Add("Errors", typeof(string));

        foreach (var row in failedRows)
        {
            table.Rows.Add(
                row.RowNumber,
                row.RawData,
                string.Join(" | ", row.Errors)
            );
        }

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("usp_LogImportJobRows", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.AddWithValue("@JobId", jobId);

        var tvpParam = new SqlParameter("@FailedRows", SqlDbType.Structured)
        {
            TypeName = "dbo.ImportJobRowType",
            Value = table
        };
        command.Parameters.Add(tvpParam);

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    // Checks DB for existing TaxIds via TVP — one round trip for entire chunk
    public async Task<IReadOnlyList<string>> GetExistingTaxIdsAsync(
        IEnumerable<string> taxIds,
        CancellationToken cancellationToken = default)
    {
        // Build DataTable to pass as TVP
        var table = new DataTable();
        table.Columns.Add("TaxId", typeof(string));

        foreach (var id in taxIds)
            table.Rows.Add(id);

        using var connection = new SqlConnection(_connectionString);
        using var command = new SqlCommand("usp_CheckDuplicateTaxIds", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var tvpParam = new SqlParameter("@TaxIds", SqlDbType.Structured)
        {
            TypeName = "dbo.TaxIdListType",
            Value = table
        };
        command.Parameters.Add(tvpParam);

        await connection.OpenAsync(cancellationToken);

        var existingIds = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
            existingIds.Add(reader.GetString(0));

        return existingIds;
    }
}