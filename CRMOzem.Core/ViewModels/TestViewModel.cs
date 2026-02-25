using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CRMOzem.Core.ViewModels
{
    public class TestViewModel
    {
        public int TestID { get; set; }
        public string TestName { get; set; }
        [StringLength(30)]
        public string? SutCode { get; set; }   
    }
}
