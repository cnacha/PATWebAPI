using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PATWebAPI.Models
{
    public class VerifyResult
    {
        public double elapseTime { get; set; }
        public ConcurrentBag<DiagnosisResult> diagnosisList { get; set; }
    }
}