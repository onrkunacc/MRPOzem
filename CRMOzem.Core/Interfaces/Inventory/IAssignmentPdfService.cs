using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Interfaces.Inventory
{
    public interface IAssignmentPdfService
    {
        Task<(string FileKey, string FileName)> CreateAndStoreAsync(int assignmentId, CancellationToken ct);
    }
}
