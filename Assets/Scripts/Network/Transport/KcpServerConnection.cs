using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

public class KcpServerConnection : KcpPeer
{
    public readonly EndPoint remoteEndPoint;
    private readonly Socket socket;
    private readonly Action<KcpServerConnection> OnConnectedCallback;
    private readonly Action<ArraySegment<byte>, KcpChannel> OnDataCallback;
    private readonly Action OnDisconnectedCallback;
    private readonly Action<ErrorCode, string> OnErrorCallback;

    public KcpServerConnection(
        Action<KcpServerConnection>             OnConnected,
        Action<ArraySegment<byte>, KcpChannel>  OnData,
        Action                                  OnDisconnected,
        Action<ErrorCode, string>               OnError,

        KcpConfig config, uint cookie, EndPoint remoteEndPoint, Socket socket)
            : base(config, cookie)
    {
        OnConnectedCallback = OnConnected;
        OnDataCallback = OnData;
        OnDisconnectedCallback = OnDisconnected;
        OnErrorCallback = OnError;

        this.remoteEndPoint = remoteEndPoint;
        this.socket = socket;
    }
    protected override void OnConnected()
    {
        SendHello();
        OnConnectedCallback.Invoke(this);
    }
    protected override void OnData(ArraySegment<byte> message, KcpChannel channel)
        => OnDataCallback(message, channel);

    protected override void OnDisconnected()
        => OnDisconnectedCallback();

    protected override void OnError(ErrorCode error, string message)
        => OnErrorCallback(error, message);

    public override void RawInput(ArraySegment<byte> segment)
    {
        if (segment.Count <= METADATA_SIZE)
            return;

        byte channel = segment.Array[segment.Offset + 0];

        Utils.Decode32Bit(segment.Array, segment.Offset + 1, out uint _cookie);

        Utils.Decode8Bit(segment.Array, segment.Offset + METADATA_SIZE + KCP.OVERHEAD, out byte state);

        if (kcpState == KcpState.Running)
        {
            if (cookie != _cookie)
            {
                UnityEngine.Debug.LogWarning("mismatching cookie, It may be due to receiving information from different links or client's Hello message was transmitted multiple times");
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
    public override void RawSend(ArraySegment<byte> data, KcpChannel channel)
        => SendData(data, channel);

    public override bool SocketReceive(out ArraySegment<byte> data)
        => throw new NotSupportedException("SocketReceive should never happen in KcpServerConnection,it happen in Server");
    public override void SocketSend(ArraySegment<byte> data)
    {

        try
        {
            socket.SendToNonBlocking(data, remoteEndPoint);
        }
        catch (SocketException e)
        {
            UnityEngine.Debug.LogError($"Send Failure:{e}");
        }
    }
}
