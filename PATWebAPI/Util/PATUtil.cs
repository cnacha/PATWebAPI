using PAT.Common.Classes.ModuleInterface;
using PATWebAPI.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;

namespace PATWebAPI.Util
{
    public class PATUtil
    {
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
                string deadlockAssert = "System() |= []<> request." + matrix[i].ID;
                cspDeadLockCheck.AppendLine("#assert " + deadlockAssert + ";");
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

        public DiagnosisResult executeAssertion(SpecificationBase spec, String assertionName)
        {
            AssertionBase assertion = spec.AssertionDatabase[assertionName];
            AssertionVerifier verifier = new AssertionVerifier(assertion, 1, 0);
            return verifier.Run();
        }
    }
}