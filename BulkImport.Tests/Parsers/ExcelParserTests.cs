namespace BulkImport.Tests.Parsers;

using BulkImport.Infrastructure.Parsers;
using BulkImport.Core.Models;
using FluentAssertions;
using OfficeOpenXml;
using Xunit;

public class ExcelParserTests
{
    private readonly ExcelParser _parser;

    public ExcelParserTests()
    {
        _parser = new ExcelParser();
        ExcelPackage.License.SetNonCommercialOrganization("BulkImportEngine");
    }

    // ─── helper — builds an in-memory Excel stream ───────────────────────────
    private static Stream CreateExcelStream(Action<ExcelWorksheet> populateSheet)
    {
        var stream = new MemoryStream();
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Sheet1");

        // always add standard headers on row 1
        worksheet.Cells[1, 1].Value = "Name";
        worksheet.Cells[1, 2].Value = "TaxId";
        worksheet.Cells[1, 3].Value = "Email";
        worksheet.Cells[1, 4].Value = "Phone";
        worksheet.Cells[1, 5].Value = "Type";

        // caller populates data rows
        populateSheet(worksheet);

        package.SaveAs(stream);
        stream.Position = 0; // rewind so parser can read from start
        return stream;
    }

    // ════════════════════════════════════════════
    // BASIC PARSING
    // ════════════════════════════════════════════

    [Fact]
    public async Task ParseAsync_WithValidRows_ShouldReturnCorrectCount()
    {
        // Arrange — 3 data rows
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 3].Value = "abc@email.com";
            ws.Cells[2, 4].Value = "9876543210";
            ws.Cells[2, 5].Value = "Customer";

            ws.Cells[3, 1].Value = "XYZ Suppliers";
            ws.Cells[3, 2].Value = "TAX-002";
            ws.Cells[3, 3].Value = "xyz@email.com";
            ws.Cells[3, 4].Value = "9123456789";
            ws.Cells[3, 5].Value = "Supplier";

            ws.Cells[4, 1].Value = "Both Corp";
            ws.Cells[4, 2].Value = "TAX-003";
            ws.Cells[4, 3].Value = "both@email.com";
            ws.Cells[4, 4].Value = "9000000001";
            ws.Cells[4, 5].Value = "Both";
        });

        // Act — collect all rows from the async stream
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert
        rows.Should().HaveCount(3);
    }

    [Fact]
    public async Task ParseAsync_WithValidRows_ShouldMapFieldsCorrectly()
    {
        // Arrange
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 3].Value = "abc@email.com";
            ws.Cells[2, 4].Value = "9876543210";
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert — check every field mapped correctly
        var first = rows[0];
        first.Name.Should().Be("ABC Company");
        first.TaxId.Should().Be("TAX-001");
        first.Email.Should().Be("abc@email.com");
        first.Phone.Should().Be("9876543210");
        first.Type.Should().Be("Customer");
    }

    // ════════════════════════════════════════════
    // ROW NUMBER
    // ════════════════════════════════════════════

    [Fact]
    public async Task ParseAsync_ShouldAssignRowNumbersStartingAtOne()
    {
        // Arrange — 3 rows
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "Company A";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";

            ws.Cells[3, 1].Value = "Company B";
            ws.Cells[3, 2].Value = "TAX-002";
            ws.Cells[3, 5].Value = "Supplier";

            ws.Cells[4, 1].Value = "Company C";
            ws.Cells[4, 2].Value = "TAX-003";
            ws.Cells[4, 5].Value = "Both";
        });

        // Act
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert — row numbers start at 1, not 2 (EPPlus index)
        rows[0].RowNumber.Should().Be(1);
        rows[1].RowNumber.Should().Be(2);
        rows[2].RowNumber.Should().Be(3);
    }

    // ════════════════════════════════════════════
    // EDGE CASES
    // ════════════════════════════════════════════

    [Fact]
    public async Task ParseAsync_WithEmptyFile_ShouldReturnNoRows()
    {
        // Arrange — header only, no data rows
        var stream = CreateExcelStream(ws => { }); // no data added

        // Act
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert
        rows.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithCellsHavingExtraSpaces_ShouldTrimValues()
    {
        // Arrange — values with leading/trailing spaces
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "  ABC Company  "; // spaces around value
            ws.Cells[2, 2].Value = "  TAX-001  ";
            ws.Cells[2, 3].Value = "  abc@email.com  ";
            ws.Cells[2, 4].Value = "  9876543210  ";
            ws.Cells[2, 5].Value = "  Customer  ";
        });

        // Act
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert — Trim() in GetCellValue removes spaces
        rows[0].Name.Should().Be("ABC Company");
        rows[0].TaxId.Should().Be("TAX-001");
    }

    [Fact]
    public async Task ParseAsync_WithEmptyOptionalFields_ShouldReturnEmptyString()
    {
        // Arrange — Email and Phone left empty
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            // Email col 3 — intentionally left empty
            // Phone col 4 — intentionally left empty
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert — empty cells return string.Empty not null
        rows[0].Email.Should().BeNullOrEmpty();
        rows[0].Phone.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithNumericTaxId_ShouldConvertToString()
    {
        // Arrange — TaxId entered as a number in Excel (common user mistake)
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = 123456; // numeric value in Excel
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        var rows = new List<BusinessPartnerImportRow>();
        await foreach (var row in _parser.ParseAsync(stream))
            rows.Add(row);

        // Assert — GetCellValue calls .ToString() so numeric becomes string
        rows[0].TaxId.Should().Be("123456");
    }
}