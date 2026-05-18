using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Enums
{
    public enum ImportStatus
    {
        Pending,
        Processing,
        Completed,
        CompletedWithErrors,
        Failed
    }
}
