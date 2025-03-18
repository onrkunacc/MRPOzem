using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Business.Interfaces
{
    public interface IAwsS3FileService
    {
        Task<string> UploadFileAsync(Stream fileStream, string fileName, string folder);
        Task<bool> DeleteFileAsync(string fileName, string folder);
        Task<List<string>> ListFilesAsync(string folder);
        Task<List<string>> ListFoldersAsync(string folder);
        Task CreateFolderAsync(string parentFolder, string folderName);

    }
}
