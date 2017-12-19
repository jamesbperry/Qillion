using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using System.Configuration;

namespace OSIResearch.Qillion.Functions
{
    public static class QiStreamBatcher
    {
        [FunctionName("QiStreamBatcher")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)]HttpRequestMessage req,
            TraceWriter log,
            Binder binder)
        {
            QiStreamBatchRepository repository = new QiStreamBatchRepository();
            await repository.DeleteExistingStreamBatches().ConfigureAwait(false);

            string batchSizeString = ConfigurationManager.AppSettings["QiStreamsPerWorker"];
            int batchSize;
            if (!int.TryParse(batchSizeString, out batchSize)) batchSize = 10000;
            List<string> qiStreams = await GetQiStreamsAsync().ConfigureAwait(false);
            int batchCount = (int)Math.Ceiling(qiStreams.Count * 1d / batchSize);

            List<QiStreamEntity> qiStreamEntities = qiStreams.Select((streamId, index) => new QiStreamEntity() { id = streamId, batchid = index % batchCount }).ToList();
            var qiStreamBatches = qiStreamEntities.GroupBy(qse => qse.batchid).ToDictionary(group => group.Key, group => group.Select(qse => qse.id).ToList());

            await repository.SaveStreamBatchesAsync(qiStreamBatches);

            return req.CreateResponse(HttpStatusCode.OK, $"{qiStreamEntities.Count} streams resolved");
        }

        private static async Task<List<string>> GetQiStreamsAsync()
        {
            string qiNamespaceName = ConfigurationManager.AppSettings["QiNamespace"]; 
            string qiQuery = ConfigurationManager.AppSettings["QiStreamFilter"];

            QiClientSlim qiClient = new QiClientSlim();
            return await qiClient.GetQiStreamsAsync(qiNamespaceName, qiQuery);
        }
    }
}
