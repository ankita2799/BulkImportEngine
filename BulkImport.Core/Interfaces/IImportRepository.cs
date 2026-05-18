using BulkImport.Core.Enums;
using BulkImport.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Interfaces
{
    public  interface IImportRepository
    {
        Task<int> CreateJobAsync(string fileName, CancellationToken cancellationToken = default);
        Task UpdateJobStatusAsync(
                int jobId,
                ImportStatus status,
                int totalRows,
                int validRows,
                int failedRows,
                CancellationToken cancellationToken = default);
        Task LogFailedRowsAsync(
        int jobId,
        IEnumerable<ImportRowResult> failedRows,
        CancellationToken cancellationToken = default);

        Task<IReadOnlyList<string>> GetExistingTaxIdsAsync(
            IEnumerable<string> taxIds,
            CancellationToken cancellationToken = default);
    }
}
