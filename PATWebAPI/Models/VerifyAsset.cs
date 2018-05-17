using PAT.Common.Classes.ModuleInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PATWebAPI.Models
{
    public class VerifyAsset
    {
        public string CSPCode { get; set; }
        public List<string> deadloopCheck { get; set; }
        public List<string> livelockCheck { get; set; }
    }
}