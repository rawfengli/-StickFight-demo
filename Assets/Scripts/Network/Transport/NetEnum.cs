using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TransportError : byte
{
    DnsResolve,
    Refused,
    Timeout,
    InvalidReceive,
    InvalidSend,
    ConnectionClosed,
    Unexpected,
    Congestion
}

public enum Channels
{
    Reliable = 0,
    Unreliable = 1
}
