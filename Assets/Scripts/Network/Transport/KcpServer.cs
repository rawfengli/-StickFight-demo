using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using static KcpPeer;

public class KcpServer
{
    private readonly Action<int, IPEndPoint> OnConnected; 
    private readonly Action<int, ArraySegment<byte>, KcpChannel> OnData;
    private readonly Action<int> OnDisconnected;
    private readonly Action<int, ErrorCode, string> OnError;

    private readonly KcpConfig config;
    private EndPoint newClientEP;

    private Socket socket;
    private readonly byte[] rawReceiveBuffer;
    public Dictionary<int, KcpServerConnection> connections = new();
    readonly HashSet<int> connectionsToRemove = new();

    public IPEndPoint GetClientEndPoint(int connectionID)
    {
        if (connections.TryGetValue(connectionID, out KcpServerConnection connection))
        {
            return connection.remoteEndPoint as IPEndPoint;
        }
        return null;
    }
    public bool IsActive()
        => socket != null;

    public KcpServer(Action<int, IPEndPoint> OnConnected,
                         Action<int, ArraySegment<byte>, KcpChannel> OnData,
                         Action<int> OnDisconnected,
                         Action<int, ErrorCode, string> OnError,
                         KcpConfig config)
    {
        this.OnConnected = OnConnected;
        this.OnData = OnData;
        this.OnDisconnected = OnDisconnected;
        this.OnError = OnError;
        this.config = config;

        rawReceiveBuffer = new byte[config.Mtu];
        newClientEP = new IPEndPoint(IPAddress.IPv6Any, 0);
    }
    public void Disconnect(int connectionID)
    {
        if (connections.TryGetValue(connectionID, out KcpServerConnection connection))
        {
            connection.Disconnect();
        }
    }
    private Socket CreateServerSocket(ushort port)
    {
        Socket socket = new Socket(AddressFamily.InterNetworkV6, SocketType.Dgram, ProtocolType.Udp);
        try
        {
            socket.DualMode = true;
        }
        catch (NotSupportedException e)
        {
            UnityEngine.Debug.LogWarning($"[KCP] Failed to set Dual Mode, continuing with IPv6 without Dual Mode. Error: {e}");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            const uint IOC_IN = 0x80000000U;
            const uint IOC_VENDOR = 0x18000000U;
            const int SIO_UDP_CONNRESET = unchecked((int)(IOC_IN | IOC_VENDOR | 12));
            socket.IOControl(SIO_UDP_CONNRESET, new byte[] { 0x00 }, null);
        }
        socket.Bind(new IPEndPoint(IPAddress.IPv6Any, port));
        return socket;
    }
    private KcpServerConnection CreateConnection(int connectionID)
    {
        if (socket == null)
        {
            UnityEngine.Debug.LogWarning("Server not Start");
            return null;
        }
        uint cookie = (uint)RandomNumberGenerator.GetInt32(1, int.MaxValue);

        KcpServerConnection connection = new KcpServerConnection(
            OnSingleConnectedCallback,
            (message, channel) => OnData(connectionID, message, channel),
            OnSingleDisconnectedCallback,
            (error, reason) => OnError(connectionID, error, reason),
            config, cookie, newClientEP, socket);

        return connection;
        void OnSingleConnectedCallback(KcpServerConnection conn)
        {
            connections.Add(connectionID, conn);
            IPEndPoint endPoint = conn.remoteEndPoint as IPEndPoint;
            OnConnected(connectionID, endPoint);
        }

        void OnSingleDisconnectedCallback()
        {
            connectionsToRemove.Add(connectionID);
            OnDisconnected(connectionID);
        }
    }
    public void StartServer(ushort port)
    {
        if (socket != null)
            return;

        socket = CreateServerSocket(port);
        socket.Blocking = false;
        Utils.ResizeSocketBuffer(socket, config.RecvBufferSize, config.SendBufferSize);
    }
    public void Stop()
    {
        connections.Clear();
        socket?.Close();
        socket = null;
    }
    public void SocketSendTo(int connectionID, ArraySegment<byte> data)
    {
        if (!connections.TryGetValue(connectionID, out KcpServerConnection connection))
        {
            UnityEngine.Debug.LogWarning(
                $"[KCP] Server: connectionId={connectionID} Connection not exist");
            return;
        }

        connection.SocketSend(data);
    }
    public bool SocketReceiveFrom(out ArraySegment<byte> segment, out int connectionID)
    {
        segment = default;
        connectionID = -1;
        if (socket == null) 
            return false;

        try
        {
            if (socket.ReceiveFromNonBlocking(rawReceiveBuffer, out segment, ref newClientEP))
            {
                connectionID = newClientEP.GetHashCode();
                return true;
            }
        }
        catch (SocketException e)
        {
            UnityEngine.Debug.Log($"[KCP] Server: ReceiveFrom failed: {e}");
        }

        return false;
    }

    public void RawSend(int connectionID, ArraySegment<byte> segment, KcpChannel channel)
    {
        if (connections.TryGetValue(connectionID, out KcpServerConnection connection))
        {
            connection.RawSend(segment, channel);
        }
        else
        {
            UnityEngine.Debug.Log($"Failed to send data, unknown connection {connectionID}");
        }
    }
    private void ProcessReceivedMessage(ArraySegment<byte> segment, int connectionID)
    {
        if (!connections.TryGetValue(connectionID, out KcpServerConnection connection))
        {
            connection = CreateConnection(connectionID);

            connection.RawInput(segment);
            connection.TickIncoming();

        }
        else
        {
            connection.RawInput(segment);
        }
    }
    public void TickIncoming()
    {
        while (SocketReceiveFrom(out ArraySegment<byte> segment, out int connectionID))
        {
            ProcessReceivedMessage(segment, connectionID);
        }

        foreach (KcpServerConnection connection in connections.Values)
        {
            connection.TickIncoming();
        }

        foreach (int connectionId in connectionsToRemove)
        {
            connections.Remove(connectionId);
        }
        connectionsToRemove.Clear();
    }
    public virtual void TickOutgoing()
    {
        // socket should not be null
        foreach (KcpServerConnection connection in connections.Values)
        {
            connection.TickOutgoing();
        }
    }
}
