namespace BulkImport.Infrastructure.Parsers;

using BulkImport.Core.Interfaces;
using BulkImport.Core.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Runtime.CompilerServices;

public class CsvParser : IFileParser<BusinessPartnerImportRow>
{
    public async IAsyncEnumerable<BusinessPartnerImportRow> ParseAsync(
        Stream fileStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,     // don't throw if a column is missing
            HeaderValidated = null,       // don't throw if header name differs
            TrimOptions = TrimOptions.Trim
        };

        using var reader = new StreamReader(fileStream);
        using var csv = new CsvReader(reader, config);

        csv.Context.RegisterClassMap<BusinessPartnerImportRowMap>();

        int rowNumber = 0;

        await foreach (var record in csv.GetRecordsAsync<BusinessPartnerImportRow>()
                                        .WithCancellation(cancellationToken))
        {
            rowNumber++;
            record.RowNumber = rowNumber;
            yield return record;
        }
    }
}

// Maps CSV column headers to DTO properties
// Handles header name variations e.g. "Tax Id" vs "TaxId"
public class BusinessPartnerImportRowMap : ClassMap<BusinessPartnerImportRow>
{
    public BusinessPartnerImportRowMap()
    {
        Map(x => x.Name).Name("Name");
        Map(x => x.TaxId).Name("TaxId", "Tax Id", "Tax_Id");
        Map(x => x.Email).Name("Email").Optional();
        Map(x => x.Phone).Name("Phone").Optional();
        Map(x => x.Type).Name("Type");
    }
}