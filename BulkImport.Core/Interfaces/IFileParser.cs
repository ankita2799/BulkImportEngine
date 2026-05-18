using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Interfaces
{
    public  interface IFileParser<T> where T : IImportRow
    {
        IAsyncEnumerable<T> ParseAsync(Stream fileStream, CancellationToken cancellationToken = default);
    }
}
