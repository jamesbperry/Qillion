# Introduction 
This is a data pump which mirrors Qi data into Google Pub/Sub. It is an Azure Function Application comprised of three functions, designed to scale arbitrarily high.

On an interval, the application queries a set of Qi Streams for their last value, and publishes them to a Google Pub/Sub topic.

Here's  how it works:
- At your command, resolves a list of Qi streams to be forwarded. You get to specify the namespace and a filter string. If this hasn't been done, nothing else will succeed. It can be re-run at any time. (Function: `QiStreamBatcher`)
- Triggers on an interval of your choosing. Spins up one worker for each batch of Qi Streams. Batch size is configurable and observed during the previous. (Function: `QiStreamBatchEnqueuer`)
- Each worker takes a batch of Qi Streams. It queries the latest value of each Qi Stream, and forwards that value to Google Pub/Sub. Values are identified by StreamId. (Function: `QiStreamBatchWorker`)

# Getting Started
See the [deployment guide](./src/OSIResearch.Qillion/OSIResearch.Qillion.Functions/setup.md).

# Why 'Qillion'?
I have no idea how much a qillion is, but it's definitely larger than a googol. Probably by about 3.