//Kcp base on mirror's kcp,
//Referring to blog lecture notes on https://luyuhuang.tech/2020/12/09/kcp.html
// Time.diff
using System;
using System.Collections.Generic;

public partial class KCP
{
    public const int RTO_NDL = 30;             // no delay min rto
    public const int RTO_MIN = 100;            // min rto
    public const int RTO_DEF = 200;            // default RTO
    public const int RTO_MAX = 60000;          // maximum RTO
    public const int CMD_PUSH = 81;            // cmd: data
    public const int CMD_ACK = 82;             // cmd: ack
    public const int CMD_WASK = 83;            // cmd: 发送窗口探测
    public const int CMD_WINS = 84;            // cmd: 回复窗口探测
    public const int ASK_SEND = 1;             // 需要发送窗口探测
    public const int ASK_TELL = 2;             // 需要回复窗口探测
    public const int WND_SND = 32;             // default send window
    public const int WND_RCV = 128;            // default receive window. must be >= max fragment size
    public const int MTU_DEF = 1200;           // default MTU

    public const int ACK_FAST = 3;
    public const int INTERVAL = 100;
    public const int OVERHEAD = 24;
    public const int FRG_MAX = byte.MaxValue;  // kcp encodes 'frg' as byte. so we can only ever send up to 255 fragments.
    public const int DEADLINK = 20;            // default maximum amount of 'xmit' retransmissions until a segment is considered lost
    public const int THRESHOLD_INIT = 2;
    public const int THRESHOLD_MIN = 2;

    public const int PROBE_INIT = 7000;        // 7 secs to probe window size
    public const int PROBE_LIMIT = 120000;     // up to 120 secs to probe window
    public const int FASTACK_LIMIT = 5;        // max times to trigger fastack

    public int linkState { get; private set; }  // link state, -1: disconnected, 1: connected
    uint conv;                                  // conversation;链接编号
    uint mtu;                                   // Maximum Transmission Unit;一个完整的包的最大大小
    uint mss;                                   // Maximum Segment Size,mss = MTU - OVERHEAD;最大数据段大小，排除报头

    //una Unacknowledged
    uint snd_una;       //发送的ack_una， snd_una = 5 说明前0-4都被发出且且被确认了，5之后的部分已经发出但未确认
    uint snd_nxt;       //下一个要插入到send buffer的index
    uint rcv_nxt;       //下一个要插入到receive buffer的index

    //重传和拥塞控制
    uint ss_threshold;      // slow start threshold 慢启动阈值
    int rx_rttval;      // average deviation of rtt, used to measure the jitter of rtt,rtt 的平均偏差，用于测量rtt的抖动
    int rx_srtt;        // smoothed round trip time (a weighted average of rtt),类平均估计的rtt
    int rx_rto;         // retransmission timeout 重传超时时间
    int rx_minrto;

    uint snd_wnd_cap;   // send window 发送窗口大小
    uint rcv_wnd_cap;   // receive window 接受窗口大小
    uint rmt_wnd_rem;   // remote window 远程窗口 对端剩余接收窗口的大小.
    uint cwnd;          // congestion window 拥塞窗口

    uint probe;         // 发送控制报文的标志
    uint interval;      // flush interval 刷新间隔
    uint ts_flush;      // flush timestamp 刷新时间戳
    uint xmit;          // 本次会话超时重传次数
    uint nodelay;       // RTO增长模式 not a bool. original Kcp has '<2 else' check.
    bool updated;       // update 是否被调用过
    uint ts_probe;      // 发送控制报文的时间戳
    uint probe_wait;    // 发送控制报文的时间Interval, 用于计算ts_probe
    uint dead_link;     // 存在一个报文被重传超过dead_link次，则认为连接断开
    uint incr;          // 用于计算 cwnd
    uint current;       // current time

    int fastresend;
    int fastlimit;
    //bool nocwnd;        // congestion control, negated. heavily restricts send/recv window sizes.

