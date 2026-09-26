using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class KcpConfig 
{
    //for choose ip4/ip6
    //public bool dualMode;

    public int RecvBufferSize;
    public int SendBufferSize;

    public int Mtu;

    public bool noDelay;

    public uint interval;

    public int fastResend;

    public uint SendWindowSize;
    public uint ReceiveWindowSize;

    public int Timeout;

    public uint maxRetransmits;//xmit

    public KcpConfig(
        int RecvBufferSize = 1024 * 1024 * 7,
        int SendBufferSize = 1024 * 1024 * 7,
        int Mtu = KCP.MTU_DEF,
        bool NoDelay = true,
        uint Interval = 10,
        int FastResend = 0,
        uint SendWindowSize = KCP.WND_SND,
        uint ReceiveWindowSize = KCP.WND_RCV,
        int Timeout = KcpPeer.DEFAULT_TIMEOUT,
        uint MaxRetransmits = KCP.DEADLINK)
    {
        this.RecvBufferSize = RecvBufferSize;
        this.SendBufferSize = SendBufferSize;
        this.Mtu = Mtu;
        this.noDelay = NoDelay;
        this.interval = Interval;
        this.fastResend = FastResend;
        this.SendWindowSize = SendWindowSize;
        this.ReceiveWindowSize = ReceiveWindowSize;
        this.Timeout = Timeout;
        this.maxRetransmits = MaxRetransmits;
    }

}
