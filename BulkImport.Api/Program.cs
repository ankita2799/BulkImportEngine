using BulkImport.Core.Interfaces;
using BulkImport.Infrastructure.Data;
using BulkImport.Infrastructure.Parsers;
using BulkImport.Core.Models;
using BulkImport.Infrastructure.Pipeline;
using BulkImport.Infrastructure.Repositories;
using BulkImport.Infrastructure.Validators;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;

ExcelPackage.License.SetNonCommercialOrganization("BulkImportEngine");

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<ImportDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("BulkImportDb")));

// Repositories
builder.Services.AddScoped<IImportRepository, ImportJobRepository>();
builder.Services.AddScoped<BusinessPartnerRepository>();

// Parsers
builder.Services.AddScoped<ExcelParser>();
builder.Services.AddScoped<CsvParser>();
builder.Services.AddScoped<FileParserFactory>();

// Validator
builder.Services.AddScoped<IValidator<BusinessPartnerImportRow>, BusinessPartnerValidator>();

// Pipeline
builder.Services.AddScoped<IImportPipeline<BusinessPartnerImportRow>, ImportPipeline>();

// API
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(
        new System.Text.Json.Serialization.JsonStringEnumConverter());
}); 
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Increase file size limit for large imports
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52428800; // 50MB
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();