using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Calliditas.Integration.D365Api
{
    public class GraphToken
    {
        public string expires_on { get; set; }
        public string access_token { get; set; }
    }
}
