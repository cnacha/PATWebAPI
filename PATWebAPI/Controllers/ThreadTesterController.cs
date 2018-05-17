using PATWebAPI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Web.Http;

namespace PATWebAPI.Controllers
{
    public class ThreadTesterController : ApiController
    {
        [HttpGet]
        [ActionName("runParallel")]
        public void runParallel()
        {
            ThreadedExecuter<String> executer1 = new ThreadedExecuter<String>(longThread, callBack);
            ThreadedExecuter<String> executer2 = new ThreadedExecuter<String>(longThread, callBack);
            ThreadedExecuter<String> executer3 = new ThreadedExecuter<String>(longThread, callBack);
            executer1.Start();
            executer2.Start();            
            executer3.Start();
            while(executer1.isAlive() || executer2.isAlive() || executer3.isAlive())
            {
                System.Diagnostics.Debug.WriteLine("Waiting to finish...");
            }
        }
        [HttpGet]
        [ActionName("runSequence")]
        public void runSequence()
        {
            longThread();
            longThread();
            longThread();
        }
        private String longThread()
        {
            System.Diagnostics.Debug.WriteLine("Start Processing");
            Thread.Sleep(3000);
            System.Diagnostics.Debug.WriteLine("End Processing");
            return "finished";
        }

        private void callBack(String result)
        {
            System.Diagnostics.Debug.WriteLine("called back now");
        }
    }

    
}
