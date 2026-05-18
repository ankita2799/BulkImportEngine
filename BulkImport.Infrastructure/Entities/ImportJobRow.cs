using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Infrastructure.Entities
{
    public class ImportJobRow
    {
        public int Id { get; set; }
        public int JobId { get; set; }
        public int RowNumber { get; set; }
        public string RawData { get; set; } = string.Empty;
        public string Errors { get; set; } = string.Empty;

        // Navigation property
        public ImportJob Job { get; set; } = null!;
    }
}
