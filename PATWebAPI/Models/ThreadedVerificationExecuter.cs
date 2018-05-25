using PAT.Common.Classes.ModuleInterface;
using PATWebAPI.Controllers;
using PATWebAPI.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Web;

namespace PATWebAPI.Models
{
    public class ThreadedVerificationExecuter<T> where T : class
    {
        public delegate void CallBackDelegate(T returnValue);
        public delegate T MethodDelegate();
        private CallBackDelegate callback;
        private MethodDelegate method;

        private Thread t;

        public ThreadedVerificationExecuter( CallBackDelegate callback, VerifyAsset asset, SpecificationBase specbase)
        {
            this.callback = callback;
            t = new Thread(() => executeVerification(asset, specbase));
        }
        public void Start()
        {
            t.Start();
        }
        public Boolean isAlive()
        {
            return t.IsAlive;
        }
        public void Abort()
        {
            t.Abort();
            callback(null); 
        }
        public void executeVerification(VerifyAsset verifyAsset, SpecificationBase specbase)
        {
            //initialize CSP module with CSP Code
            
            PATUtil util = new PATUtil(); 
            Stopwatch sw = new Stopwatch();
            sw.Start();

            // run assertions and gather results
            ConcurrentBag<DiagnosisResult>  results = new ConcurrentBag<DiagnosisResult>();
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


            T stuffReturned = (T)Convert.ChangeType(verResult, typeof(T));
            callback(stuffReturned);

        }
    }
}