    Queue<Segment> snd_queue = new Queue<Segment>(16); // send queue
    Queue<Segment> rcv_queue = new Queue<Segment>(16); // receive queue.
    List<Segment> snd_buf = new List<Segment>(16);   // send buffer
    List<Segment> rcv_buf = new List<Segment>(16);   // receive buffer
                                                     //ack
    readonly List<AckMess> acklist = new(16);
    byte[] buffer;
    Action<byte[], int> output;
    public KCP(uint conv, Action<byte[], int> output)
    {
        this.conv = conv;
        this.output = output;

        snd_wnd_cap = WND_SND;
        rcv_wnd_cap = WND_RCV;
        rmt_wnd_rem = WND_RCV;
        mtu = MTU_DEF;
        mss = mtu - OVERHEAD;
        rx_rto = RTO_DEF;
        rx_minrto = RTO_MIN;
        interval = INTERVAL;
        ts_flush = INTERVAL;
        ss_threshold = THRESHOLD_INIT;
        fastlimit = FASTACK_LIMIT;
        dead_link = DEADLINK;
        buffer = new byte[(mtu + OVERHEAD) * 3];
    }
    public int TotalQueueDataCount()
        => rcv_queue.Count + snd_queue.Count + rcv_buf.Count + snd_buf.Count;
    uint WndUnused()
    {
        if (rcv_queue.Count < rcv_wnd_cap)
            return rcv_wnd_cap - (uint)rcv_queue.Count;
        return 0;
    }
    void OutputIfBufferWillFull(ref int size, int space)
    {
        if (size + space > mtu)
        {
            output(buffer, size);
            size = 0;
        }
    }
    #region Send
    public void Update(uint currentTimeMilliSeconds)
    {
        current = currentTimeMilliSeconds;

        if (!updated)
        {
            updated = true;
            ts_flush = current;
        }

        int slap = (int)current - (int)ts_flush;
        if (slap >= 10000 || slap < -10000)
        {
            ts_flush = current;
            slap = 0;
        }

        if (slap >= 0)
        {
            // increase last flush time by one interval
            ts_flush += interval;

            // if last flush is still behind, increase it to current + interval
            // if (Utils.TimeDiff(current, ts_flush) >= 0) // original kcp.c
            if (current >= ts_flush)                       // less confusing
            {
                ts_flush = current + interval;
            }
            Flush();
        }
    }
    public int Send(byte[] buffer, int offset, int len)
    {
        int count;
        if (len < 0) return -1;

        if (len <= mss) count = 1;//if len = 0, count also = 1
        else count = (int)((len + mss - 1) / mss);

        if (count > FRG_MAX)
            throw new Exception($"Send len={len} requires {count} fragments, but kcp can only handle up to {FRG_MAX} fragments.");

        if (count >= rcv_wnd_cap) 
            return -2;

        if (count == 0) 
            count = 1;

        for (int i = 0; i < count; i++)
        {
            int size = len > (int)mss ? (int)mss : len;
            Segment segment = Segment.New();
            if (len > 0)
            {
                segment.data.Write(buffer, offset, size);
            }
            segment.frg = (byte)(count - i - 1);
            snd_queue.Enqueue(segment);
            offset += size;
            len -= size;
        }
        return 0;
    }
    private void Flush()
    {
        int size = 0;
        bool lost = false;

        if(!updated) 
            return;

        Segment segment = Segment.New();
        segment.conv = conv;
        segment.cmd = CMD_ACK;
        segment.wnd = WndUnused();
        segment.una = rcv_nxt;
        
        foreach (AckMess ack in acklist)
        {
            OutputIfBufferWillFull(ref size, OVERHEAD);
            // ikcp_ack_get assigns ack[i] to seg.sn, seg.ts
            segment.sn = ack.sn;
            segment.ts = ack.ts;
            size += segment.Encode(buffer, size);
        }
        acklist.Clear();

        if (rmt_wnd_rem == 0)
        {
            if (probe_wait == 0)//first probe packet
            {
                probe_wait = PROBE_INIT;
                ts_probe = current + probe_wait;
            }
            else//more than one probe packet
            {
                if (Utils.TimeDiff(current, ts_probe) >= 0)
                {
                    //如果probe_wait小于PROBE_INIT，则初始化成PROBE_INIT
                    if (probe_wait < PROBE_INIT)
                        probe_wait = PROBE_INIT;

                    probe_wait += probe_wait / 2;//*1.5策略
                    //如果probe_wait大于PROBE_LIMIT，则限制到PROBE_LIMIT
                    if (probe_wait > PROBE_LIMIT)
                        probe_wait = PROBE_LIMIT;

                    ts_probe = current + probe_wait;//probe ts
                    probe |= ASK_SEND;
                }
            }
        }
        else
        {
            //reset probe_wait and ts_probe if remote window is not zero
            ts_probe = 0;
            probe_wait = 0;
        }

        if((probe & ASK_SEND) != 0)
        {
            segment.cmd = CMD_WASK;
            OutputIfBufferWillFull(ref size, OVERHEAD);
            size += segment.Encode(buffer, size);
        }

        if ((probe & ASK_TELL) != 0)
        {
            segment.cmd = CMD_WINS;
            OutputIfBufferWillFull(ref size, OVERHEAD);
            size += segment.Encode(buffer, size);
        }

        uint _cwnd;
        _cwnd = Math.Min(snd_wnd_cap, rmt_wnd_rem);
        _cwnd = Math.Min(cwnd, _cwnd);
        
        while (Utils.TimeDiff(snd_nxt, snd_una + _cwnd) < 0)
        {
            if (snd_queue.Count == 0) break;
            // send data
            Segment dataSegment = snd_queue.Dequeue();

            dataSegment.conv = conv;
            dataSegment.cmd = CMD_PUSH;
            dataSegment.wnd = segment.wnd;
            dataSegment.ts = current;
            dataSegment.sn = snd_nxt;
            dataSegment.una = rcv_nxt;

            dataSegment.resendts = current;
            dataSegment.rto = rx_rto;
            dataSegment.fastack = 0;
            dataSegment.xmit = 0;

            //add from queue to buffer
            snd_buf.Add(dataSegment);
            snd_nxt++;
        }

        uint resent = fastresend > 0 ? (uint)fastresend : 0xffffffff;
        uint rtomin = nodelay == 0 ? (uint)rx_rto >> 3 : 0;

        int change = 0;

        foreach (Segment segmentInBuffer in snd_buf)
        {
            bool needsend = false;

            if(segmentInBuffer.xmit == 0)//第一次发送 
            {
                needsend = true;
                segmentInBuffer.xmit++;
                segmentInBuffer.rto = rx_rto;
                segmentInBuffer.resendts = current + (uint)segmentInBuffer.rto + rtomin;
            }
            else if(Utils.TimeDiff(current, segmentInBuffer.resendts) >= 0)//重传
            {
                needsend = true;
                segmentInBuffer.xmit++;

                if (nodelay == 0)//no delay = 0
                {
                    segmentInBuffer.rto += Math.Max(segmentInBuffer.rto, rx_rto);//约为rto * 2
                }
                else//no delay = 1 or else
                {
                    int step = (nodelay < 2) ? segmentInBuffer.rto : rx_rto;// * 1.5
                    segmentInBuffer.rto += step / 2;
                }

                segmentInBuffer.resendts = current + (uint)segmentInBuffer.rto;
                lost = true;
                xmit++;

            }
            else if (segmentInBuffer.fastack >= resent)//fastresend，快速重传
            {
                if (segmentInBuffer.xmit <= fastlimit || fastlimit <= 0)
                {
                    needsend = true;
                    segmentInBuffer.xmit++;
                    segmentInBuffer.fastack = 0;
                    segmentInBuffer.resendts = current + (uint)segmentInBuffer.rto;

                    change++;
                }
            }

            if(needsend)
            {
                segmentInBuffer.ts = current;
                segmentInBuffer.wnd = segment.wnd;
                segmentInBuffer.una = rcv_nxt;

                int need = OVERHEAD + (int)segmentInBuffer.data.Position;
                OutputIfBufferWillFull(ref size, need);
                size += segmentInBuffer.Encode(buffer, size);

                if(segmentInBuffer.data.Position > 0)//data message, other cmd's data length is 0
                {
                    Buffer.BlockCopy(segmentInBuffer.data.GetBuffer(), 0, buffer, size, (int)segmentInBuffer.data.Position);
                    size += (int)segmentInBuffer.data.Position;
                }

                if (segmentInBuffer.xmit >= dead_link)
                    linkState = -1;
            }
        }

        Segment.Return(segment);

        FlushBuffer(size);

        //此处处理快恢复和丢包的cwnd
        //慢启动和拥塞控制在input处理
        if(change > 0)// fast retransmit then cal cwnd
        {
            uint inflight = snd_nxt - snd_una;
            ss_threshold = inflight / 2;
            if (ss_threshold < THRESHOLD_MIN)
                ss_threshold = THRESHOLD_MIN;

            cwnd = ss_threshold + resent;
            incr = cwnd * mss;
        }

        if (lost)
        {
            ss_threshold = _cwnd / 2;//值得注意的是，此处不是kcp的cwnd (?
            if (ss_threshold < THRESHOLD_MIN)
                ss_threshold = THRESHOLD_MIN;

            cwnd = 1;
            incr = mss;
        }

        if (cwnd < 1)
        {
            cwnd = 1;
            incr = mss;
        }
    }
    void FlushBuffer(int size)
    {
        if (size > 0)
        {
            output(buffer, size);
        }
    }
    #endregion

