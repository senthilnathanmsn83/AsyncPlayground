using System.Threading.Channels;
using AsyncPlayground.Examples.Support;

namespace AsyncPlayground.Examples.Synchronization;

/// <summary>
/// System.Threading.Channels is the modern, fully-async producer/consumer queue —
/// no locks, no polling, backpressure built in via a bounded channel. This runs one
/// producer and two consumers concurrently over a single channel.
/// </summary>
sealed class ProducerConsumerChannel : IAsyncExample
{
    public string Category => "05. Synchronization";
    public string Title => "Producer/consumer with System.Threading.Channels";
    public string Summary => "A bounded Channel<T> feeding two concurrent consumers, with backpressure when the channel fills up.";

    public async Task RunAsync(CancellationToken ct)
    {
        // Bounded to 2: once 2 unread items are buffered, the producer's WriteAsync
        // will asynchronously wait for a consumer to catch up. This is backpressure —
        // it stops a fast producer from unboundedly outrunning slow consumers.
        var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(capacity: 2)
        {
            FullMode = BoundedChannelFullMode.Wait,
        });

        Task producer = ProduceAsync(channel.Writer, count: 6, ct);
        Task consumer1 = ConsumeAsync("consumer-1", channel.Reader, ct);
        Task consumer2 = ConsumeAsync("consumer-2", channel.Reader, ct);

        await producer;
        await Task.WhenAll(consumer1, consumer2);

        Log.Write("Both consumers pulled from the same channel — work naturally load-balanced between them.", ConsoleColor.Cyan);
    }

    private static async Task ProduceAsync(ChannelWriter<int> writer, int count, CancellationToken ct)
    {
        for (int i = 1; i <= count; i++)
        {
            await writer.WriteAsync(i, ct); // suspends here if the channel is full — that's backpressure
            Log.Write($"produced {i}");
            await Task.Delay(50, ct);
        }

        writer.Complete(); // signals readers that no more items are coming, so ReadAllAsync can finish
        Log.Write("producer complete, channel closed");
    }

    private static async Task ConsumeAsync(string name, ChannelReader<int> reader, CancellationToken ct)
    {
        // ReadAllAsync asynchronously waits for new items and stops cleanly once the
        // writer calls Complete() and the channel drains — no manual loop-and-check needed.
        await foreach (int item in reader.ReadAllAsync(ct))
        {
            Log.Write($"{name} processing {item}");
            await Task.Delay(120, ct); // slower than the producer, so backpressure will kick in
        }
        Log.Write($"{name} finished — channel drained");
    }
}
