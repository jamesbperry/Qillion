using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OSIResearch.Qillion.Functions
{
    public class QiStreamBatchRepository
    {
        private readonly string batchCountPropertyName = "BatchCount";
        public QiStreamBatchRepository()
        {
            StorageConnectionString = ConfigurationManager.AppSettings["QillionStorage"];
            StorageContainerName = ConfigurationManager.AppSettings["StreamBatchesBlobContainerName"];
        }

        public string StorageConnectionString { get; set; }
        public string StorageContainerName { get; set; }

        public async Task<CloudBlobContainer> GetBatchContainerAsync()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();

            CloudBlobContainer container = blobClient.GetContainerReference(StorageContainerName);
            await container.CreateIfNotExistsAsync();
            return container;
        }

        public async Task<int> GetStreamBatchCountAsync()
        {
            CloudBlobContainer container = await GetBatchContainerAsync();
            if (container.Metadata?.Any() != true) return 0;
            string batchCountString = container.Metadata[batchCountPropertyName];
            return int.Parse(batchCountString);
        }

        public async Task<List<string>> GetStreamsInBatch(int batchId)
        {
            CloudBlobContainer container = await GetBatchContainerAsync();
            CloudBlockBlob blob = container.GetBlockBlobReference($"batch{batchId}");
            string streamsIdsJson = await blob.DownloadTextAsync();
            return JsonConvert.DeserializeObject<List<string>>(streamsIdsJson);
        }

        public async Task DeleteExistingStreamBatches()
        {
            CloudBlobContainer container = await GetBatchContainerAsync();

            List<Task> deletionTasks = container.ListBlobs().OfType<CloudBlockBlob>().Select(blob => blob.DeleteAsync()).ToList();
            await Task.WhenAll(deletionTasks).ConfigureAwait(false);
        }

        public async Task SaveStreamBatchesAsync(Dictionary<int, List<string>> streamBatches)
        {
            CloudBlobContainer container = await GetBatchContainerAsync();

            List<Task> uploadTasks = new List<Task>();
            foreach (var streamBatch in streamBatches)
            {
                int batchId = streamBatch.Key;
                List<string> streamIds = streamBatch.Value;
                string streamIdsJson = JsonConvert.SerializeObject(streamIds);
                CloudBlockBlob blob = container.GetBlockBlobReference($"batch{batchId}");
                Task uploadTask = blob.UploadTextAsync(streamIdsJson);
                uploadTasks.Add(uploadTask);
            }
            await Task.WhenAll(uploadTasks).ConfigureAwait(false);

            container.Metadata[batchCountPropertyName] = streamBatches.Count.ToString();
            await container.SetMetadataAsync();
        }

    }
}