    #region Receive
    void AckPush(uint sn, uint ts) // serial number, timestamp
        => acklist.Add(new AckMess { sn = sn, ts = ts });

    void ParseData(Segment segment)
    {
        uint sn = segment.sn;
        // || sn < rcv_nxt || sn >= rcv_nxt + rcv_wnd_cap
        if (Utils.TimeDiff(sn, rcv_nxt + rcv_wnd_cap) >= 0 || 
            Utils.TimeDiff(sn, rcv_nxt) < 0)
        {
            Segment.Return(segment);
            return;
        }
        InsertSegmentInReceiveBuffer(segment);//将segment插入到接收缓冲区rcv_buf对应的位置中
        MoveReceiveBufferReadySegmentsToQueue();//按顺序将rcv_buf中连续的segment移动到rcv_queue中
    }
    void InsertSegmentInReceiveBuffer(Segment segment)
    {
        bool repeat = false;

        int i;
        for(i = rcv_buf.Count - 1; i >= 0; i--)
        {
            Segment seg = rcv_buf[i];
            if (seg.sn == segment.sn)
            {
                repeat = true;
                break;
            }
            if (Utils.TimeDiff(segment.sn, seg.sn) > 0)
            {//i是第一个比segment.sn小的segment的index
                break;
            }
        }

        if(!repeat)
        {
            rcv_buf.Insert(i + 1, segment);//i+1是segment的位置
        }
        else
        {
            Segment.Return(segment);
        }
    }
    void MoveReceiveBufferReadySegmentsToQueue()
    {
        int removed = 0;
        foreach (Segment seg in rcv_buf)
        {
            if (seg.sn == rcv_nxt && rcv_queue.Count < rcv_wnd_cap)
            {
                rcv_queue.Enqueue(seg);
                rcv_nxt++;
                removed++;
            }
            else
            {
                break;
            }
        }
        rcv_buf.RemoveRange(0, removed);
    }
    void ParseAck(uint sn)//收到ack后，删除发送缓冲区中对应的segment
    {
        /*
            if (sn < snd_una || sn >= snd_nxt)//not in the send queue(?
                return;
        */
        if (Utils.TimeDiff(sn, snd_una) < 0 || Utils.TimeDiff(sn, snd_nxt) >= 0)
            return;
        // for-int so we can erase while iterating
        for (int i = 0; i < snd_buf.Count; ++i)
        {
            // is this the segment?
            Segment seg = snd_buf[i];
            if (sn == seg.sn)
            {
                // remove and return
                snd_buf.RemoveAt(i);
                Segment.Return(seg);
                break;
            }
            if (Utils.TimeDiff(sn, seg.sn) < 0)
            {
                break;
            }
        }
    }
    void UpdateAck(int rtt)//current time - ts
    {//rtt : 从发出ack到收到ack的时间间隔
        if (rx_srtt == 0)
        {
            rx_srtt = rtt;
            rx_rttval = rtt / 2;
        }
        else
        {
            int delta = Math.Abs(rtt - rx_srtt);
            rx_rttval = (3 * rx_rttval + delta) / 4;
            rx_srtt = (7 * rx_srtt + rtt) / 8;
            if (rx_srtt < 1) rx_srtt = 1;
        }
        int rto = rx_srtt + Math.Max((int)interval, 4 * rx_rttval);
        rx_rto = Utils.Clamp(rto, rx_minrto, RTO_MAX);
    }
    void ParseUna(uint una)
    {
        int removed = 0;
        foreach (Segment seg in snd_buf)
        {
            if (Utils.TimeDiff(una, seg.sn) > 0)
            {
                ++removed;
                Segment.Return(seg);
            }
            else
            {
                break;
            }
        }
        snd_buf.RemoveRange(0, removed);
    }
    void ParseFastack(uint sn, uint ts)//处理快速ack，增加snd_buf中对应segment的fastack计数
    {
        if (sn < snd_una || sn >= snd_nxt)
            return;

        foreach (Segment seg in snd_buf)
        {
            // if (Utils.TimeDiff(sn, seg.sn) < 0)
            if (sn < seg.sn)
            {
                break;
            }
            else if (sn != seg.sn)
            {
                seg.fastack++;
            }
        }
    }
    internal void ShrinkBuf()
    {
        if (snd_buf.Count > 0)
        {
            Segment seg = snd_buf[0];
            snd_una = seg.sn;
        }
        else
        {
            snd_una = snd_nxt;
        }
    }

