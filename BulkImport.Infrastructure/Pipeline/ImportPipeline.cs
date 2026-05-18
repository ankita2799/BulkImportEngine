namespace BulkImport.Infrastructure.Pipeline;

using BulkImport.Core.Enums;
using BulkImport.Core.Interfaces;
using BulkImport.Core.Models;
using BulkImport.Infrastructure.Parsers;
using BulkImport.Infrastructure.Repositories;
using FluentValidation;

public class ImportPipeline : IImportPipeline<BusinessPartnerImportRow>
{
    private readonly FileParserFactory _parserFactory;
    private readonly IValidator<BusinessPartnerImportRow> _validator;
    private readonly BusinessPartnerRepository _businessPartnerRepository;
    private readonly IImportRepository _importRepository;

    private const int ChunkSize = 1000;

    public ImportPipeline(
        FileParserFactory parserFactory,
        IValidator<BusinessPartnerImportRow> validator,
        BusinessPartnerRepository businessPartnerRepository,
        IImportRepository importRepository)
    {
        _parserFactory = parserFactory;
        _validator = validator;
        _businessPartnerRepository = businessPartnerRepository;
        _importRepository = importRepository;
    }

    public async Task<ImportJobResult> RunAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        // Step 1 — create job record in DB via SP
        var jobId = await _importRepository.CreateJobAsync(fileName, cancellationToken);

        var result = new ImportJobResult
        {
            JobId = jobId,
            FileName = fileName,
            StartedAt = DateTime.UtcNow,
            Status = ImportStatus.Processing
        };

        int totalRows = 0;
        int validRows = 0;
        int failedRows = 0;

        // Tracks TaxIds seen within this file to catch in-file duplicates
        var seenTaxIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Step 2 — get the right parser (Excel or CSV) based on file extension
            var parser = _parserFactory.GetParser(fileName);

            // Step 3 — stream rows and collect into chunks
            var chunk = new List<BusinessPartnerImportRow>(ChunkSize);

            await foreach (var row in parser.ParseAsync(fileStream, cancellationToken))
            {
                chunk.Add(row);
                totalRows++;

                // Process chunk when it hits ChunkSize
                if (chunk.Count == ChunkSize)
                {
                    var (valid, failed) = await ProcessChunkAsync(chunk, seenTaxIds, cancellationToken);
                    validRows += valid;
                    failedRows += failed;
                    chunk.Clear();
                }
            }

            // Process any remaining rows in the last chunk
            if (chunk.Count > 0)
            {
                var (valid, failed) = await ProcessChunkAsync(chunk, seenTaxIds, cancellationToken);
                validRows += valid;
                failedRows += failed;
            }

            // Step 4 — determine final status
            result.Status = failedRows == 0
                ? ImportStatus.Completed
                : ImportStatus.CompletedWithErrors;
        }
        catch (Exception)
        {
            result.Status = ImportStatus.Failed;
            throw;
        }
        finally
        {
            // Step 5 — always update job record regardless of outcome
            result.TotalRows = totalRows;
            result.ValidRows = validRows;
            result.FailedRows = failedRows;
            result.CompletedAt = DateTime.UtcNow;

            await _importRepository.UpdateJobStatusAsync(
                jobId,
                result.Status,
                totalRows,
                validRows,
                failedRows,
                cancellationToken);
        }

        return result;
    }

    private async Task<(int valid, int failed)> ProcessChunkAsync(
        List<BusinessPartnerImportRow> chunk,
        HashSet<string> seenTaxIds,
        CancellationToken cancellationToken)
    {
        var validRows = new List<BusinessPartnerImportRow>();
        var failedRows = new List<ImportRowResult>();

        // Step A — FluentValidation + in-file duplicate check
        foreach (var row in chunk)
        {
            var errors = new List<string>();

            // FluentValidation rules
            var validationResult = await _validator.ValidateAsync(row, cancellationToken);
            if (!validationResult.IsValid)
                errors.AddRange(validationResult.Errors.Select(e => e.ErrorMessage));

            // In-file duplicate TaxId check
            if (!string.IsNullOrWhiteSpace(row.TaxId) && !seenTaxIds.Add(row.TaxId))
                errors.Add($"Duplicate TaxId '{row.TaxId}' found within the file.");

            if (errors.Count > 0)
                failedRows.Add(ImportRowResult.Failure(row.RowNumber, row.Name, errors));
            else
                validRows.Add(row);
        }

        // Step B — DB duplicate check via SP (only for rows that passed validation)
        if (validRows.Count > 0)
        {
            var taxIdsToCheck = validRows.Select(r => r.TaxId);
            var existingTaxIds = await _importRepository.GetExistingTaxIdsAsync(
                                      taxIdsToCheck, cancellationToken);

            if (existingTaxIds.Count > 0)
            {
                var existingSet = new HashSet<string>(existingTaxIds, StringComparer.OrdinalIgnoreCase);

                // Move DB duplicates from valid to failed
                var dbDuplicates = validRows.Where(r => existingSet.Contains(r.TaxId)).ToList();
                foreach (var dup in dbDuplicates)
                {
                    failedRows.Add(ImportRowResult.Failure(
                        dup.RowNumber,
                        dup.Name,
                        new List<string> { $"TaxId '{dup.TaxId}' already exists in the database." }
                    ));
                }

                validRows = validRows.Where(r => !existingSet.Contains(r.TaxId)).ToList();
            }
        }

        // Step C — bulk insert valid rows
        if (validRows.Count > 0)
            await _businessPartnerRepository.BulkInsertAsync(validRows, cancellationToken);

        // Step D — log failed rows via SP
        if (failedRows.Count > 0)
            await _importRepository.LogFailedRowsAsync(1, failedRows, cancellationToken);

        return (validRows.Count, failedRows.Count);
    }
}