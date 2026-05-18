using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Interfaces
{
    public interface IImportRow
    {
        int RowNumber { get; set; }
    }
}
