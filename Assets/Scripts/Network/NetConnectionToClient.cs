using System;
using System.Collections.Generic;
using UnityEngine;

public class NetConnectionToClient : NetConnection
{
    public Unbatcher unbatcher = new();
    public int connectionID {  get; private set; }
    public string remoteAddress { get; private set; }

    public double lastPingTime = 0;

    public ExponentialMovingAverage _rtt = new (NetTime.PingWindowSize);
    public double rtt => _rtt.Value;

    //observing表示他能感知到的NetIdentity
    public readonly HashSet<NetIdentity> observing = new HashSet<NetIdentity>();
    public bool spawnedObservers;

    public NetConnectionToClient(int connectionID, string remoteAddress)
        : base()
    {
        this.connectionID = connectionID;
        this.remoteAddress = remoteAddress;
        spawnedObservers = false;
    }

    public override void Disconnect()
    {
        ready = false;
        Transport.instance.ServerDisconnect(connectionID);
    }
    protected override void SendToTransport(ArraySegment<byte> segment, int channelID = (int)Channels.Reliable)
        => Transport.instance.ServerSend(connectionID, segment, channelID);

    //将NetIdentity添加到观察列表中
    public void AddIdentityToObserving(NetIdentity identity)
    {
        if(observing.Contains(identity))
            return; 
        observing.Add(identity);
    }
    public void RemoveIdentityToObserving(NetIdentity identity)
    {
        if (!observing.Contains(identity))
            return;
        observing.Remove(identity);
    }

    //---------------------------------- TimeSnapshot
    public int snapshotBufferSizeLimit { get; private set; } = 64;

    private readonly SortedList<double, TimeSnapshot> snapshots = new();
    private ExponentialMovingAverage driftEma;
    private ExponentialMovingAverage deliveryTimeEma;
    public double remoteTimeline;
    public double remoteTimescale;
    private double bufferTimeMultiplier = 2;
    private double bufferTime => NetServer.sendInterval * bufferTimeMultiplier;
    public void OnTimeSnapshot(TimeSnapshot snapshot)
    {
        if (snapshots.Count >= snapshotBufferSizeLimit)
            return;


        SnapshotInterpolation.InsertAndAdjust(
            snapshots,
            snapshot,
            NetClient.snapshotSettings.bufferLimit,
            ref remoteTimeline,
            ref remoteTimescale,
            ref driftEma,
            ref deliveryTimeEma,
            NetServer.sendInterval,
            bufferTime,
            NetClient.snapshotSettings.catchupSpeed,
            NetClient.snapshotSettings.slowdownSpeed,
            NetClient.snapshotSettings.catchupNegativeThreshold,
            NetClient.snapshotSettings.catchupPositiveThreshold
        );

    }
    public void UpdateTimeInterpolation()
    {
        if (snapshots.Count > 0)
        {
            SnapshotInterpolation.UpdateTime(Time.unscaledDeltaTime, ref remoteTimeline, remoteTimescale);
            SnapshotInterpolation.StepInterpolation(snapshots, remoteTimeline, out _, out _, out _);
        }
    }
}
