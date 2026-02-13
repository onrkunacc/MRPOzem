using CRMOzem.Core.Interfaces.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Services.Storage
{
    public sealed class LocalFileStorage : IFileStorage
    {
        private readonly FileStorageOptions _opt;
        private readonly IWebHostEnvironment _env;

        public LocalFileStorage(IOptions<FileStorageOptions> opt, IWebHostEnvironment env)
        {
            _opt = opt.Value;
            _env = env;
        }

        public async Task<string> SaveAsync(string fileName, string contentType, Stream content, CancellationToken ct)
        {
            var root = Path.IsPathRooted(_opt.LocalRootPath)
                ? _opt.LocalRootPath
                : Path.Combine(_env.ContentRootPath, _opt.LocalRootPath);

            Directory.CreateDirectory(root);

            var safeName = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}_{Sanitize(fileName)}";
            var fullPath = Path.Combine(root, safeName);

            await using var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await content.CopyToAsync(fs, ct);
            return fullPath;
        }

        public Task<(Stream Content, string ContentType, string FileName)> OpenAsync(string fileKey, CancellationToken ct)
        {
            Stream stream = new FileStream(fileKey, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Task.FromResult((stream, "application/pdf", Path.GetFileName(fileKey)));
        }

        private static string Sanitize(string name)
        {
            foreach (var ch in Path.GetInvalidFileNameChars()) name = name.Replace(ch, '_');
            return name;
        }
    }

}
