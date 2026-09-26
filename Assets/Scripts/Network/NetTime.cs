using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Transactions;
using UnityEngine;

public static class NetTime 
{
    //时钟时间
    public static double localTime => Time.unscaledTimeAsDouble;
    public static float localTimeF => Time.unscaledTime;
    public static double time
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get 
        {
            if (NetServer.active)//server
                return localTime;
            else
                return NetClient.localTimeline;//client 时间快照
        }
    }
    //ping
    private const float DEFAULT_PING_INTERVAL = 0.1f;
    public static float PingInterval = DEFAULT_PING_INTERVAL;
    private static double lastPingTime;//this for client
    public const int PingWindowSize = 10; // average over 10 * 100ms : 1s
    //not transport rtt
    private static ExponentialMovingAverage _rtt = new (PingWindowSize);
    public static double rtt => _rtt.Value;
    public static double rttVariance => _rtt.Variance;

    //predictedTime

    //客户端预测服务端的时钟时间:注意因为客户端和服务端的时钟几乎不可能同步，这里预测的是服务端的时间戳
    //所以predictedTime只是用来对比快照的并没有所谓的预测回滚的作用
    public static double predictedTime
    {
        get
        {
            if(NetServer.active)
            {
                return localTime;
            }
            else
            {
                return localTime + predictedTimeError;
            }
        }
    }
    private static ExponentialMovingAverage _predictedTimeError = new(PingWindowSize);
    public static double predictedTimeError => _predictedTimeError.Value;
    public static double predictedTimeErrorActually;//预测时间与服务器之间的时间误差

    public static void ResetStatics()
    {
        PingInterval = DEFAULT_PING_INTERVAL;
        lastPingTime = 0;
        _rtt = new (PingWindowSize);
        _predictedTimeError = new (PingWindowSize);
        predictedTimeErrorActually = 0;
    }

    public static void UpdateClient()
    {
        if (localTime >= lastPingTime + PingInterval)
            ClientSendPing();
    }
    public static void UpdateServer(NetConnectionToClient conn)
    {
        if (localTime >= conn.lastPingTime + PingInterval)
            ServerSendPing(conn);
    }
    public static void ClientSendPing()
    {
        PingMessage pingMessage = new PingMessage()
        {
            localTime = NetTime.localTime,
            predictedTime = NetTime.predictedTime
        };
        NetClient.Send(pingMessage, (int)Channels.Unreliable);
        lastPingTime = localTime;
    }
    public static void ServerSendPing(NetConnectionToClient conn)
    {

        PingMessage pingMessage = new PingMessage()
        {
            localTime = NetTime.localTime,
            predictedTime = NetTime.localTime,//for server, local time is predictedTime
        };
        conn.Send(pingMessage, (int)Channels.Unreliable);
        conn.lastPingTime = NetTime.localTime;
    }


    //here is ping-pong interaction:client ping to server and then server pong to client
    //for client to cal rtt and cal predictedTime
    public static void OnServerPing(NetConnectionToClient conn, PingMessage message)
    {
        double rawError = localTime - message.localTime;//client to server time
        double predictedError = localTime - message.predictedTime;

        PongMessage pongMessage = new PongMessage()
        {
            localTime = message.localTime,//for cal rtt
            rawError = rawError,
            predictionError = predictedError,
        };
        conn.Send(pongMessage, (int)Channels.Unreliable);
    }
    public static void OnClientPong(PongMessage message)
    {
        if (message.localTime > localTime) 
            return;

        double new_rtt = localTime - message.localTime;
        _rtt.Add(new_rtt);

        _predictedTimeError.Add(message.rawError);//add raw error to cal new predictionError
        predictedTimeErrorActually = message.predictionError;
    }

    //here is ping-pong interaction:server ping to client and then client pong to server
    //for server to cal rtt 
    public static void OnClientPing(PingMessage message)
    {
        PongMessage pongMessage = new PongMessage()
        {
            localTime = message.localTime,//for cal rtt
            rawError = 0,
            predictionError = 0
        };
        NetClient.Send(pongMessage, (int)Channels.Unreliable);
    }
    public static void OnServerPong(NetConnectionToClient conn, PongMessage message)
    {
        if (message.localTime > localTime) 
            return;

        double newRtt = localTime - message.localTime;
        conn._rtt.Add(newRtt);
    }
}
