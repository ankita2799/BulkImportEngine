namespace BulkImport.Infrastructure.Repositories;
using BulkImport.Infrastructure.Entities;
using BulkImport.Infrastructure.Data;
using BulkImport.Core.Models;
using EFCore.BulkExtensions;

public class BusinessPartnerRepository
{
    private readonly ImportDbContext _context;

    public BusinessPartnerRepository(ImportDbContext context)
    {
        _context = context;
    }

    // Maps valid import rows to entities and bulk inserts the chunk
    public async Task BulkInsertAsync(
        IEnumerable<BusinessPartnerImportRow> validRows,
        CancellationToken cancellationToken = default)
    {
        var entities = validRows.Select(row => new BusinessPartner
        {
            Name = row.Name,
            TaxId = row.TaxId,
            Email = string.IsNullOrWhiteSpace(row.Email) ? null : row.Email,
            Phone = string.IsNullOrWhiteSpace(row.Phone) ? null : row.Phone,
            Type = row.Type,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        var bulkConfig = new BulkConfig
        {
            BatchSize = 1000,
            SetOutputIdentity = true  // populates Id on entities after insert
        };

        await _context.BulkInsertAsync(entities, bulkConfig, cancellationToken: cancellationToken);
    }
}