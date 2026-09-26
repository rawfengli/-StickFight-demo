using System;
using System.Net;
using System.Net.Sockets;

public class KcpClient : KcpPeer
{
    private Socket socket;
    private EndPoint remoteEndPoint;
    public EndPoint LocalEndPoint => socket?.LocalEndPoint;

    private readonly KcpConfig kcpConfig;

    //为什么不把send的回调放到client和server
    private event Action OnConnectedCallback;
    private event Action<ArraySegment<byte>, KcpChannel> OnDataCallback;
    private event Action OnDisconnectedCallback;
    private readonly Action<ErrorCode, string> OnErrorCallback;

    private readonly byte[] rawReceiveBuffer;
    public bool active { get; private set; }
    public bool connected { get; private set; }
    public KcpClient(
        Action OnConnectedCallback,
        Action<ArraySegment<byte>, KcpChannel> OnDataCallback,
        Action OnDisconnectedCallback,
        Action<ErrorCode, string> OnError,
        KcpConfig config)
        : base(config, 0)
    {
        connected = false;
        active = false;

        this.OnConnectedCallback = OnConnectedCallback;
        this.OnDataCallback = OnDataCallback;
        this.OnDisconnectedCallback = OnDisconnectedCallback;
        this.OnErrorCallback = OnError;

        kcpConfig = config;
        rawReceiveBuffer = new byte[config.Mtu];
    }

    public void Connect(string address, ushort port)
    {
        if (connected)
            return;

        if (Utils.HandleHostName(address, out IPAddress[] addresses) == false)
        {
            OnError(ErrorCode.DnsResolve, $"Failed to resolve host: {address}");
            OnDisconnectedCallback.Invoke();
            return;
        }

        Reset(kcpConfig);

        remoteEndPoint = new IPEndPoint(addresses[0], port);
        socket = new(remoteEndPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        active = true;

        socket.Blocking = false;

        Utils.ResizeSocketBuffer(socket, kcpConfig.RecvBufferSize, kcpConfig.SendBufferSize);

        socket.Connect(remoteEndPoint);

        SendHello();
    }
    protected override void OnConnected()
    {
        connected = true;
        OnConnectedCallback.Invoke();
    }
    protected override void OnData(ArraySegment<byte> message, KcpChannel channel)
    {
        OnDataCallback.Invoke(message, channel);
    }
    protected override void OnDisconnected()
    {
        connected = false;
        active = false;
        socket?.Close();
        socket = null;
        remoteEndPoint = null;
        OnDisconnectedCallback.Invoke();
    }
    protected override void OnError(ErrorCode error, string reason)
        => OnErrorCallback.Invoke(error, reason);
    public override void SocketSend(ArraySegment<byte> data)
    {
        if (socket == null)
            return;

        try
        {
            socket.SendNonBlocking(data);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.Log(e);
        }
    }
    public override bool SocketReceive(out ArraySegment<byte> segment)
    {
        segment = default;
        if (socket == null) return false;

        try
        {
            return socket.ReceiveNonBlocking(rawReceiveBuffer, out segment);
        }
        // for non-blocking sockets, Receive throws WouldBlock if there is
        // no message to read. that's okay. only log for other errors.
        catch (SocketException e)
        {
            UnityEngine.Debug.Log($"{e}happen, it is fine");
            base.Disconnect();
            return false;
        }
    }
    public override void RawSend(ArraySegment<byte> segment, KcpChannel channel)
    {
        if(connected == false)
        {
            UnityEngine.Debug.LogWarning("Send failed: Not connected");
            return;
        }
        SendData(segment, channel);
    }
    public override void RawInput(ArraySegment<byte> segment)
    {
        if (segment.Count <= METADATA_SIZE)
            return;

        byte channel = segment.Array[segment.Offset + 0];

        Utils.Decode32Bit(segment.Array, segment.Offset + 1, out uint _cookie);
        if (_cookie == 0)
        {
            UnityEngine.Debug.LogError("Cookie is zero");
        }

        if (cookie == 0)
            cookie = _cookie;
        else
        {
            if(cookie != _cookie)
            {
                UnityEngine.Debug.LogWarning("mismatching cookie, It may be due to receiving information from different links");
                return;
            }
        }
        ArraySegment<byte> message = new ArraySegment<byte>(
            segment.Array, 
            segment.Offset + METADATA_SIZE, 
            segment.Count - METADATA_SIZE);

        switch (channel)
        {
            case (byte)KcpChannel.Reliable:
            {
                OnRawInputReliable(message);
                break;
            }
            case (byte)KcpChannel.Unreliable:
            {
                OnRawInputUnreliable(message);
                break;
            }
            default:
            {
                UnityEngine.Debug.LogWarning("Incorrect Kcp channel type");
                break;
            }
        }
    }
    public override void TickIncoming()
    {
        if (active)
        {
            while (SocketReceive(out ArraySegment<byte> segment))
                RawInput(segment);
        }
        if (active) 
            base.TickIncoming();
    }
    public override void TickOutgoing()
    {
        if (active) 
            base.TickOutgoing();
    }
}
