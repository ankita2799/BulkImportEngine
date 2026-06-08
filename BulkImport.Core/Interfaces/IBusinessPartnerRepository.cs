using BulkImport.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BulkImport.Core.Interfaces
{
    public interface IBusinessPartnerRepository
    {
        Task BulkInsertAsync(
            IEnumerable<BusinessPartnerImportRow> validRows,
            CancellationToken cancellationToken = default);
    }
}
