using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.IO;
using System.Text;
using PAT.Common;
using PAT.Common.Classes.ModuleInterface;
using Microsoft.WindowsAzure.ServiceRuntime;
using System.Reflection;
using PATWebAPI.Models;
using System.Collections.Concurrent;

namespace PATWebAPI.Controllers
{
    public class PATWebController : ApiController
    {
        ConcurrentBag<DiagnosisResult> asrtResults;



        [HttpPost]
        [ActionName("VerifyLTL")]
        public ConcurrentBag<DiagnosisResult> Verify(ArchDesignConfig  arch)
        {
            System.Diagnostics.Debug.WriteLine("processing "+arch.name);
            System.Diagnostics.Debug.WriteLine("ltl " + arch.ltl);
            List<ArchMatrix> matrix = arch.matrix;
            List<int> startCompList = new List<int>();
            foreach(ArchMatrix comp in matrix)
            {
                if (comp.IsStartCaller == true)
                    startCompList.Add(comp.ID);
            }
            ConcurrentBag<DiagnosisResult> result = new ConcurrentBag<DiagnosisResult>();
            // ConcurrentBag<List<DiagnosisResult>> resultSet = new ConcurrentBag<List<DiagnosisResult>>();
            ConcurrentBag<DiagnosisResult> pmResult = executeVerification(arch);
            foreach (DiagnosisResult obj in pmResult)
            {
                result.Add(obj);
            }
            bool isDeadlock = false;
            bool isLivelock = false;
            foreach (DiagnosisResult rs in pmResult)
            {
                if (rs.Symptom == "deadloop")
                    isDeadlock = true;
                else if (rs.Symptom == "livelock")
                    isLivelock = true;
            }
            if (pmResult.Count > 0 && isDeadlock && startCompList.Count > 1)
            {
                // clear result set
                DiagnosisResult eachitem;
                while (result.TryTake(out eachitem)) ;
                // for loop to simulate when each start component initiate
                foreach (int startCompId in startCompList)
                {
                    foreach (ArchMatrix comp in matrix)
                    {
                        if (comp.ID == startCompId)
                            comp.IsStartCaller = true;
                        else
                            comp.IsStartCaller = false;
                    }
                    ConcurrentBag<DiagnosisResult> addResult = executeVerification(arch);
                    foreach (DiagnosisResult obj in pmResult)
                    {
                        result.Add(obj);
                    }
                }

            } else if (pmResult.Count > 0 && isLivelock && startCompList.Count > 1)
            {
                // for loop to simulate when each start component initiate
                foreach (int startCompId in startCompList)
                {
                    foreach (ArchMatrix comp in matrix)
                    {
                        if (comp.ID == startCompId)
                            comp.IsStartCaller = true;
                        else
                            comp.IsStartCaller = false;
                    }
                    ConcurrentBag<DiagnosisResult> addResult = executeVerification(arch);
                    foreach (DiagnosisResult obj in pmResult)
                    {
                        result.Add(obj);
                    }
                }
            }

            return result;
        }

        private ConcurrentBag<DiagnosisResult> executeVerification(ArchDesignConfig arch)
        {
            VerifyAsset verifyAsset = GenerateAsset(arch);
            
            System.Diagnostics.Debug.WriteLine(verifyAsset.CSPCode);
            try
            {
                PAT.CSP.ModuleFacade modulebase = new PAT.CSP.ModuleFacade();

                SpecificationBase Spec = modulebase.ParseSpecification(verifyAsset.CSPCode, string.Empty, string.Empty);
                System.Diagnostics.Debug.WriteLine("Specification Loaded...");

                 // Verify LTL
                List<KeyValuePair<string, AssertionBase>> asrtlists = Spec.AssertionDatabase.ToList();
                foreach (KeyValuePair<string, AssertionBase> asrt in asrtlists)
                {
                    //print assertion for debugging 
                    System.Diagnostics.Debug.WriteLine("#" + asrt.Key + "#");
                    RunAssertion(asrt.Value, 1, 0);
                }

                ConcurrentBag<DiagnosisResult> diagnosisList = new ConcurrentBag<DiagnosisResult>();

                asrtResults = new ConcurrentBag<DiagnosisResult>();
                // wait for the  result
                while (asrtResults.Count == 0)
                {
                  //  System.Diagnostics.Debug.WriteLine("wait for result from ltl checking : ");
                }

                // analyse for the  result
                foreach (DiagnosisResult result in asrtResults)
                {
                   
                        result.Symptom = "LTL";
                        diagnosisList.Add(result);
                   
                }

                
               return diagnosisList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.Message);
                System.Diagnostics.Trace.TraceError(ex.StackTrace);
            }
            return null;

        }