    public int Input(byte[] data, int offset, int size)
    {
        uint prev_una = snd_una;
        uint maxack = 0;
        uint latest_ack = 0;
        int flag = 0;


        if (data == null || size < OVERHEAD)
            return -1;

        while(true)
        {
            if (size < OVERHEAD)
                break;

            offset += Utils.Decode32Bit(data, offset, out uint _conv);
            if (_conv != conv) return -1;

            offset += Utils.Decode8Bit(data, offset, out byte _cmd);
            offset += Utils.Decode8Bit(data, offset, out byte _frg);
            offset += Utils.Decode16Bit(data, offset, out ushort _wnd);
            offset += Utils.Decode32Bit(data, offset, out uint _ts);
            offset += Utils.Decode32Bit(data, offset, out uint _sn);
            offset += Utils.Decode32Bit(data, offset, out uint _una);
            offset += Utils.Decode32Bit(data, offset, out uint _len);

            size -= OVERHEAD;
            //len could be 0, because CMD_ACK, CMD_WASK, CMD_WINS has no data
            if (size < _len || (int)_len < 0)
                return -2;
            //unknow comd
            if (_cmd != CMD_PUSH && _cmd != CMD_ACK &&
                _cmd != CMD_WASK && _cmd != CMD_WINS)
                return -3;

            rmt_wnd_rem = _wnd;
            ParseUna(_una);
            ShrinkBuf();

            switch (_cmd)
            {
                case CMD_ACK:
                    if (Utils.TimeDiff(current, _ts) >= 0)
                    {
                        UpdateAck(Utils.TimeDiff(current, _ts));
                    }
                    ParseAck(_sn);
                    ShrinkBuf();
                    if (flag == 0)//first in
                    {
                        flag = 1;
                        maxack = _sn;
                        latest_ack = _ts;
                    }
                    else
                    {
                        if (Utils.TimeDiff(_sn, maxack) > 0)//maxack sn
                        {
                            maxack = _sn;
                            latest_ack = _ts;
                        }
                    }
                    break;

                case CMD_PUSH:
                    if (Utils.TimeDiff(_sn, rcv_nxt + rcv_wnd_cap) < 0)
                    {
                        AckPush(_sn, _ts);//add ack to acklist
                        if (Utils.TimeDiff(_sn, rcv_nxt) >= 0)
                        {
                            Segment seg = Segment.New();
                            seg.conv = _conv;
                            seg.cmd = _cmd;
                            seg.frg = _frg;
                            seg.wnd = _wnd;
                            seg.ts = _ts;
                            seg.sn = _sn;
                            seg.una = _una;
                            if (_len > 0)
                            {
                                seg.data.Write(data, offset, (int)_len);
                            }
                            ParseData(seg);
                        }
                    }
                    break;

                case CMD_WASK:
                    probe |= ASK_TELL;
                    break;

                case CMD_WINS:
                    break;

                default:
                    return -3;
            }
            offset += (int)_len;
            size -= (int)_len;
        }

        if(flag != 0)//exist ack segment
        {
            ParseFastack(maxack, latest_ack);
        }

        //慢启动和拥塞控制计算cwnd
        if (Utils.TimeDiff(snd_una, prev_una) > 0)
        {
            if (cwnd < rmt_wnd_rem)
            {
                if (cwnd < ss_threshold)
                {
                    cwnd++;
                    incr += mss;
                }
                else
                {
                    if (incr < mss) incr = mss;
                    incr += (mss * mss) / incr + (mss / 16);
                    if ((cwnd + 1) * mss <= incr)
                    {
                        cwnd = (incr + mss - 1) / ((mss > 0) ? mss : 1);
                    }
                }
                if (cwnd > rmt_wnd_rem)
                {
                    cwnd = rmt_wnd_rem;
                    incr = rmt_wnd_rem * mss;
                }
            }
        }

        return 0;
    }

