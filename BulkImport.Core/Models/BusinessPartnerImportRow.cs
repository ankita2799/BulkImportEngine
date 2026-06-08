using BulkImport.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Models
{
    public class BusinessPartnerImportRow : IImportRow
    {
        public int RowNumber { get; set; }
        public string Name { get; set; } = string.Empty;
        public string TaxId { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
