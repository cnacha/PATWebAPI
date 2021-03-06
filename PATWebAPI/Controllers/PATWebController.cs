﻿using System;
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
        [ActionName("Verify")]
        public ConcurrentBag<DiagnosisResult> Verify(List<ArchMatrix> matrix)
        {
            List<int> startCompList = new List<int>();
            foreach(ArchMatrix comp in matrix)
            {
                if (comp.IsStartCaller == true)
                    startCompList.Add(comp.ID);
            }
            ConcurrentBag<DiagnosisResult> result = new ConcurrentBag<DiagnosisResult>();
            // ConcurrentBag<List<DiagnosisResult>> resultSet = new ConcurrentBag<List<DiagnosisResult>>();
            ConcurrentBag<DiagnosisResult> pmResult = executeVerification(matrix);
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
                    ConcurrentBag<DiagnosisResult> addResult = executeVerification(matrix);
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
                    ConcurrentBag<DiagnosisResult> addResult = executeVerification(matrix);
                    foreach (DiagnosisResult obj in pmResult)
                    {
                        result.Add(obj);
                    }
                }
            }

            return result;
        }

        private ConcurrentBag<DiagnosisResult> executeVerification(List<ArchMatrix> matrix)
        {
            VerifyAsset verifyAsset = GenerateAsset(matrix);
            
            System.Diagnostics.Debug.WriteLine(verifyAsset.CSPCode);
            try
            {
                PAT.CSP.ModuleFacade modulebase = new PAT.CSP.ModuleFacade();

                SpecificationBase Spec = modulebase.ParseSpecification(verifyAsset.CSPCode, string.Empty, string.Empty);
                System.Diagnostics.Debug.WriteLine("Specification Loaded...");

                //print assertion for debugging
                List<KeyValuePair<string, AssertionBase>> asrtlists = Spec.AssertionDatabase.ToList();
                foreach (KeyValuePair<string, AssertionBase> asrt in asrtlists)
                {
                    System.Diagnostics.Debug.WriteLine("#" + asrt.Key + "#");
                }

                ConcurrentBag<DiagnosisResult> diagnosisList = new ConcurrentBag<DiagnosisResult>();

                // Verify deadlock loop
                bool deadlockFound = false;
                asrtResults = new ConcurrentBag<DiagnosisResult>();
                for (var i = 0; i < verifyAsset.deadloopCheck.Count; i++)
                {
                    AssertionBase assertion = Spec.AssertionDatabase[verifyAsset.deadloopCheck[i]];
                    RunAssertion(assertion, 1, 0);
                }
                // wait for the deadlock result
                while(asrtResults.Count < verifyAsset.deadloopCheck.Count)
                {
                  //  System.Diagnostics.Debug.WriteLine("wait for result from deadloop checking : ");
                }

                // analyse for the deadlock result
                foreach (DiagnosisResult result in asrtResults)
                {
                    if (!result.IsValid && result.Scenario != null && result.LoopIndex > -1)
                    {
                        deadlockFound = true;
                        result.Symptom = "deadloop";
                        diagnosisList.Add(result);
                    }
                }

                // Verify livelock, if deadlock is not found 
                if (!deadlockFound)
                {
                    List<DiagnosisResult> normalList = new List<DiagnosisResult>();
                    asrtResults = new ConcurrentBag<DiagnosisResult>();
                    for (var i = 0; i < verifyAsset.livelockCheck.Count; i++)
                    {
                        AssertionBase assertion = Spec.AssertionDatabase[verifyAsset.livelockCheck[i]];
                        RunAssertion(assertion, 0, 0);
                    }

                    // wait for the livelock result
                    while (asrtResults.Count < verifyAsset.livelockCheck.Count)
                    {
                     //   System.Diagnostics.Debug.WriteLine("wait for result from livelock checking : ");
                    }
                    bool livelockFound = false;
                    string prevScenario = null;
                    bool isAllSameScenario = true;
                    // analyse for the livelock result
                    foreach (DiagnosisResult result in asrtResults)
                    {
                        if (!result.IsValid && result.Scenario != null && hasDuplilcateInvoke(result.Scenario))
                        {
                            if (result.LoopIndex > -1)
                                result.Symptom = "deadloop";
                            else
                                result.Symptom = "livelock";
                            diagnosisList.Add(result);
                            livelockFound = true;

                        }
                        else if (!result.IsValid && result.Scenario != null)
                        {
                            result.Symptom = "normal";
                            normalList.Add(result);
                            if (prevScenario != null && !prevScenario.Equals(result.Scenario))
                            {
                                isAllSameScenario = false;
                            }

                        }
                        prevScenario = result.Scenario;
                        System.Diagnostics.Debug.WriteLine("prevScenario : " + prevScenario);
                    }

                    // in case normal and all same scenario, try to generate more simulation for normal case
                    if (!livelockFound)
                    {
                        foreach(DiagnosisResult obj  in normalList)
                        {
                            diagnosisList.Add(obj);
                        }
                       
                        // if all same scenario given, we execute additional assertion
                        if (isAllSameScenario)
                        {
                            string firstId = prevScenario.Substring(prevScenario.IndexOf(".") + 1, 3);
                            string lastId = prevScenario.Substring(prevScenario.LastIndexOf(".") + 1);
                            System.Diagnostics.Debug.WriteLine("firstId : " + firstId);
                            System.Diagnostics.Debug.WriteLine("lastId : " + lastId);
                            verifyAsset = InsertMoreLivelockCheck(verifyAsset, matrix, firstId, lastId);
                            Spec = modulebase.ParseSpecification(verifyAsset.CSPCode, string.Empty, string.Empty);
                            asrtlists = Spec.AssertionDatabase.ToList();
                            asrtResults = new ConcurrentBag<DiagnosisResult>();
                            foreach (KeyValuePair<string, AssertionBase> asrt in asrtlists)
                            {
                             //   System.Diagnostics.Debug.WriteLine("#" + asrt.Key + "#");
                                RunAssertion(asrt.Value, 0, 0);
                            }
                           
                            while (asrtResults.Count < asrtlists.Count)
                            {
                            //    System.Diagnostics.Debug.WriteLine("wait for result from normal : ");
                            }
                            foreach (DiagnosisResult result in asrtResults)
                            {
                                if (!result.IsValid && result.Scenario != null)
                                {
                                    result.Symptom = "normal";
                                    diagnosisList.Add(result);

                                }
                            }
                        }
                    }
                }
                ConcurrentBag<DiagnosisResult> rs = checkLivelockBetweenResult(diagnosisList);
                if (rs.Count != 0)
                    diagnosisList = rs;
                return diagnosisList;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.Message);
                System.Diagnostics.Trace.TraceError(ex.StackTrace);
            }
            return null;

        }

        private ConcurrentBag<DiagnosisResult> checkLivelockBetweenResult(ConcurrentBag<DiagnosisResult> diagnosisList)
        {
            List<DiagnosisResult> lList = diagnosisList.ToList();
            ConcurrentBag<DiagnosisResult> result = new ConcurrentBag<DiagnosisResult>();
            lList.AddRange(diagnosisList);
            int i = 0;
            foreach (DiagnosisResult rsItem in diagnosisList)
            {
                for (var j= 0; j < lList.Count; j++)
                {
                    string scenario1 = rsItem.Scenario;
                    string scenario2 = lList[j].Scenario;
                    //System.Diagnostics.Debug.WriteLine(scenario1.Substring(scenario1.IndexOf(".") + 1, 3) +" == "+ scenario2.Substring(scenario2.IndexOf(".")+1, 3));
                    if(i!=j 
                        && rsItem.Symptom.Equals("normal")
                        && lList[j].Symptom.Equals("normal")
                        && !scenario1.Equals(scenario2) 
                        && scenario1.Substring(scenario1.IndexOf(".") + 1, 3).Equals(scenario2.Substring(scenario2.IndexOf(".") + 1, 3))
                        && scenario1.Substring(scenario1.LastIndexOf(".") + 1, 3).Equals(scenario2.Substring(scenario2.LastIndexOf(".") + 1, 3)))
                    {
                        DiagnosisResult rs = new DiagnosisResult();
                        rs.Symptom = "livelock";
                        rs.LoopIndex = -1;
                        rs.Scenario = scenario1 + " invoke"+scenario2.Substring(scenario1.IndexOf("."));
                        rs.MemoryUsage = rsItem.MemoryUsage + lList[j].MemoryUsage;
                        rs.NumberOfStates = rsItem.NumberOfStates + lList[j].NumberOfStates;
                        rs.TotalTime = rsItem.TotalTime + lList[j].TotalTime;
                        result.Add(rs);
                    }
                }
                i++;
            }
            return result;

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
  
        private VerifyAsset InsertMoreLivelockCheck(VerifyAsset asset, List<ArchMatrix> matrix,string firstCallID, string lastCallID)
        {
            StringBuilder cspLivelockCheck = new StringBuilder();
            List<string> livelockAssertList = new List<String>();
            cspLivelockCheck.AppendLine("");
            for (var i = 0; i < matrix.Count; i++)
            {
                // grad component in the middle of trace
                if(!matrix[i].ID.Equals(firstCallID) && !matrix[i].ID.Equals(lastCallID))
                {
                    string assert = "System() |= []<>( request."+ firstCallID + "-> request."+ matrix[i].ID + ")&&[]( request."+ matrix[i].ID + "-> invoke."+lastCallID+")";
                    cspLivelockCheck.AppendLine("#assert " + assert + ";");
                    livelockAssertList.Add(assert);
                    System.Diagnostics.Debug.WriteLine("@" + assert + "@");
                }
            }

            asset.CSPCode = asset.CSPCode + cspLivelockCheck.ToString();
            asset.livelockCheck = livelockAssertList;
            return asset;
        }

        [HttpPost]
        [ActionName("GenerateAsset")]
        public VerifyAsset GenerateAsset(List<ArchMatrix> matrix)
        {

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

                    // Generate code to verify livelock
                    if (i != j && matrix[i].IsStartCaller && !matrix[j].IsStartCaller)
                    {
                        string assertionName = "System() |= []( invoke." + matrix[i].ID + "-><> request." + matrix[j].ID + ")";
                        cspLTLCheck.AppendLine("#assert " + assertionName + ";");
                        livelockAssertList.Add(assertionName);
                       
                    }
                }
            }


            string templatePath = System.Web.HttpContext.Current.Request.MapPath("~\\CSP\\com-tier-tracematrix.csp");

            // read template and replace code with dynamic generated 
            string templateTxt = File.ReadAllText(templatePath);
            StringBuilder cspCode = new StringBuilder(templateTxt);
            cspCode.Replace("/**COMPONENTNUMBER**/", matrix.Count.ToString());
            cspCode.Replace("/**COMPONENTLIST**/", cspComponent.ToString());
            cspCode.Replace("/**MATRIX**/", cspMatrix.ToString());
            cspCode.Replace("/**START**/", cspStartSimulate.ToString());
            cspCode.Replace("/**DEADLOCKCHECK**/", cspDeadLockCheck.ToString());
            cspCode.Replace("/**LTLCHECK**/", cspLTLCheck.ToString());

            VerifyAsset asset = new VerifyAsset();
            asset.CSPCode = cspCode.ToString();
            asset.deadloopCheck = deadlockAssertList;
            asset.livelockCheck = livelockAssertList;

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
