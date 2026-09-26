using System;
using System.Collections.Generic;

//Batcher用以存储多个写入write的msg最后再update中被一口气发出(?
public class Batcher
{
    public const int TIMESTAMP_HEADER_SIZE = sizeof(double);
    private readonly Queue<NetWriterPooled> batches = new Queue<NetWriterPooled>();
    private NetWriterPooled targetBatch;
    private double batchTS;//上一个被放入write的时间戳

    private readonly int capacity;
    public Batcher(int capacity)
    {
        this.capacity = capacity;
    }

    public void Clear()
    {
        if (targetBatch != null)
        {
            NetWriterPool.Return(targetBatch);
            targetBatch = null;
            batchTS = 0;
        }
        while(batches.Count > 0)
        {
            NetWriterPooled batcher = batches.Dequeue();
            NetWriterPool.Return(batcher);
        }
        batches.Clear();
    }

    public static int MessageHeaderSize(int messageSize) 
        => Compression.VarUIntSize((ulong)messageSize);
    public static int MaxMessageOverhead(int messageSize) 
        => TIMESTAMP_HEADER_SIZE + MessageHeaderSize(messageSize);

    public void AddMessage(ArraySegment<byte> message, double timeStamp)
    {
        if(targetBatch != null && batchTS != timeStamp)
        {
            batches.Enqueue(targetBatch);
            targetBatch = null;
        }
        int headerSize = Compression.VarUIntSize((ulong)message.Count);
        int neededSize = headerSize + message.Count;

        if (targetBatch != null && targetBatch.Position + neededSize > capacity)
        {
            batches.Enqueue(targetBatch);
            targetBatch = null;
        }

        if(targetBatch == null)
        {
            targetBatch = NetWriterPool.Get();
            NetWriter.WriteDouble(targetBatch, timeStamp);
            batchTS = timeStamp;
        }

        Compression.CompressVarUInt(targetBatch, (ulong)message.Count);//已经写入了headersize
        targetBatch.WriteBytes(message.Array, message.Offset, message.Count);
    }
    public bool GetBatch(NetWriter writer)
    {
        if(batches.Count > 0)
        {
            NetWriterPooled data = batches.Dequeue();
            WriteIntoWriterAndReturn(data, writer);
            return true;
        }
        if(targetBatch != null)
        {
            WriteIntoWriterAndReturn(targetBatch, writer);
            targetBatch = null;
            return true;
        }
        return false;
    }
    private static void WriteIntoWriterAndReturn(NetWriterPooled batch, NetWriter writer)
    {
        if (writer.Position != 0)
            throw new ArgumentException($"Batch Take one piece of message as a unit" +
                "if you want to parse data from batch, please use an empty writer");


        ArraySegment<byte> segment = batch.ToArraySegment();
        writer.WriteBytes(segment.Array, segment.Offset, segment.Count);

        NetWriterPool.Return(batch);
    }
}
