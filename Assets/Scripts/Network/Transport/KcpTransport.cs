using System;
using System.Net;
using UnityEngine;
using static KcpPeer;

public class KcpTransport : Transport
{
    [Header("Common")]

    [SerializeField]
    private ushort port = 8001;
    public override ushort Port { get => port; set => port = value; }


    //public bool dualMode = true;
    public bool NoDelay = true;
    public uint NetUpdateInterval = 20;//for transport update,default : 0.02f, fixedupdate
    public int Timeout = 10000;
    public int RecvBufferSize = 1024 * 1027 * 7;//7MB
    public int SendBufferSize = 1024 * 1027 * 7;

    [Header("Advanced")]
    public int FastResend = 3;
    public uint ReceiveWindowSize = 4096; 
    public uint SendWindowSize    = 4096;
    public uint MaxRetransmit = KCP.DEADLINK * 2;//xmit
                                        
    private KcpConfig config;
    private const int MTU = KCP.MTU_DEF;
    private KcpServer server;
    private KcpClient client;

    //const default
    private const int RELIABLE_CHANNEL = 0;
    private const int UNRELIABLE_CHANNEL = 1;

    private void Awake()
    {
        config = new KcpConfig(
            RecvBufferSize, SendBufferSize, MTU, //buffer size
            NoDelay, NetUpdateInterval, FastResend,//transport conntrol
            SendWindowSize, ReceiveWindowSize,//wnd size 
            Timeout, MaxRetransmit);//resnd


        client = new KcpClient(
            () => OnClientConnected?.Invoke(),
            (msg, channel) => OnClientDataReceived?.Invoke(msg, FromKcpChannelToInt(channel)),
            () => OnClientDisconnected?.Invoke(),
            (error, reason) => OnClientError?.Invoke(ToTransportError(error), reason),
            config);

        server = new KcpServer(
            (connID, address) => OnServerConnectedWithAddress?.Invoke(connID, PrettyAddress(address)),
            (connID, msg, channel) => OnServerDataReceived?.Invoke(connID, msg, FromKcpChannelToInt(channel)),
            (connID) => OnServerDisconnected.Invoke(connID),
            (connID, error, reason) => OnServerError.Invoke(connID, ToTransportError(error), reason),
            config);
    }
    public static int FromKcpChannelToInt(KcpChannel channel)
    {
        if (channel == KcpChannel.Reliable)
            return RELIABLE_CHANNEL;
        else
            return UNRELIABLE_CHANNEL;
    }
    public static KcpChannel FromIntToKcpChannel(int channel)
    {
        if (channel == RELIABLE_CHANNEL)
            return KcpChannel.Reliable;
        else
            return KcpChannel.Unreliable;
    }
    public override int GetMaxPacketSize(int channelID = (int)Channels.Reliable)
    {
        switch (channelID)
        {
            case (int)Channels.Unreliable:
                return KcpPeer.UnreliableMaxMessageSize(config.Mtu);
            default:
                return KcpPeer.ReliableMaxMessageSize(config.Mtu, ReceiveWindowSize);
        }
    }
    public override int GetBatchCapacity(int channelID = (int)Channels.Reliable)
        => KcpPeer.UnreliableMaxMessageSize(config.Mtu);

    #region client
    public override bool ClientConnected()
        => client.connected;
    public override void ClientConnect(string address)
        => client.Connect(address, Port);
    public override void ClientSend(ArraySegment<byte> segment, int channelID)
    {
        client.RawSend(segment, FromIntToKcpChannel(channelID));
        OnClientDataSent?.Invoke(segment, channelID);
    }
    public override void ClientDisconnect() 
        => client.Disconnect();
    public override void ClientEarlyUpdate()
    {
        if (enabled) 
            client.TickIncoming();
    }
    public override void ClientLateUpdate()
        => client.TickOutgoing();
    #endregion

    #region server
    public override bool ServerActive() 
        => server.IsActive();
    public override void ServerStart() 
        => server.StartServer(Port);
    public override string ServerGetClientAddress(int connectionID)
    {
        IPEndPoint endPoint = server.GetClientEndPoint(connectionID);
        return PrettyAddress(endPoint);
    }
    public override void ServerSend(int connectionID, ArraySegment<byte> segment, int channelID)
    {
        server.RawSend(connectionID, segment, FromIntToKcpChannel(channelID));
        OnServerDataSent?.Invoke(connectionID, segment, channelID);
    }
    public override void ServerDisconnect(int connectionID) 
        => server.Disconnect(connectionID);

    public override void ServerEarlyUpdate()
    {
        if (enabled) 
            server.TickIncoming();
    }
    // process outgoing in late update
    public override void ServerLateUpdate() 
        => server.TickOutgoing();
    public override void ServerStop()
        => server.Stop();

    #endregion
    public static TransportError ToTransportError(ErrorCode error)
    {
        switch (error)
        {
            case ErrorCode.DnsResolve: return TransportError.DnsResolve;
            case ErrorCode.Timeout: return TransportError.Timeout;
            case ErrorCode.Congestion: return TransportError.Congestion;
            case ErrorCode.InvalidReceive: return TransportError.InvalidReceive;
            case ErrorCode.InvalidSend: return TransportError.InvalidSend;
            case ErrorCode.ConnectionClosed: return TransportError.ConnectionClosed;
            case ErrorCode.Unexpected: return TransportError.Unexpected;
            default: 
                throw new InvalidCastException($"KCP: missing error translation for {error}");
        }
    }
    private static string PrettyAddress(IPEndPoint endPoint)
    {
        if (endPoint.Address.IsIPv4MappedToIPv6)
        {
            return endPoint.Address.MapToIPv4().ToString();
        }
        else
        {
            return endPoint.Address.ToString();
        }

    }
}
