using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Linq;
using System.Configuration;
using Microsoft.ServiceBus.Messaging;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OSIResearch.Qillion.Functions
{
    public static class QiStreamBatchEnqueuer
    {
        [FunctionName("QiStreamBatchEnqueuer")]
        public static async Task Run(
            [TimerTrigger("0 */15 * * * *")]TimerInfo myTimer, 
            [ServiceBus("qillion", AccessRights.Manage, Connection = "QillionTriggerBus")]ICollector<BatchEntity> batchQueue, 
            TraceWriter log)
        {
            log.Info($"Stream Batch Enqueuer executed at: {DateTime.Now}");

            QiStreamBatchRepository repository = new QiStreamBatchRepository();
            int batchCount = await repository.GetStreamBatchCountAsync();

            IEnumerable<BatchEntity> batchEntities = Enumerable.Range(0, batchCount).Select(i => new BatchEntity() { id = i });
            foreach (BatchEntity batchEntity in batchEntities)
            {
                batchQueue.Add(batchEntity);
            }
        }
    }
}
