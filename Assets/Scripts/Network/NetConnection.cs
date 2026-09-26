using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements.Experimental;

//msg in batcher: [msg WriteIn ST][msg size header][msg header][msg content]|[msg size header][msg header][msg content]...
public abstract class NetConnection 
{
    public const int OFFLINE_CONNECTION_ID = 0;
    protected Dictionary<int, Batcher> batches = new Dictionary<int, Batcher>();

    //for client, after client rec hellp form server
    public virtual bool ready { get; set; }
    public float lastActiveMessageTime;

    public double remoteTS;
    public bool isAuthenticated;

    public NetIdentity playerIdentity;

    public readonly HashSet<NetIdentity> owned = new HashSet<NetIdentity>();
    public NetConnection()
    {
        lastActiveMessageTime = NetTime.localTimeF;
    }
    public bool IsAlive(float timeout)
    {
        if(NetTime.localTime - lastActiveMessageTime < timeout)
            return true;
        else
            return false;
    }
    protected Batcher GetBatchForChannel(int channel)
    {

        if (!batches.TryGetValue(channel, out Batcher batch))
        {
            int capacity = Transport.instance.GetBatchCapacity(channel);
            batch = new Batcher(capacity);
            batches[channel] = batch;
        }
        return batch;
    }
    public abstract void Disconnect();

    public void Send<T>(T msg, int channel = (int)Channels.Reliable)
        where T : struct, INetMessage
    {
        using (NetWriterPooled writer = NetWriterPool.Get())
        {
            NetMessages.Pack(msg, writer);

            int max = NetMessages.MaxMessageSize(channel);
            if (writer.Position > max)
            {
                Debug.LogError($"NetworkConnection.Send: message of type {typeof(T)} with a size of {writer.Position} bytes is larger than the max allowed message size in one batch: {max}.");
                return;
            }

            Send(writer.ToArraySegment(), channel);
        }
    }
    protected virtual void Send(ArraySegment<byte> segment, int channel = (int)Channels.Reliable)
    {
        GetBatchForChannel(channel).AddMessage(segment, NetTime.localTime);
    }

    public virtual void Update()
    {
        foreach(KeyValuePair<int, Batcher> kvp in batches)
        {
            using (NetWriterPooled writer = NetWriterPool.Get())
            {
                while (kvp.Value.GetBatch(writer))
                {
                    ArraySegment<byte> segment = writer.ToArraySegment();
                    SendToTransport(segment, kvp.Key);
                    writer.Position = 0;
                }
            }
        }
    }
    protected abstract void SendToTransport(ArraySegment<byte> segment, int channel = (int)Channels.Reliable);
    public virtual void Clean()
    {
        foreach (Batcher batcher in batches.Values)
        {
            batcher.Clear();
        }
    }

}
