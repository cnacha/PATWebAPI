using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PATWebAPI.Models
{
    public class ArchMatrix
    {
        public int ID { get; set; }

        public bool IsStartCaller { get; set; }

        private List<int> calls;

        public List<int> Calls
        {
            get { return calls; }
            set { calls = value; }
        }
    }
}