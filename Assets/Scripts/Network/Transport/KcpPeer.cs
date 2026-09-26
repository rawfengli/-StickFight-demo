using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Unity.VisualScripting;
using Unity.VisualScripting.FullSerializer;
using UnityEngine;

// kcp.send -> update(kcp.flush) -> input -> receive

public abstract partial class KcpPeer
{
    public const int DEFAULT_TIMEOUT = 10000;
    protected const int CONGESTION_LIMIT = 3000;
    protected const int PING_INTERVAL = 1000;

    public const int CHANNEL_HEADER_SIZE = 1;
    public const int COOKIE_HEADER_SIZE = 4;
    public const int KCP_MESSAGE_STATUS_HEADER_SIZE = 1;//it is  KcpHeaderReliable or KcpHeaderUnreliable header
    public const int METADATA_SIZE = CHANNEL_HEADER_SIZE + COOKIE_HEADER_SIZE;

    protected KCP kcp;
    protected uint cookie;

    protected readonly Stopwatch stopWatch = new Stopwatch();
    protected KcpState kcpState;
    public int timeout;
    protected uint lastReceiveTime;

    protected uint lastPingTime;

    protected readonly byte[] kcpMessageBuffer;
    protected readonly byte[] kcpSendBuffer;

    protected readonly byte[] rawSendBuffer;//socket send buffer

    public readonly int unreliableMax;
    public readonly int reliableMax;

    // -> Send() 会检查分片数是否 < rcv_wnd，因此我们使用 rcv_wnd - 1。
    static int ReliableMaxMessageSize_Unconstrained(int mtu, uint rcv_wnd)
        => (mtu - KCP.OVERHEAD - METADATA_SIZE) * ((int)rcv_wnd - 1) - KCP_MESSAGE_STATUS_HEADER_SIZE;
    public static int ReliableMaxMessageSize(int mtu, uint rcv_wnd) 
        => ReliableMaxMessageSize_Unconstrained(mtu, Math.Min(rcv_wnd, KCP.FRG_MAX));
    public static int UnreliableMaxMessageSize(int mtu)
        => mtu - METADATA_SIZE - KCP_MESSAGE_STATUS_HEADER_SIZE;

