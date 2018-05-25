using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PATWebAPI.Models
{
    public class ArchDesignConfig
    {
        public String name { get; set; }
        public List<ArchMatrix> matrix { get; set; }
    }
}