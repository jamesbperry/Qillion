# Deploying Qillion

------
## Overview  
Qillion is an Azure Function app. To deploy it, you will

- Create a bunch of prerequisite items
- Set a bunch of settings
- Publish it via CLI
- Initialize the Stream list

## Prerequisites

Create one each of the following:
- Azure Function App (v1, Consumption model). Note its name.
- Azure Service Bus Queue. Note the connection string and queue name.
- Azure Storage Acccount. Note the connection string.
- Google Pub/Sub Topic, [via the Google Cloud Console](https://console.cloud.google.com/cloudpubsub/topicList). Note the Topic's ID as well as the overarching Project's ID.

Obtain:
- Your Qi cluster's endpoint and security resource
- a Qi namespace full of tags you want to pump
- Qi login credentials to that tenant: an App ID and Secret. You may need to create an Application in AAD.

## Google Authentication
This Function Application must authenticate with Google in order to publish values there.  

In the Google Cloud Platform [create a service account](https://console.cloud.google.com/apis/credentials) with Pub/Sub Publish and View permissions.  

You will be given a ```.json``` file. Rename this file to ```googleCredential.json``` and add it to the ```OSIResearch.Qillion.Functions``` project root.

## Settings

There are a number of required settings that your Function App must have once it is deployed. 

One way to set these is va a local.settings.config file. It should contain, at minimum,
```
{
  "Values": {
    "AzureWebJobsStorage": "<system provided>",
    "AzureWebJobsDashboard": "<system provided>",
    "StreamBatchesBlobContainerName": "<a container name to be used exclusively by this instance. If it already exists, all contents will be deleted.>",
    "QiStreamsPerWorker": <Number of streams processed by each worker. Stream count modulo this number will determine the number of workers. Not a string.>,
    "QiUrl": "<URL to your Qi instance, e.g. [https://historianmain.osipi.com].>",
    "QiResource": "<Qi application ID in AAD, e.g. [https://pihomemain.onmicrosoft.com/ocsapi]>",
    "QiAccountId": "<the Guid of your OCS tenant>",
    "QiClientId": "<OCS service account id>",
    "QiClientSecret": "<OCS service account key>",
    "QiNamespace": "<namespace of the Qi tags you want to read>",
    "QiStreamFilter": "<OCS query syntax used to filter the list of streams"
    "GoogleProjectId": "<the destination Google PubSub will have a project id. It's found in the GCS portal.>",
    "GoogleTopicId": "<the destination Google PubSub needs a topic to write to. Create one in the GCS portal. Here, use its short id, not its path.>",
    "QillionTriggerBus": "<connection string to an Azure Service Bus, which needs to contain a queue named quillion>", 
    "QillionStorage": "<connection string to an Azure Storage account"
  }
}
```

Note that the AzureWebJobs* properties are used by the Azure Functions platform.

The values in `local.settings.config` are also used during local debugging.

## Scheduling

The `StreamBatchEnqueuer` function runs on a timer trigger. This value is currently [set in code, in a class attribute](
"QiStreamBatchEnqueuer.cs&line=16&lineStyle=plain&lineEnd=16&lineStartColumn=27&lineEndColumn=43). The default interval is 15m.

## Deployment

To publish the Application along with the settings from `local.settings.json`, use the CLI per [documentation](https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local#publish).  
```
func azure subscriptions list
func azure subscriptions set <id>
func azure functionapp publish <appName> --publish-local-settings -i --overwrite-settings -y
```

If you publish via the UI in Visual Studio, these application settings must be applied manually, e.g. in the Azure Portal

## Initialization

The `QiStreamBatcher` function is what resolves the list of Qi Streams to be pumped. Until it is run, no data will be pumped. Anytime Qi settings are changed (Namespace, Query, StreamsPerWorker) this must be re-run.

`QiStreamBatcher` can be run manually or via HTTP trigger. It can be re-run at any time, but it's probably best to pause the other functions first.

## Testing

To see what's flowing through the Google PubSub bus, create a subscription in the Console.
With the Google Cloud SDK installed and initialzied, you can use the CLI to pop some items from the queue:
```
gcloud beta pubsub subscriptions pull <fullSubscriptionId> --max-messages=100 --auto-ack
```