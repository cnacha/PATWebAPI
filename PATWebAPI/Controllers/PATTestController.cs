using PAT.Common.Classes.ModuleInterface;
using PATWebAPI.Models;
using PATWebAPI.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace PATWebAPI.Controllers
{
    public class PATTestController : ApiController
    {
        [HttpPost]
        [ActionName("SingleVerify")]
        public VerifyResult SimpleVerify(List<ArchMatrix> matrix)
        {
            PATUtil util = new PATUtil();
            // generate CSP Code
            VerifyAsset verifyAsset = util.GenerateAsset(matrix);
            int totalAsert = verifyAsset.deadloopCheck.Count + verifyAsset.livelockCheck.Count;
            System.Diagnostics.Debug.WriteLine("total Assertion" + totalAsert);

            //initialize CSP module with CSP Code
            PAT.CSP.ModuleFacade modulebase = new PAT.CSP.ModuleFacade();

            Stopwatch sw = new Stopwatch();
            sw.Start();
            SpecificationBase specbase = modulebase.ParseSpecification(verifyAsset.CSPCode, string.Empty, string.Empty);

            // run assertions and gather results
             results = new ConcurrentBag<DiagnosisResult>();
            foreach (String assertionName in verifyAsset.deadloopCheck)
            {
                results.Add(util.executeAssertion(specbase, assertionName));
            }
            foreach (String assertionName in verifyAsset.livelockCheck)
            {
                results.Add(util.executeAssertion(specbase, assertionName));
            }
            sw.Stop();
            VerifyResult verResult = new VerifyResult();
            verResult.elapseTime = sw.ElapsedMilliseconds;
            verResult.diagnosisList = results;
            System.Diagnostics.Debug.WriteLine("Total Result" + results.Count);

            return verResult;
        }

        ConcurrentBag<DiagnosisResult> results;

        [HttpPost]
        [ActionName("MultiVerify")]
        public VerifyResult MultiVerify(List<ArchMatrix> matrix)
        {
            // change this for performance tuning
            int maxConcurrent = 8;

            PATUtil util = new PATUtil();
            // generate CSP Code
            VerifyAsset verifyAsset = util.GenerateAsset(matrix);
            int totalAsert = verifyAsset.deadloopCheck.Count + verifyAsset.livelockCheck.Count;
            System.Diagnostics.Debug.WriteLine("total Assertion" + totalAsert);

            //initialize CSP module with CSP Code
            PAT.CSP.ModuleFacade modulebase = new PAT.CSP.ModuleFacade();
            Stopwatch sw = new Stopwatch();
            sw.Start();
            SpecificationBase specbase = modulebase.ParseSpecification(verifyAsset.CSPCode, string.Empty, string.Empty);

            // run assertions and gather results
            results = new ConcurrentBag<DiagnosisResult>();
            ThreadedAssertionExecuter<DiagnosisResult> executer;
            List<ThreadedAssertionExecuter<DiagnosisResult>> threadsList = new List<ThreadedAssertionExecuter<DiagnosisResult>>();
            foreach (String assertionName in verifyAsset.deadloopCheck)
            {
                threadsList.Add(new ThreadedAssertionExecuter<DiagnosisResult>(callBack, specbase, assertionName));
            }
            foreach (String assertionName in verifyAsset.livelockCheck)
            {
                threadsList.Add(new ThreadedAssertionExecuter<DiagnosisResult>(callBack, specbase, assertionName));
            }
           
            for (int i=0; i < totalAsert; i+= maxConcurrent)
            {
                int beforeNumResult = results.Count;
                int num = maxConcurrent;
                if (i + maxConcurrent > (totalAsert-1))
                    num = totalAsert % maxConcurrent;
                for(int j=0; j<num; j++)
                {
                //    System.Diagnostics.Debug.WriteLine("Starting ->" + i +" "+j);
                    threadsList[i+j].Start();
                }

                // gather results
                while (results.Count < beforeNumResult+num)
                {
                  //  System.Diagnostics.Debug.WriteLine("Waiting to finish..." + results.Count);
                }
            }
            sw.Stop();
            VerifyResult verResult = new VerifyResult();
            verResult.elapseTime = sw.ElapsedMilliseconds;
            verResult.diagnosisList = results;
            System.Diagnostics.Debug.WriteLine("Total Result" + results.Count);

            return verResult;
        }

        public void callBack(DiagnosisResult result)
        {
            results.Add(result);
        }

       
    }
}
