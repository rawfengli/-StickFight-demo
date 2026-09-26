using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Compression 
{
    public static bool ScaleToLong(float value, out long result, float precision)
    {
        if (precision == 0) 
            throw new DivideByZeroException($"ScaleToLong: precision=0 would cause null division.");

        try
        {
            result = Convert.ToInt64(value / precision);
            return true;
        }
        catch (OverflowException)
        {
            result = value > 0 ? long.MaxValue : long.MinValue;
            return false;
        }

    }
    /*
    public static bool ScaleToFloat(Vector3 value, float precision, out Vector3Long quantized)
    {

    }
    */
    public static bool ScaleToLong(Vector3 value, out ValueTuple<long, long, long> scaleValue, float precision)
    {
        bool result = true;
        result &= ScaleToLong(value.x, out long x, precision);
        result &= ScaleToLong(value.y, out long y, precision);
        result &= ScaleToLong(value.z, out long z, precision);
        scaleValue = (x, y, z);
        return result;
    }
    public static bool ScaleToLong(Quaternion value, out ValueTuple<long, long, long, long> scaleValue, float precision)
    {
        bool result = true;
        result &= ScaleToLong(value.x, out long x, precision);
        result &= ScaleToLong(value.y, out long y, precision);
        result &= ScaleToLong(value.z, out long z, precision);
        result &= ScaleToLong(value.w, out long w, precision);
        scaleValue = (x, y, z, w);
        return result;
    }


    public static float ScaleToFloat(long value, float precision)
    {
        if (precision == 0) 
            throw new DivideByZeroException($"ScaleToLong: precision=0 would cause null division.");

        return value * precision;
    }
    public static Vector3 ScaleToFloat(ValueTuple<long, long, long> scaleValue, float precision)
    {
        Vector3 value;
        value.x = ScaleToFloat(scaleValue.Item1, precision);
        value.y = ScaleToFloat(scaleValue.Item2, precision);
        value.z = ScaleToFloat(scaleValue.Item3, precision);
        return value;
    }
    public static Quaternion ScaleToFloat(ValueTuple<long, long, long, long> scaleValue, float precision)
    {
        Quaternion value;
        value.x = ScaleToFloat(scaleValue.Item1, precision);
        value.y = ScaleToFloat(scaleValue.Item2, precision);
        value.z = ScaleToFloat(scaleValue.Item3, precision);
        value.w = ScaleToFloat(scaleValue.Item4, precision);
        return value;
    }


    public static int VarUIntSize(ulong value)
    {
        if (value <= 240)
            return 1;
        if (value <= 2287)
            return 2;
        if (value <= 67823)
            return 3;
        if (value <= 16777215)
            return 4;
        if (value <= 4294967295)
            return 5;
        if (value <= 1099511627775)
            return 6;
        if (value <= 281474976710655)
            return 7;
        if (value <= 72057594037927935)
            return 8;
        return 9;
    }
    public static int VarIntSize(long value)
    {
        // CompressVarInt zigzags it first
        ulong zigzagged = (ulong)((value >> 63) ^ (value << 1));
        return VarUIntSize(zigzagged);
    }
    /// <summary>
    /// notice: this fuc will write in netwriter
    /// </summary>
    public static void CompressVarUInt(NetWriter writer, ulong value)
    {
        if (value <= 240)
        {
            byte a = (byte)value;
            writer.WriteByte(a);
            return;
        }
        if (value <= 2287)
        {
            byte a = (byte)(((value - 240) >> 8) + 241);
            byte b = (byte)((value - 240) & 0xFF);
            NetWriter.WriteUShort(writer, (ushort)(b << 8 | a));
            return;
        }
        if (value <= 67823)
        {
            byte a = (byte)249;
            byte b = (byte)((value - 2288) >> 8);
            byte c = (byte)((value - 2288) & 0xFF);
            writer.WriteByte(a);
            NetWriter.WriteUShort(writer, (ushort)(c << 8 | b));
            return;
        }
        if (value <= 16777215)
        {
            byte a = (byte)250;
            uint b = (uint)(value << 8);
            NetWriter.WriteUInt(writer, b | a);
            return;
        }
        if (value <= 4294967295)
        {
            byte a = (byte)251;
            uint b = (uint)value;
            writer.WriteByte(a);
            NetWriter.WriteUInt(writer, b);
            return;
        }
        if (value <= 1099511627775)
        {
            byte a = (byte)252;
            byte b = (byte)(value & 0xFF);
            uint c = (uint)(value >> 8);
            NetWriter.WriteUShort(writer, (ushort)(b << 8 | a));
            NetWriter.WriteUInt(writer, c);
            return;
        }
        if (value <= 281474976710655)
        {
            byte a = (byte)253;
            byte b = (byte)(value & 0xFF);
            byte c = (byte)((value >> 8) & 0xFF);
            uint d = (uint)(value >> 16);
            writer.WriteByte(a);
            NetWriter.WriteUShort(writer, (ushort)(c << 8 | b));
            NetWriter.WriteUInt(writer, d);
            return;
        }
        if (value <= 72057594037927935)
        {
            byte a = 254;
            ulong b = value << 8;
            NetWriter.WriteULong(writer, b | a);
            return;
        }

        writer.WriteByte(255);
        NetWriter.WriteULong(writer, value);
    }
    //read netreader size
    public static ulong DecompressVarUInt(NetReader reader)
    {
        byte a0 = reader.ReadByte();
        if (a0 < 241)
        {
            return a0;
        }

        byte a1 = reader.ReadByte();
        if (a0 <= 248)
        {
            return 240 + ((a0 - (ulong)241) << 8) + a1;
        }

        byte a2 = reader.ReadByte();
        if (a0 == 249)
        {
            return 2288 + ((ulong)a1 << 8) + a2;
        }

        byte a3 = reader.ReadByte();
        if (a0 == 250)
        {
            return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16);
        }

        byte a4 = reader.ReadByte();
        if (a0 == 251)
        {
            return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24);
        }

        byte a5 = reader.ReadByte();
        if (a0 == 252)
        {
            return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32);
        }

        byte a6 = reader.ReadByte();
        if (a0 == 253)
        {
            return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40);
        }

        byte a7 = reader.ReadByte();
        if (a0 == 254)
        {
            return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48);
        }

        byte a8 = reader.ReadByte();
        if (a0 == 255)
        {
            return a1 + (((ulong)a2) << 8) + (((ulong)a3) << 16) + (((ulong)a4) << 24) + (((ulong)a5) << 32) + (((ulong)a6) << 40) + (((ulong)a7) << 48) + (((ulong)a8) << 56);
        }

        throw new IndexOutOfRangeException($"DecompressVarInt failure: {a0}");
    }

    // zigzag encoding https://gist.github.com/mfuerstenau/ba870a29e16536fdbaba
    public static void CompressVarInt(NetWriter writer, long i)
    {
        ulong zigzagged = (ulong)((i >> 63) ^ (i << 1));
        CompressVarUInt(writer, zigzagged);
    }
    public static long DecompressVarInt(NetReader reader)
    {
        ulong data = DecompressVarUInt(reader);
        return ((long)(data >> 1)) ^ -((long)data & 1);
    }
}
