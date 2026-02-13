using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.Interfaces.Storage
{
    public sealed class FileStorageOptions
    {
        public string Provider { get; set; } = "Local";
        public string LocalRootPath { get; set; } = "App_Data/files";
    }
}
