using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    public class LoginModel
    {
        public string? CorporationName {  get; set; }

        public string? Password { get; set; } = string.Empty;

        public string? User { get; set; } = "员工";

        public bool RememberMe { get; set; }
       
    }
}
