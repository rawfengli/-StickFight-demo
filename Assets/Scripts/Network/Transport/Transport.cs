using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Transport : MonoBehaviour
{
    public static Transport instance;
    public abstract ushort Port { get; set; }

    public Action                             OnClientConnected;
    public Action<ArraySegment<byte>, int>    OnClientDataReceived;
    public Action<ArraySegment<byte>, int>    OnClientDataSent;
    public Action                             OnClientDisconnected;
    public Action<TransportError, string>     OnClientError;
    public Action<Exception>                  OnClientTransportException;


    public Action<int, string>                  OnServerConnectedWithAddress;
    public Action<int, ArraySegment<byte>, int> OnServerDataReceived;
    public Action<int, ArraySegment<byte>, int> OnServerDataSent;
    public Action<int>                          OnServerDisconnected;
    public Action<int, TransportError, string>  OnServerError;
    public Action<int, Exception>               OnServerTransportException;

    #region Client
    public virtual void ClientEarlyUpdate() { }
    public virtual void ClientLateUpdate() { }
    public abstract bool ClientConnected();
    public abstract void ClientConnect(string address);
    public abstract void ClientSend(ArraySegment<byte> segment, int channelID = (int)Channels.Reliable);
    public abstract void ClientDisconnect();

    #endregion

    #region Server
    public virtual void ServerEarlyUpdate() { }
    public virtual void ServerLateUpdate() { }

    public abstract bool ServerActive();
    public abstract void ServerStart();
    public abstract void ServerSend(int connectionID, ArraySegment<byte> segment, int channelID = (int)Channels.Reliable);
    public abstract void ServerDisconnect(int connectionID);
    public abstract string ServerGetClientAddress(int connectionID);
    public abstract void ServerStop();
    #endregion

    public abstract int GetMaxPacketSize(int channelID = (int)Channels.Reliable);
    public virtual int GetBatchCapacity(int channelID = (int)Channels.Reliable)
        => GetMaxPacketSize(channelID);
}