    public int PeekSize()
    {
        int length = 0;

        if (rcv_queue.Count == 0)
            return -1;

        Segment seq = rcv_queue.Peek();

        if (seq.frg == 0) 
            return (int)seq.data.Position;

        if (rcv_queue.Count < seq.frg + 1) 
            return -1;

        foreach (Segment seg in rcv_queue)
        {
            length += (int)seg.data.Position;
            if (seg.frg == 0) break;
        }

        return length;
    }
    public int Receive(byte[] buffer, int len)
    {
        if (len < 0)
            throw new NotSupportedException("No Data Receive");

        if(rcv_queue.Count == 0)
            return -1;

        if (len < 0)
            throw new NotSupportedException("Len is negative");

        int peekSize = PeekSize();

        if (peekSize < 0)
            return -2;

        if (peekSize > len)
            return -3;

        bool recover = (rcv_queue.Count >= rcv_wnd_cap);//说明接收窗口已经满了
        int offset = 0;
        len = 0;

        while(rcv_queue.Count > 0)
        {
            Segment seg = rcv_queue.Dequeue();
            Buffer.BlockCopy(seg.data.GetBuffer(), 0, buffer, offset, (int)seg.data.Position);
            offset += (int)seg.data.Position;
            len += (int)seg.data.Position;
            uint fragment = seg.frg;

            Segment.Return(seg);

            if (fragment == 0)
                break;
        }

        MoveReceiveBufferReadySegmentsToQueue();

        if (rcv_queue.Count < rcv_wnd_cap && recover)
        {
            // ready to send back CMD_WINS in flush
            // tell remote my window size
            probe |= ASK_TELL;
        }
        return len;
    }