        private bool IsBracketFound(string sequence)
        {
            if (sequence.IndexOf("(") != -1 && sequence.IndexOf(")") != -1)
                return true;
            else
                return false;
        }

        private bool IsAdjacentInvokeFound(string sequence)
        {
            List<string> actsSequence = sequence.Split(' ').ToList();
            string prevAct = "";
            foreach (String eachAct in actsSequence)
            {
                if (eachAct.IndexOf("invoke") !=-1 && prevAct.IndexOf("invoke") != -1)
                {
                    return true;
                }
                prevAct = eachAct;
            }
            return false;
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
  

        [HttpPost]
        [ActionName("GenerateAsset")]
        public VerifyAsset GenerateAsset(ArchDesignConfig arch)
        {
            List<ArchMatrix> matrix = arch.matrix;
            StringBuilder cspComponent = new StringBuilder();
            StringBuilder cspMatrix = new StringBuilder();
            StringBuilder cspStartSimulate = new StringBuilder();
            StringBuilder cspDeadLockCheck = new StringBuilder();
            StringBuilder cspLTLCheck = new StringBuilder();
            List<string> livelockAssertList = new List<String>();
            List<string> deadlockAssertList = new List<String>();
           
            Dictionary<int, int> compDict = new Dictionary<int, int>();
            // generate csp for component declaration
            for (var i = 0; i < matrix.Count; i++)
            {
                cspComponent.Append(matrix[i].ID);
                //cspDeadLockCheck.Append("invokecount["+i+"]");
                // generate code to verify deadlock
                string deadlockAssert = "System() |= []<> request."+ matrix[i].ID ;
                cspDeadLockCheck.AppendLine("#assert "+ deadlockAssert + ";");
                deadlockAssertList.Add(deadlockAssert);

                compDict.Add(matrix[i].ID, i);
                if (i != matrix.Count - 1)
                {
                    cspComponent.Append(",");

                }
                if (matrix[i].IsStartCaller)
                {
                    // start simulate in concurrent if there is more than one start caller
                    if (cspStartSimulate.Length > 0) { cspStartSimulate.Append(" ||| "); }
                    cspStartSimulate.Append("Request(" + i + ")");
                }
            }
            // generate csp for matrix 
            for (var i = 0; i < matrix.Count; i++)
            {
                for (var j = 0; j < matrix.Count; j++)
                {
                    //System.Diagnostics.Debug.WriteLine("prc mat: " + matrix[i].Calls[j]);
                    if (matrix[i].Calls != null && j < matrix[i].Calls.Count)
                    {
                        cspMatrix.Append("reqmat[" + i + "][" + j + "] = " + compDict[matrix[i].Calls[j]] + ";");
                    }
                    else
                    {
                        cspMatrix.Append("reqmat[" + i + "][" + j + "] = " + -1 + ";");
                    }
                   
               
                }
            }
            cspLTLCheck.AppendLine("#assert System() |=  " + arch.ltl + ";");

            string templatePath = System.Web.HttpContext.Current.Request.MapPath("~\\CSP\\com-tier-tracematrix.csp");

            // read template and replace code with dynamic generated 
            string templateTxt = File.ReadAllText(templatePath);
            StringBuilder cspCode = new StringBuilder(templateTxt);
            cspCode.Replace("/**COMPONENTNUMBER**/", matrix.Count.ToString());
            cspCode.Replace("/**COMPONENTLIST**/", cspComponent.ToString());
            cspCode.Replace("/**MATRIX**/", cspMatrix.ToString());
            cspCode.Replace("/**START**/", cspStartSimulate.ToString());
            //cspCode.Replace("/**DEADLOCKCHECK**/", cspDeadLockCheck.ToString());
            cspCode.Replace("/**LTLCHECK**/", cspLTLCheck.ToString());

            VerifyAsset asset = new VerifyAsset();
            asset.CSPCode = cspCode.ToString();
            //asset.deadloopCheck = deadlockAssertList;
           // asset.livelockCheck = livelockAssertList;

            return asset;
        }
       
        private void RunAssertion(AssertionBase assertion,int fairnessIndex, int engineIndex)
        {
            AssertionVerifier verifier = new AssertionVerifier(assertion, fairnessIndex, engineIndex);
            ThreadedExecuter<DiagnosisResult> executer = new ThreadedExecuter<DiagnosisResult>(verifier.Run, gatherResults);
            executer.Start();
        }

        void gatherResults(DiagnosisResult result)
        {
            System.Diagnostics.Debug.WriteLine("retrieve result : " + result.Symptom);
            asrtResults.Add(result);
        }
    }

    
}
