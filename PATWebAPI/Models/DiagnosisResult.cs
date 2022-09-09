using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PATWebAPI.Models
{
    public class DiagnosisResult
    {
        public string Symptom { get; set; }
        public string Assertion { get; set; }
        public bool IsValid { get; set; }
        public string Scenario { get; set; }
        public int LoopIndex { get; set; }
        public float MemoryUsage { get; set; }
        public double TotalTime { get; set; }
        public double NumberOfStates { get; set; }
    }
}