    #endregion

    #region Init And Reset

    public void SetDeadLink(uint dead_link)
        => this.dead_link = dead_link;
    public void SetMtu(uint mtu)
    {
        if (mtu < 64 || mtu < OVERHEAD)
            throw new ArgumentException("MTU must be higher than 6400 and higher than OVERHEAD");

        buffer = new byte[(mtu + OVERHEAD) * 3];
        this.mtu = mtu;
        mss = mtu - OVERHEAD;
    }

    public void SetNoDelay(uint nodelay, uint interval = INTERVAL, int fastresend = 0)
    {
        this.nodelay = nodelay;
        if(nodelay != 0)
        {
            rx_minrto = RTO_NDL;
        }
        else
        {
            rx_minrto = RTO_MIN;
        }

        if (interval >= 0)
        {
            // clamp interval between 10 and 5000
            if (interval > 5000) interval = 5000;
            else if (interval < 10) interval = 10;
            this.interval = interval;
        }

        if (fastresend >= 0)
        {
            this.fastresend = fastresend;
        }

    }
    public void SetWndSize(uint sendWndSize, uint receiveWndSize)
    {
        if(sendWndSize > 0)
        {
            snd_wnd_cap = sendWndSize;
        }

        if (receiveWndSize > 0)
        {
            // must >= max fragment size
            rcv_wnd_cap = Math.Max(receiveWndSize, WND_RCV);
        }
    }

    #endregion
}
