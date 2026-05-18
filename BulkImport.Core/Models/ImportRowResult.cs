using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BulkImport.Core.Models
{
    public class ImportRowResult
    {
        public int RowNumber { get; set; }
        public bool IsValid => Errors.Count == 0;
        public string RawData { get; set; } = string.Empty;

        public List<string> Errors { get; set; } = new();
        public static ImportRowResult Success(int rowNumber) => new()
        {
            RowNumber = rowNumber
        };
        public static ImportRowResult Failure(int rowNumber, string rawData, List<string> errors) => new()
        {
            RowNumber = rowNumber,
            RawData = rawData,
            Errors = errors
        };
    }
}
