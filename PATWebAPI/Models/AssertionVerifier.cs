using PAT.Common.Classes.ModuleInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace PATWebAPI.Models
{
    
    public class AssertionVerifier
    {
        public AssertionBase assertion;

        //engineIndex 0 for Depth First Search, 1 for Breadth First Search
        public int fairnessIndex;
        public int engineIndex;

        public AssertionVerifier(AssertionBase assertion, int fairnessIndex, int engineIndex)
        {
            this.assertion = assertion;
            this.fairnessIndex = fairnessIndex;
            this.engineIndex = engineIndex;
        }

        //engineIndex 0 for Depth First Search, 1 for Breadth First Search
        public DiagnosisResult Run()
        {
          //  System.Diagnostics.Debug.WriteLine(assertion);
            // return CreatedAtRoute("GetProduct", new { id = item.Id }, item);
            assertion.UIInitialize(null, fairnessIndex, engineIndex);

            assertion.VerificationMode = true;
            assertion.InternalStart();

        //    System.Diagnostics.Debug.WriteLine(assertion.GetVerificationStatistics());
            DiagnosisResult result = new DiagnosisResult();
            result.Assertion = assertion.ToString();

           // System.Diagnostics.Debug.WriteLine("VALID? " + assertion.VerificationOutput.VerificationResult.Equals(VerificationResultType.VALID));
            string scenarioDesc = "";
           // System.Diagnostics.Debug.WriteLine("loop: " + assertion.VerificationOutput.LoopIndex);

            result.MemoryUsage = assertion.VerificationOutput.EstimateMemoryUsage;
            result.TotalTime = assertion.VerificationOutput.VerificationTime;
            result.NumberOfStates = assertion.VerificationOutput.NoOfStates;
            result.LoopIndex = assertion.VerificationOutput.LoopIndex;

            if (assertion.VerificationOutput.VerificationResult.Equals(VerificationResultType.VALID))
            {
                result.IsValid = true;
            }
            else
            {
                result.IsValid = false;   
            }
            if (assertion.VerificationOutput.CounterExampleTrace != null)
            {
                foreach (ConfigurationBase step in assertion.VerificationOutput.CounterExampleTrace)
                {
                    scenarioDesc += " " + step.GetDisplayEvent();
                }
                result.Scenario = scenarioDesc;
           //     System.Diagnostics.Debug.WriteLine(scenarioDesc);
            }

            // determine symthomp
            if (result.LoopIndex >= 0)
            {
                result.Symptom = "deadloop";
            }
            else if (result.Scenario!=null && hasDuplilcateInvoke(result.Scenario))
            {
                result.Symptom = "livelock";
            }
            else
            {
                result.Symptom = "normal";
            }

            return result;

        }

        private bool hasDuplilcateInvoke(string sequence)
        {
            List<string> actsSequence = sequence.Split(' ').ToList();
            List<string> duplicatedComponentInvoke = new List<string>();
            string actName;
            foreach (String eachAct in actsSequence)
            {
                actName = eachAct.Trim();
                if (duplicatedComponentInvoke.Contains(actName))
                {
                    return true;
                }
                duplicatedComponentInvoke.Add(actName);
            }
            return false;
        }
    }
}