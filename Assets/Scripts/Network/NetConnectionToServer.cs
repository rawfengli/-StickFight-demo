using System;
using UnityEngine;

public class NetConnectionToServer : NetConnection
{
    public override bool ready
    {
        get => NetClient.ready;
        set => Debug.LogWarning("Ready property on NetConnectionToServer is same with NetClient.ready, please use NetClient.ready instead.");
    }
    public override void Disconnect()
    {
        Transport.instance.ClientDisconnect();
    }
    protected override void SendToTransport(ArraySegment<byte> segment, int channelID = (int)Channels.Reliable)
        => Transport.instance.ClientSend(segment, channelID);
}
