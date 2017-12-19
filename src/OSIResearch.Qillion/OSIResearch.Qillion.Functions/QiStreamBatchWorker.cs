using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.ServiceBus.Messaging;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Configuration;
using System.Net.Http;
using System.Threading;
using Google.Cloud.PubSub.V1;
using Google.Apis.Auth.OAuth2;
using static Google.Cloud.PubSub.V1.SimplePublisher;
using Grpc.Auth;
using System;
using Grpc.Core;
using System.Linq;

namespace OSIResearch.Qillion.Functions
{
    public static class QiStreamBatchWorker
    {
        [FunctionName("QiStreamBatchWorker")]
        public static async Task Run([ServiceBusTrigger("qillion", AccessRights.Manage, Connection = "QillionTriggerBus")]BatchEntity batchEntity, TraceWriter log, Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            log.Info($"Qi stream batch worker processing batch {batchEntity.id}");

            BindingRedirector.RedirectAssembly("Google.Apis.Auth", new Version("1.28.0.0"), "4b01fa6e34db77ab");
            BindingRedirector.RedirectAssembly("Google.Apis.Auth.PlatformServices", new Version("1.28.0.0"), "4b01fa6e34db77ab");

            QiStreamBatchRepository repository = new QiStreamBatchRepository();
            List<string> streamIds = await repository.GetStreamsInBatch(batchEntity.id);

            string qiNamespace = ConfigurationManager.AppSettings["QiNamespace"];
            QiClientSlim qiClient = new QiClientSlim();

            await GetAndSendSnapshotsAsync(qiNamespace, streamIds, qiClient, context, log);
        }

        private static async Task GetAndSendSnapshotsAsync(string namespaceId, List<string> streamIds, QiClientSlim qiClient,
            Microsoft.Azure.WebJobs.ExecutionContext context, TraceWriter log = null)
        {
            SimplePublisher busWriter = GetGoogleBusWriter(context);

            List<Task<Task>> snapshotTasks = new List<Task<Task>>(streamIds.Count);
            foreach (string streamId in streamIds)
            {
                string streamIdCopy = streamId;
                Task<Task> snapshotTask = GetAndSendAsync(qiClient, namespaceId, streamIdCopy, busWriter, log);
                snapshotTasks.Add(snapshotTask);
            }
            await Task.WhenAll(snapshotTasks);

            //Wait until messages are flushed from the bus
            await busWriter.ShutdownAsync(CancellationToken.None);

            var publishingTasks = snapshotTasks.Select(outerTask => outerTask.Result);
            await Task.WhenAll(publishingTasks);
        }

        private static async Task<Task> GetAndSendAsync(QiClientSlim qiClient, string namespaceId, string streamId, SimplePublisher busWriter, TraceWriter log = null)
        {
            string snapshotJson = await qiClient.GetLatestValueJsonAsync(namespaceId, streamId).ConfigureAwait(false);
            if (snapshotJson == null) return Task.CompletedTask;
            string identifiedSnapshotJson = $"{{ streamId: \"{streamId}\", values: {snapshotJson} }}";
            return busWriter.PublishAsync(identifiedSnapshotJson);
        }

        private static SimplePublisher GetGoogleBusWriter(Microsoft.Azure.WebJobs.ExecutionContext context)
        {
            string topicId = ConfigurationManager.AppSettings["GoogleTopicId"];
            string projectId = ConfigurationManager.AppSettings["GoogleProjectId"];

            //Google credential expected to be a local file called googleCredential.json, copied to output

            TopicName topicName = new TopicName(projectId, topicId);
            string credentialPath = Path.Combine(context.FunctionDirectory, "..\\googleCredential.json");

            GoogleCredential credential = ReadGoogleCredentialFile(credentialPath);
            credential = credential.CreateScoped(PublisherClient.DefaultScopes);
            ClientCreationSettings clientSettings = new ClientCreationSettings(credentials: credential.ToChannelCredentials());

            Channel channel = new Channel(PublisherClient.DefaultEndpoint.Host, PublisherClient.DefaultEndpoint.Port, credential.ToChannelCredentials());
            PublisherClient client = PublisherClient.Create(channel);
            return SimplePublisher.Create(topicName, new[] { client });
        }

        private static GoogleCredential ReadGoogleCredentialFile(string filePath)
        {
            using (var credentialFileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return GoogleCredential.FromStream(credentialFileStream);
            }
        }
    }
}
