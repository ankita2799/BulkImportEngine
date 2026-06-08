namespace BulkImport.Tests.Pipeline;

using BulkImport.Core.Enums;
using BulkImport.Core.Interfaces;
using BulkImport.Core.Models;
using BulkImport.Infrastructure.Data;
using BulkImport.Infrastructure.Parsers;

using BulkImport.Infrastructure.Pipeline;
using BulkImport.Infrastructure.Repositories;
using BulkImport.Infrastructure.Validators;
using FluentAssertions;
using FluentValidation;
using Moq;
using OfficeOpenXml;
using Xunit;

public class ImportPipelineTests
{
    // ─── dependencies ────────────────────────────────────────────────────────
    private readonly Mock<IImportRepository> _mockImportRepo;
    private readonly Mock<IBusinessPartnerRepository> _mockBpRepo;
    private readonly IValidator<BusinessPartnerImportRow> _realValidator;
    private readonly ImportPipeline _pipeline;

    public ImportPipelineTests()
    {
        ExcelPackage.License.SetNonCommercialOrganization("BulkImportEngine");

        // real validator — pure logic, no dependencies
        _realValidator = new BusinessPartnerValidator();

        // mock import repository — no real DB needed
        _mockImportRepo = new Mock<IImportRepository>();
        _mockBpRepo = new Mock<IBusinessPartnerRepository>();

      
       

        // set up default mock behaviours
        SetupDefaultMocks();

        // real parsers + factory
        var excelParser = new ExcelParser();
        var csvParser = new CsvParser();
        var parserFactory = new FileParserFactory(excelParser, csvParser);

        // pipeline wired with real parsers + real validator + mocked repos
        _pipeline = new ImportPipeline(
            parserFactory,
            _realValidator,
            _mockBpRepo.Object,
            _mockImportRepo.Object);
    }

