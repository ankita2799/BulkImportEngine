namespace BulkImport.Tests.Validators;

using BulkImport.Core.Models;
using BulkImport.Infrastructure.Parsers;

using BulkImport.Infrastructure.Validators;
using FluentAssertions;
using FluentValidation;

public class BusinessPartnerValidatorTests
{
    // The thing we are testing
    private readonly BusinessPartnerValidator _validator;

    public BusinessPartnerValidatorTests()
    {
        // xUnit creates a new instance of this class for every test
        // so every test gets a fresh validator — no shared state between tests
        _validator = new BusinessPartnerValidator();
    }

    // ─── helper — builds a valid row so each test only changes what it needs ───
    private static BusinessPartnerImportRow ValidRow() => new()
    {
        RowNumber = 1,
        Name = "ABC Company",
        TaxId = "TAX-001",
        Email = "abc@email.com",
        Phone = "9876543210",
        Type = "Customer"
    };

    // ════════════════════════════════════════════
    // NAME RULES
    // ════════════════════════════════════════════

    [Fact]
    public async Task Name_WhenEmpty_ShouldHaveError()
    {
        // Arrange
        var row = ValidRow();
        row.Name = string.Empty; // only change what this test cares about

        // Act
        var result = await _validator.ValidateAsync(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Name is required.");
    }

    [Fact]
    public async Task Name_WhenExceeds200Characters_ShouldHaveError()
    {
        // Arrange
        var row = ValidRow();
        row.Name = new string('A', 201); // 201 A's — one over the limit

        // Act
        var result = await _validator.ValidateAsync(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Name cannot exceed 200 characters.");
    }

    [Fact]
    public async Task Name_WhenValid_ShouldNotHaveError()
    {
        // Arrange
        var row = ValidRow(); // Name = "ABC Company" — valid

        // Act
        var result = await _validator.ValidateAsync(row);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // ════════════════════════════════════════════
    // TAXID RULES
    // ════════════════════════════════════════════

    [Fact]
    public async Task TaxId_WhenEmpty_ShouldHaveError()
    {
        var row = ValidRow();
        row.TaxId = string.Empty;

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "TaxId is required.");
    }

    [Theory]
    [InlineData("TAX 001")]   // space not allowed
    [InlineData("TAX@001")]   // @ not allowed
    [InlineData("TAX#001")]   // # not allowed
    public async Task TaxId_WhenContainsInvalidCharacters_ShouldHaveError(string invalidTaxId)
    {
        // Arrange
        var row = ValidRow();
        row.TaxId = invalidTaxId;

        // Act
        var result = await _validator.ValidateAsync(row);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorMessage == "TaxId can only contain letters, numbers and hyphens.");
    }

    [Theory]
    [InlineData("TAX-001")]   // valid — letters, numbers, hyphen
    [InlineData("ABC123")]    // valid — letters and numbers
    [InlineData("123456")]    // valid — numbers only
    public async Task TaxId_WhenValidFormat_ShouldNotHaveError(string validTaxId)
    {
        var row = ValidRow();
        row.TaxId = validTaxId;

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeTrue();
    }

    // ════════════════════════════════════════════
    // EMAIL RULES
    // ════════════════════════════════════════════

    [Fact]
    public async Task Email_WhenEmpty_ShouldNotHaveError()
    {
        // This tests the When() condition — email is optional
        var row = ValidRow();
        row.Email = null; // not provided

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeTrue(); // no error — email is optional
    }

    [Fact]
    public async Task Email_WhenProvidedButInvalidFormat_ShouldHaveError()
    {
        var row = ValidRow();
        row.Email = "notanemail"; // provided but wrong format

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Email format is invalid.");
    }

    [Fact]
    public async Task Email_WhenProvidedAndValid_ShouldNotHaveError()
    {
        var row = ValidRow();
        row.Email = "valid@email.com";

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeTrue();
    }

    // ════════════════════════════════════════════
    // TYPE RULES
    // ════════════════════════════════════════════

    [Theory]
    [InlineData("Customer")]
    [InlineData("Supplier")]
    [InlineData("Both")]
    public async Task Type_WhenValidValue_ShouldNotHaveError(string validType)
    {
        var row = ValidRow();
        row.Type = validType;

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("Vendor")]
    [InlineData("Partner")]
    [InlineData("")]
    public async Task Type_WhenInvalidValue_ShouldHaveError(string invalidType)
    {
        var row = ValidRow();
        row.Type = invalidType;

        var result = await _validator.ValidateAsync(row);

        result.IsValid.Should().BeFalse();
    }

    // ════════════════════════════════════════════
    // MULTIPLE ERRORS — critical for bulk import
    // ════════════════════════════════════════════

    [Fact]
    public async Task WhenMultipleFieldsInvalid_ShouldReturnAllErrors()
    {
        // Arrange — intentionally broken row
        var row = new BusinessPartnerImportRow
        {
            RowNumber = 1,
            Name = string.Empty,    // error 1
            TaxId = string.Empty,    // error 2
            Email = "notanemail",    // error 3
            Type = "InvalidType"    // error 4
        };

        // Act
        var result = await _validator.ValidateAsync(row);

        // Assert — all four errors collected, not just the first
        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThanOrEqualTo(4);
    }
}