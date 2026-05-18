namespace BulkImport.Infrastructure.Parsers;

using BulkImport.Core.Interfaces;

public class FileParserFactory
{
    private readonly ExcelParser _excelParser;
    private readonly CsvParser _csvParser;

    public FileParserFactory(ExcelParser excelParser, CsvParser csvParser)
    {
        _excelParser = excelParser;
        _csvParser = csvParser;
    }

    public IFileParser<BusinessPartnerImportRow> GetParser(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();

        return extension switch
        {
            ".xlsx" => _excelParser,
            ".csv" => _csvParser,
            _ => throw new NotSupportedException(
                $"File type '{extension}' is not supported. Please upload .xlsx or .csv")
        };
    }
}