    // ─── default mock setup ──────────────────────────────────────────────────
    private void SetupDefaultMocks()
    {
        // CreateJobAsync always returns JobId = 1
        _mockImportRepo
            .Setup(r => r.CreateJobAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // UpdateJobStatusAsync does nothing — void equivalent
        _mockImportRepo
            .Setup(r => r.UpdateJobStatusAsync(
                It.IsAny<int>(),
                It.IsAny<ImportStatus>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // LogFailedRowsAsync does nothing
        _mockImportRepo
            .Setup(r => r.LogFailedRowsAsync(
                It.IsAny<int>(),
                It.IsAny<IEnumerable<ImportRowResult>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // GetExistingTaxIdsAsync returns empty list — no duplicates in DB by default
        _mockImportRepo
            .Setup(r => r.GetExistingTaxIdsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        // BulkInsertAsync does nothing — we don't test DB insert in unit tests
        _mockBpRepo
            .Setup(r => r.BulkInsertAsync(
                It.IsAny<IEnumerable<BusinessPartnerImportRow>>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
    }

    // ─── Excel stream helper ─────────────────────────────────────────────────
    private static Stream CreateExcelStream(Action<ExcelWorksheet> populateSheet)
    {
        var stream = new MemoryStream();
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("Sheet1");

        worksheet.Cells[1, 1].Value = "Name";
        worksheet.Cells[1, 2].Value = "TaxId";
        worksheet.Cells[1, 3].Value = "Email";
        worksheet.Cells[1, 4].Value = "Phone";
        worksheet.Cells[1, 5].Value = "Type";

        populateSheet(worksheet);

        package.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    // ════════════════════════════════════════════
    // JOB CREATION
    // ════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_ShouldCreateImportJob_AtStart()
    {
        // Arrange
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert — CreateJobAsync must have been called exactly once
        _mockImportRepo.Verify(
            r => r.CreateJobAsync("test.xlsx", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ════════════════════════════════════════════
    // RESULT COUNTS
    // ════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_WithAllValidRows_ShouldReturnCorrectCounts()
    {
        // Arrange — 3 valid rows
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";

            ws.Cells[3, 1].Value = "XYZ Suppliers";
            ws.Cells[3, 2].Value = "TAX-002";
            ws.Cells[3, 5].Value = "Supplier";

            ws.Cells[4, 1].Value = "Both Corp";
            ws.Cells[4, 2].Value = "TAX-003";
            ws.Cells[4, 5].Value = "Both";
        });

        // Act
        var result = await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert
        result.TotalRows.Should().Be(3);
        result.ValidRows.Should().Be(3);
        result.FailedRows.Should().Be(0);
    }

    [Fact]
    public async Task RunAsync_WithSomeInvalidRows_ShouldReturnCorrectCounts()
    {
        // Arrange — 2 valid, 1 invalid (empty TaxId)
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";

            ws.Cells[3, 1].Value = "XYZ Suppliers";
            ws.Cells[3, 2].Value = "TAX-002";
            ws.Cells[3, 5].Value = "Supplier";

            ws.Cells[4, 1].Value = "Bad Row";
            ws.Cells[4, 2].Value = ""; // invalid — empty TaxId
            ws.Cells[4, 5].Value = "Customer";
        });

        // Act
        var result = await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert
        result.TotalRows.Should().Be(3);
        result.ValidRows.Should().Be(2);
        result.FailedRows.Should().Be(1);
    }

    // ════════════════════════════════════════════
    // STATUS
    // ════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_WithAllValidRows_ShouldReturnCompletedStatus()
    {
        // Arrange
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        var result = await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert
        result.Status.Should().Be(ImportStatus.Completed);
    }

    [Fact]
    public async Task RunAsync_WithSomeInvalidRows_ShouldReturnCompletedWithErrorsStatus()
    {
        // Arrange — one bad row
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";

            ws.Cells[3, 1].Value = "Bad Row";
            ws.Cells[3, 2].Value = ""; // invalid
            ws.Cells[3, 5].Value = "Customer";
        });

        // Act
        var result = await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert
        result.Status.Should().Be(ImportStatus.CompletedWithErrors);
    }

    [Fact]
    public async Task RunAsync_WithEmptyFile_ShouldReturnCompletedStatus()
    {
        // Arrange — header only
        var stream = CreateExcelStream(ws => { });

        // Act 
        var result =await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert
        result.Status.Should().Be(ImportStatus.Completed);
        result.TotalRows.Should().Be(0);
    }

    // ════════════════════════════════════════════
    // SP CALLS VERIFIED
    // ════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_ShouldAlwaysUpdateJobStatus_EvenIfNoRows()
    {
        // Arrange — empty file
        var stream = CreateExcelStream(ws => { });

        // Act
        await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert — UpdateJobStatusAsync must always be called
        // even for empty files — job record must be closed out
        _mockImportRepo.Verify(
            r => r.UpdateJobStatusAsync(
                1,
                It.IsAny<ImportStatus>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithInvalidRows_ShouldCallLogFailedRows()
    {
        // Arrange — one bad row
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "Bad Row";
            ws.Cells[2, 2].Value = ""; // invalid
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert — LogFailedRowsAsync must be called when rows fail
        _mockImportRepo.Verify(
            r => r.LogFailedRowsAsync(
                It.IsAny<int>(),
                It.IsAny<IEnumerable<ImportRowResult>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_WithAllValidRows_ShouldNeverCallLogFailedRows()
    {
        // Arrange — all valid rows
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";
        });

        // Act
        await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert — no failures means LogFailedRowsAsync should never be called
        _mockImportRepo.Verify(
            r => r.LogFailedRowsAsync(
                It.IsAny<int>(),
                It.IsAny<IEnumerable<ImportRowResult>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ════════════════════════════════════════════
    // DB DUPLICATE CHECK
    // ════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_WhenDbHasExistingTaxId_ShouldCountItAsFailed()
    {
        // Arrange — TAX-001 already exists in DB
        _mockImportRepo
            .Setup(r => r.GetExistingTaxIdsAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "TAX-001" }); // simulate DB duplicate

        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001"; // exists in DB
            ws.Cells[2, 5].Value = "Customer";

            ws.Cells[3, 1].Value = "XYZ Suppliers";
            ws.Cells[3, 2].Value = "TAX-002"; // new — should pass
            ws.Cells[3, 5].Value = "Supplier";
        });

        // Act
        var result = await _pipeline.RunAsync(stream, "test.xlsx");

        // Assert
        result.TotalRows.Should().Be(2);
        result.ValidRows.Should().Be(1);   // only TAX-002 inserted
        result.FailedRows.Should().Be(1);  // TAX-001 rejected as duplicate
    }

    // ════════════════════════════════════════════
    // TIMING
    // ════════════════════════════════════════════

    [Fact]
    public async Task RunAsync_ShouldSetStartedAtAndCompletedAt()
    {
        // Arrange
        var stream = CreateExcelStream(ws =>
        {
            ws.Cells[2, 1].Value = "ABC Company";
            ws.Cells[2, 2].Value = "TAX-001";
            ws.Cells[2, 5].Value = "Customer";
        });

        var before = DateTime.UtcNow;

        // Act
        var result = await _pipeline.RunAsync(stream, "test.xlsx");

        var after = DateTime.UtcNow;

        // Assert — timestamps should be within the test window
        result.StartedAt.Should().BeOnOrAfter(before);
        result.CompletedAt.Should().BeOnOrBefore(after);
        result.Duration.Should().NotBeNull();
    }
}