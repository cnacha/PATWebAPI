using PAT.Common.Classes.ModuleInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace PATWebAPI.Models
{
    public class ThreadedAssertionExecuter<T> where T : class
    {
        public delegate void CallBackDelegate(T returnValue);
        public delegate T MethodDelegate();
        private CallBackDelegate callback;
        private MethodDelegate method;

        private Thread t;

        public ThreadedAssertionExecuter( CallBackDelegate callback, SpecificationBase spec, String asrtName)
        {
            this.callback = callback;
            t = new Thread(() => executeAssertion(spec, asrtName));
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
        public void executeAssertion(SpecificationBase spec, String assertionName)
        {

            AssertionBase assertion = spec.AssertionDatabase[assertionName];
            AssertionVerifier verifier = new AssertionVerifier(assertion, 1, 0);
            T stuffReturned = (T)Convert.ChangeType(verifier.Run(), typeof(T));
            callback(stuffReturned);
        }
    }
}