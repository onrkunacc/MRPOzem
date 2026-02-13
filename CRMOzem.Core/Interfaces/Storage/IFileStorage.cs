using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Interfaces.Storage
{
    public interface IFileStorage
    {
        Task<string> SaveAsync(string fileName, string contentType, Stream content, CancellationToken ct);
        Task<(Stream Content, string ContentType, string FileName)> OpenAsync(string fileKey, CancellationToken ct);
    }
}
