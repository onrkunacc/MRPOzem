using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CRMOzem.Core.Models;

namespace CRMOzem.Core.ViewModels
{
    public class FileTransferPageViewModel
    {
        public FileUploadViewModel UploadModel { get; set; } = new();
        public List<FileTransferQueue> UploadedFiles { get; set; } = new();
    }
}
