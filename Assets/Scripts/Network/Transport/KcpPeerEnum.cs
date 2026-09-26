using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract partial class KcpPeer
{
    protected enum KcpState
    {
        Connecting = 0,
        Running = 1,
        Disconnected = 2,
    }
    public enum ErrorCode : byte
    {
        DnsResolve,
        Congestion,
        Timeout,
        InvalidReceive,
        InvalidSend,
        ConnectionClosed, 
        Unexpected
    }
    public enum KcpChannel : byte
    {
        Reliable = 1,
        Unreliable = 2
    }
    protected enum KcpHeaderReliable :byte
    {
        Hello = 1,
        Ping = 2,
        Data = 3,
    }
    protected enum KcpHeaderUnreliable :byte
    {
        Data = 4,
        Disconnect = 5,
    }
    protected static class KcpHeader
    {
        public static bool ParseReliable(byte value, out KcpHeaderReliable header)
        {
            if (Enum.IsDefined(typeof(KcpHeaderReliable), value))
            {
                header = (KcpHeaderReliable)value;
                return true;
            }

            header = KcpHeaderReliable.Ping; // any default
            return false;
        }
        public static bool ParseUnreliable(byte value, out KcpHeaderUnreliable header)
        {
            if (Enum.IsDefined(typeof(KcpHeaderUnreliable), value))
            {
                header = (KcpHeaderUnreliable)value;
                return true;
            }

            header = KcpHeaderUnreliable.Disconnect;
            return false;
        }
    }
}
