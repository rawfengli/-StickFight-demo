using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//msg in batcher: [msg WriteIn ST][msg size header][msg header][msg content]|[msg size header][msg header][msg content]...
public class Unbatcher
{
    public const int TIMESTAMP_HEADER_SIZE = sizeof(double);
    private readonly Queue<NetWriterPooled> batches = new Queue<NetWriterPooled>();
    private readonly NetReader reader = new NetReader(new byte[0]);
    private double readerRemoteTS;
    public int BatchesCount => batches.Count;

    public void Clear()
    {
        while (batches.Count > 0)
        {
            NetWriterPooled writer = batches.Dequeue();
            NetWriterPool.Return(writer);
        }
        reader.SetBuffer(Array.Empty<byte>());
    }
    private void ResetreaderWithNewBatch(NetWriterPooled batch)
    {
        reader.SetBuffer(batch.ToArraySegment());

        readerRemoteTS = NetReader.ReadDouble(reader);
    }
    public bool AddBatch(ArraySegment<byte> batch)
    {
        if (batch.Count < TIMESTAMP_HEADER_SIZE)
            return false;

        NetWriterPooled writer = NetWriterPool.Get();
        writer.WriteBytes(batch.Array, batch.Offset, batch.Count);

        if (batches.Count == 0)
            ResetreaderWithNewBatch(writer);

        batches.Enqueue(writer);
        return true;
    }
    public bool ReceiveNextMessage(out ArraySegment<byte> message, out double remoteTS)
    {
        message = default;
        remoteTS = 0;

        if (batches.Count == 0)
            return false;

        if (reader.Capacity == 0)//invalid reader
            return false;

        //函数每次只读取一条msg，但是一条batch中可能存在多个msg，所以要先取batch到reader中，然后从batch中读消息
        if (reader.Remaining == 0)        
        {
            NetWriterPooled writer = batches.Dequeue();
            NetWriterPool.Return(writer);

            if (batches.Count > 0)
            {
                NetWriterPooled next = batches.Peek();
                ResetreaderWithNewBatch(next);
            }
            else return false;
        }

        remoteTS = readerRemoteTS;

        if (reader.Remaining == 0)
            return false;//queue and reader all empty

        int size = (int)Compression.DecompressVarUInt(reader);
        if (reader.Remaining < size)
        {
            Clear();
            throw new InvalidOperationException($"GetNextMessage: malformed batch with message size {size} > remaining {reader.Remaining}, Invaild message, Disconnect.");
        }
        message = reader.ReadBytesSegment(size);
        return true;
    }

}
