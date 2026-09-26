using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class KCP
{
    private class Segment
    {
        public uint conv;//会话编号                                        4 byte 32 bit
        public uint cmd;//command                                          1 byte 8 bit
        public uint frg;//分片数量. 表示随后还有多少个报文属于同一个包     1 byte 8 bit
        public uint wnd;//发送方剩余接收窗口的大小                         2 byte 16 bit
        public uint ts;//时间戳                                            4 byte 32 bit
        public uint sn;//序列号                                            4 byte 32 bit
        public uint una;//发送方接收缓冲区中最小还未收到的报文段的编号     4 byte 32 bit
        public uint len;//数据长度                                         4 byte 32 bit

        public uint resendts; // 重传时间戳
        public int rto;//rto Retransmission TimeOut, 重传超时时间
        public uint fastack;//fastack
        public uint xmit;//重传次数

        public MemoryStream data = new MemoryStream(KCP.MTU_DEF);
        
        private static Queue<Segment> pool = new Queue<Segment>();
        public static Segment New()
        {
            if (pool.Count > 0)
            {
                Segment res = pool.Dequeue();
                res.Reset();
                return res;
            }
            else
            {
                Segment res = new Segment();
                res.Reset();
                return res;
            }
        }
        public static void Return(Segment value)
            => pool.Enqueue(value);
        //encode the segment into bytes, return the number of bytes written
        public int Encode(byte[] bytes, int offset)
        {
            int written = 0;

            written += Utils.Encode32Bit(bytes, offset + written, conv);
            written += Utils.Encode8Bit(bytes, offset + written, (byte)cmd);
            written += Utils.Encode8Bit(bytes, offset + written, (byte)frg);
            written += Utils.Encode16Bit(bytes, offset + written, (ushort)wnd);
            written += Utils.Encode32Bit(bytes, offset + written, ts);
            written += Utils.Encode32Bit(bytes, offset + written, sn);
            written += Utils.Encode32Bit(bytes, offset + written, una);

            len = (uint)data.Position;
            written += Utils.Encode32Bit(bytes, offset + written, len);
            return written;
        }
        public void Reset()
        {
            conv = 0;
            cmd = 0;
            frg = 0;
            wnd = 0;
            ts = 0;
            sn = 0;
            una = 0;

            len = 0;
            data.SetLength(0);

            resendts = 0;
            rto = 0;
            fastack = 0;
            xmit = 0;
        }
        public void WriteTo()
        {
        }
    }
}
