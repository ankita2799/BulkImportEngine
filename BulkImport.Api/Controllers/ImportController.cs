namespace BulkImport.Api.Controllers;

using BulkImport.Core.Interfaces;
using BulkImport.Infrastructure.Parsers;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class ImportController : ControllerBase
{
    private readonly IImportPipeline<BusinessPartnerImportRow> _pipeline;

    public ImportController(IImportPipeline<BusinessPartnerImportRow> pipeline)
    {
        _pipeline = pipeline;
    }

    [HttpPost("business-partners")]
    [RequestSizeLimit(52428800)] // 50MB
    public async Task<IActionResult> ImportBusinessPartners(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file uploaded.");

        var allowedExtensions = new[] { ".xlsx", ".csv" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!allowedExtensions.Contains(extension))
            return BadRequest("Only .xlsx and .csv files are supported.");

        using var stream = file.OpenReadStream();

        var result = await _pipeline.RunAsync(stream, file.FileName, cancellationToken);

        return result.Status switch
        {
            Core.Enums.ImportStatus.Completed => Ok(result),
            Core.Enums.ImportStatus.CompletedWithErrors => Ok(result),
            Core.Enums.ImportStatus.Failed => StatusCode(500, result),
            _ => Ok(result)
        };
    }
}