using BulkImport.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Interfaces
{
    public interface IImportPipeline<T> where T : IImportRow
    {
        Task<ImportJobResult> RunAsync(
            Stream fileStream,
            string fileName,
            CancellationToken cancellationToken = default);
    }
}
