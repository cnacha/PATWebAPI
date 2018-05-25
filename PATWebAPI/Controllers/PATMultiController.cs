using PAT.Common.Classes.ModuleInterface;
using PATWebAPI.Models;
using PATWebAPI.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace PATWebAPI.Controllers
{
    public class PATMultiController : ApiController
    {
        List<VerifyResult> resultSet;

        [HttpPost]
        [ActionName("SyncVerify")]
        public List<VerifyResult> SyncVerify(List<ArchDesignConfig> designSet)
        {
            PATUtil util = new PATUtil();
            // generate CSP Code
            resultSet = new List<VerifyResult>();
            List <ThreadedVerificationExecuter<VerifyResult>> threadsList = new List<ThreadedVerificationExecuter<VerifyResult>>();
            ThreadedVerificationExecuter<VerifyResult> thread;
            PAT.CSP.ModuleFacade modulebase = new PAT.CSP.ModuleFacade();
            foreach (ArchDesignConfig designConfig in designSet)
            {
                VerifyAsset verifyAsset = util.GenerateAsset(designConfig.matrix);
                SpecificationBase specbase = modulebase.ParseSpecification(verifyAsset.CSPCode, string.Empty, string.Empty);
                thread = new ThreadedVerificationExecuter<VerifyResult>(collectVerifyResult, verifyAsset, specbase);
                threadsList.Add(thread);
                thread.Start();
            }

            while(resultSet.Count < designSet.Count)
            {
                System.Diagnostics.Debug.WriteLine("Waiting for result "+resultSet.Count);
            }
            return resultSet;
        }

        public void collectVerifyResult(VerifyResult result)
        {
            resultSet.Add(result);
        }
    }

   
}
