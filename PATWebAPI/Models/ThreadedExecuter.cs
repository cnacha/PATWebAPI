using PAT.Common.Classes.ModuleInterface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Web;

namespace PATWebAPI.Models
{
    public class ThreadedExecuter<T> where T : class
    {
        public delegate void CallBackDelegate(T returnValue);
        public delegate T MethodDelegate();
        private CallBackDelegate callback;
        private MethodDelegate method;

        private Thread t;

        public ThreadedExecuter(MethodDelegate method, CallBackDelegate callback)
        {
            this.method = method;
            this.callback = callback;
            t = new Thread(this.Process);
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
        private void Process()
        {
            T stuffReturned = method();
            callback(stuffReturned);
        }
    }
}