    protected KcpPeer(KcpConfig config, uint cookie)
    {
        Reset(config);
        this.cookie = cookie;

        unreliableMax = UnreliableMaxMessageSize(config.Mtu);
        reliableMax = ReliableMaxMessageSize(config.Mtu, config.ReceiveWindowSize);

        rawSendBuffer = new byte[config.Mtu];

        kcpMessageBuffer = new byte[reliableMax + 1];
        kcpSendBuffer    = new byte[reliableMax + 1];
    }
    protected void Reset(KcpConfig config)
    {
        cookie = 0;
        kcpState = KcpState.Connecting;
        lastReceiveTime = 0;
        lastPingTime = 0;
        stopWatch.Restart();

        kcp = new KCP(0, RawSendReliable);//conv = 0?
        uint nodelay = (config.noDelay ? 1u : 0u);

        kcp.SetNoDelay(nodelay, config.interval, config.fastResend);
        kcp.SetWndSize(config.SendWindowSize, config.ReceiveWindowSize);
        kcp.SetMtu((uint)config.Mtu - METADATA_SIZE);

        kcp.SetDeadLink(config.maxRetransmits);
        timeout = config.Timeout;
    }
    protected abstract void OnConnected();
    //protected abstract void OnData(ArraySegment<byte> message, KcpChannel channel);
    protected abstract void OnDisconnected();
    protected abstract void OnData(ArraySegment<byte> message, KcpChannel channel);
    protected abstract void OnError(ErrorCode error, string message);
    public abstract void SocketSend(ArraySegment<byte> data);
    public abstract bool SocketReceive(out ArraySegment<byte> data);
    public abstract void RawSend(ArraySegment<byte> data, KcpChannel channel);
    public abstract void RawInput(ArraySegment<byte> data);
    protected void HandleTimeOut(uint time)
    {
        if (time >= lastReceiveTime + timeout)
        {
            // pass error to user callback. no need to log it manually.
            // GetType() shows Server/ClientConn instead of just Connection.
            OnError(ErrorCode.Timeout, $"{GetType()}: Connection timed out after not receiving any message for {timeout}ms. Disconnecting.");
            Disconnect();
        }
    }
    protected void HandleDeadLink()
    {
        if (kcp.linkState == -1)
        {
            // pass error to user callback. no need to log it manually.
            // GetType() shows Server/ClientConn instead of just Connection.
            OnError(ErrorCode.Timeout, $"{GetType()}: a message was retransmitted many times without ack. Disconnecting.");
            Disconnect();
        }
    }
    protected void HandlePing(uint time)
    {
        if (time >= lastPingTime + PING_INTERVAL)
        {
            // ping again and reset time
            //Log.Debug("[KCP] sending ping...");
            SendPing();
            lastPingTime = time;
        }
    }
    protected void HandleChoke()
    {
        int total = kcp.TotalQueueDataCount();
        if (total >= CONGESTION_LIMIT)
        {
            // pass error to user callback. no need to log it manually.
            // GetType() shows Server/ClientConn instead of just Connection.
            OnError(ErrorCode.Congestion, "connection can't process data fast enough.\n" +
                $"Network congestion upper limit is {CONGESTION_LIMIT}");

            Disconnect();
        }
    }
    #region Update Receive And Send
    protected bool ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message)
    {
        message = default;
        header = KcpHeaderReliable.Ping;

        int msgSize = kcp.PeekSize();
        //如果kcp的queue中不是一个完整的消息，则直接退出
        //kcpMessageBuffer的第一个永远是kcp header
        if (msgSize <= 0) return false;

        if (msgSize > kcpMessageBuffer.Length)
        {
            OnError(ErrorCode.InvalidReceive,
                $"{GetType()}: msgSize {msgSize} > buffer {kcpMessageBuffer.Length}. Disconnecting the connection.");
            Disconnect();
            return false;
        }
        int received = kcp.Receive(kcpMessageBuffer, msgSize);
        if (received < 0)
        {
            OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive failed with error={received}. closing connection.");
            Disconnect();
            return false;
        }

        byte headerByte = kcpMessageBuffer[0];
        if (!KcpHeader.ParseReliable(headerByte, out header))
        {
            OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive failed to parse header: {headerByte} is not defined in {typeof(KcpHeaderReliable)}.");
            Disconnect();
            return false;
        }

        // extract content without header
        message = new ArraySegment<byte>(kcpMessageBuffer, 1, msgSize - 1);
        lastReceiveTime = (uint)stopWatch.ElapsedMilliseconds;
        return true;
    }
    protected void TickIncoming_Connecting(uint time)
    {
        HandleTimeOut(time);
        HandleDeadLink();
        HandleChoke();

        if (ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message))
        {
            switch (header)
            {

                case KcpHeaderReliable.Hello:
                {
                    kcpState = KcpState.Running;
                    OnConnected();
                    break;
                }
                case KcpHeaderReliable.Ping:
                {
                    // ping keeps kcp from timing out. do nothing.
                    break;
                }
                case KcpHeaderReliable.Data:
                {
                    OnError(ErrorCode.InvalidReceive, $"[KCP] {GetType()}: received invalid header {header} while Connected. Disconnecting the connection.");
                    Disconnect();
                    break;
                }
            }
        }
    }
    protected void TickIncoming_Running(uint time)
    {
        HandleTimeOut(time);
        HandleDeadLink();
        HandlePing(time);
        HandleChoke();

        while (ReceiveNextReliable(out KcpHeaderReliable header, out ArraySegment<byte> message))
        {
            switch (header)
            {
                case KcpHeaderReliable.Hello:
                {
                    // should never receive another hello after auth
                    Disconnect();
                    break;
                }

                case KcpHeaderReliable.Ping:
                {
                    // do nothing.
                    break;
                }

                case KcpHeaderReliable.Data:
                {
                    // call OnData IF the message contained actual data
                    if (message.Count > 0)
                    {
                        OnData(message, KcpChannel.Reliable);
                    }
                    // empty data = attacker, or something went wrong
                    else
                    {
                        // pass error to user callback. no need to log it manually.
                        // GetType() shows Server/ClientConn instead of just Connection.
                        OnError(ErrorCode.InvalidReceive, $"{GetType()}: received empty Data message while Authenticated. Disconnecting the connection.");
                        Disconnect();
                    }
                    break;
                }
            }
        }
    }
    public virtual void TickIncoming()
    {
        uint time = (uint)stopWatch.ElapsedMilliseconds;
        try
        {
            switch (kcpState)
            {
                case KcpState.Connecting:
                {
                    TickIncoming_Connecting(time);
                    break;
                }
                case KcpState.Running:
                {
                    TickIncoming_Running(time);
                    break;
                }
                case KcpState.Disconnected:
                {
                    // do nothing while disconnected
                    break;
                }
            }
        }
        catch (Exception exception)
        {
            OnError(ErrorCode.Unexpected, $"{GetType()}: unexpected Exception: {exception}");
            Disconnect();
        }
    }
    public virtual void TickOutgoing()
    {
        uint time = (uint)stopWatch.ElapsedMilliseconds;
        try
        {
            switch (kcpState)
            {
                case KcpState.Connecting://here is no break,because we need to send hello mess
                case KcpState.Running:
                    {
                        kcp.Update(time);
                        break;
                    }
                case KcpState.Disconnected:
                    {
                        // do nothing while disconnected
                        break;
                    }
            }
        }
        catch(Exception exception)
        {
            OnError(ErrorCode.Unexpected, $"{GetType()}: unexpected exception: {exception}");
            Disconnect();
        }
    }

    // Reliable message:
    //  KcpChannel cookie [kcp_overheader kcp_header/ChanneContent data_message]
    //  []内会出现分片情况
    // Unreliable message
    //  KcpChannel cookie kcp_header/ChanneContent data_message 
    //send
    protected void RawSendReliable(byte[] data, int length)
    {
        rawSendBuffer[0] = (byte)KcpChannel.Reliable;

        Utils.Encode32Bit(rawSendBuffer, 1, cookie);
        //Buffer.BlockCopy(data, 0, rawSendBuffer, 1 + 4, length);
        int headerSize = CHANNEL_HEADER_SIZE + COOKIE_HEADER_SIZE;
        Buffer.BlockCopy(data, 0, rawSendBuffer, headerSize, length);

        ArraySegment<byte> segment = new ArraySegment<byte>(rawSendBuffer, 0, length + headerSize);
        SocketSend(segment);
    }
    void SendUnreliable(KcpHeaderUnreliable header, ArraySegment<byte> content)
    {
        if (content.Count > unreliableMax)
        {
            return;
        }

        rawSendBuffer[0] = (byte)KcpChannel.Unreliable;

        Utils.Encode32Bit(rawSendBuffer, 1, cookie); 

        rawSendBuffer[5] = (byte)header;

        int headerSize = CHANNEL_HEADER_SIZE + COOKIE_HEADER_SIZE + KCP_MESSAGE_STATUS_HEADER_SIZE;

        if (content.Count > 0)
            Buffer.BlockCopy(content.Array, content.Offset, rawSendBuffer, headerSize, content.Count);

        ArraySegment<byte> segment = new ArraySegment<byte>(rawSendBuffer, 0, content.Count + headerSize);
        SocketSend(segment);
    }
    protected void SendReliable(KcpHeaderReliable header, ArraySegment<byte> content)
    {
        if (content.Count + 1 > kcpSendBuffer.Length)
        {
            OnError(ErrorCode.InvalidSend, $"{GetType()}: " +
                $"Failed to send reliable message of size {content.Count} because it's larger than ReliableMaxMessageSize={reliableMax}");
            return;
        }
        kcpSendBuffer[0] = (byte)header;
        if(content.Count > 0)
            Buffer.BlockCopy(content.Array, content.Offset, kcpSendBuffer, 1, content.Count);

        int sent = kcp.Send(kcpSendBuffer, 0, 1 + content.Count);
        if (sent < 0)
        {
            OnError(ErrorCode.InvalidSend, $"{GetType()}: " +
                $"Send failed with error={sent} for content with length={content.Count}");
        }
    }
    public void SendHello()
        => SendReliable(KcpHeaderReliable.Hello, default);
    public void SendData(ArraySegment<byte> data, KcpChannel channel)
    {
        if (data.Count == 0)
        {
            OnError(ErrorCode.InvalidSend, 
                $"{GetType()}: tried sending empty message. Disconnecting.");
            Disconnect();
            return;
        }
        switch (channel)
        {
            case KcpChannel.Reliable:
                SendReliable(KcpHeaderReliable.Data, data);
                break;
            case KcpChannel.Unreliable:
                SendUnreliable(KcpHeaderUnreliable.Data, data);
                break;
        }
    }
    protected void SendDisconnect()
    {
        for (int i = 0; i < 5; ++i)
            SendUnreliable(KcpHeaderUnreliable.Disconnect, default);
    }
    protected void SendPing() 
        => SendReliable(KcpHeaderReliable.Ping, default);
    public void Disconnect()
    {
        if (kcpState == KcpState.Disconnected)
            return;

        try
        {
            SendDisconnect();
        }
        // TODO KcpConnection is IO agnostic. move this to outside later.
        catch (Exception e)
        {
            UnityEngine.Debug.Log($"{e} happen on disconnecting");
        }

        kcpState = KcpState.Disconnected;
        OnDisconnected();
    }

    //---------------------------------------------------------------------------------------
    //input / receive
    protected void OnRawInputReliable(ArraySegment<byte> message)
    {
        // input into kcp, but skip channel byte
        int input = kcp.Input(message.Array, message.Offset, message.Count);
        if (input != 0)
        {
            UnityEngine.Debug.LogError(
                $"[KCP] {GetType()}: Input failed with error={input} for buffer with length={message.Count - 1}");    
        }
    }

    protected void OnRawInputUnreliable(ArraySegment<byte> message)
    {
        // need at least one byte for the KcpHeader enum
        if (message.Count < 1) return;

        // safely extract header. attackers may send values out of enum range.
        byte headerByte = message.Array[message.Offset + 0];
        if (!KcpHeader.ParseUnreliable(headerByte, out KcpHeaderUnreliable header))
        {
            OnError(ErrorCode.InvalidReceive, $"{GetType()}: Receive failed to parse header: {headerByte} is not defined in {typeof(KcpHeaderUnreliable)}.");
            Disconnect();
            return;
        }

        message = new ArraySegment<byte>(message.Array, message.Offset + 1, message.Count - 1);

        switch (header)
        {
            case KcpHeaderUnreliable.Data:
            {

                if (kcpState == KcpState.Running)
                {
                    OnData(message, KcpChannel.Unreliable);
                    lastReceiveTime = (uint)stopWatch.ElapsedMilliseconds;
                }
                break;
            }
            case KcpHeaderUnreliable.Disconnect:
            {
                Disconnect();
                break;
            }
        }
    }
    #endregion
}
