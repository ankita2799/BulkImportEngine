using BulkImport.Core.Interfaces;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using BulkImport.Core.Models;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Infrastructure.Parsers
{
    public class ExcelParser : IFileParser<BusinessPartnerImportRow>
    {
        public async IAsyncEnumerable<BusinessPartnerImportRow> ParseAsync(
            Stream fileStream,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {

            using var package = new ExcelPackage(fileStream);
            var worksheet = package.Workbook.Worksheets[0];

            if (worksheet == null)
                yield break;

            int totalRows = worksheet.Dimension?.Rows ?? 0;

            if (totalRows <= 1)
                yield break; // only header or empty

            // Start from row 2 — row 1 is the header
            for (int row = 2; row <= totalRows; row++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Small async yield to keep this truly async
                await Task.Yield();

                yield return new BusinessPartnerImportRow
                {
                    RowNumber = row - 1, // user-facing row number starts at 1
                    Name = GetCellValue(worksheet, row, 1),
                    TaxId = GetCellValue(worksheet, row, 2),
                    Email = GetCellValue(worksheet, row, 3),
                    Phone = GetCellValue(worksheet, row, 4),
                    Type = GetCellValue(worksheet, row, 5)
                };
            }
        }

        private static string GetCellValue(ExcelWorksheet ws, int row, int col)
            => ws.Cells[row, col].Value?.ToString()?.Trim() ?? string.Empty;
    }
}
