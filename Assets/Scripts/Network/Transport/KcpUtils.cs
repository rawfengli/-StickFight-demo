using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

//same with mirror's Utils 
public static partial class Utils 
{
    public static int Clamp(int value, int min, int max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    public static int Encode8Bit(byte[] bytes, int offset, byte value)
    {
        bytes[offset] = value;
        return 1;
    }
    public static int Decode8Bit(byte[] bytes, int offset, out byte value)
    {
        value = bytes[offset];
        return 1;
    }
    public static int Encode16Bit(byte[] bytes, int offset, ushort value)
    {
        bytes[offset + 0] = (byte)(value >> 0);
        bytes[offset + 1] = (byte)(value >> 8);
        return 2;
    }
    public static int Decode16Bit(byte[] bytes, int offset, out ushort value)
    {
        ushort result = 0;
        result |= (ushort)(bytes[offset + 0] << 0);
        result |= (ushort)(bytes[offset + 1] << 8);
        value = result;
        return 2;
    }
    public static int Encode32Bit(byte[] bytes, int offset, uint value)
    {
        bytes[offset + 0] = (byte)(value >> 0);
        bytes[offset + 1] = (byte)(value >> 8);
        bytes[offset + 2] = (byte)(value >> 16);
        bytes[offset + 3] = (byte)(value >> 24);
        return 4;
    }
    public static int Decode32Bit(byte[] bytes, int offset, out uint value)
    {
        uint result = 0;
        result |= (uint)(bytes[offset + 0] << 0);
        result |= (uint)(bytes[offset + 1] << 8);
        result |= (uint)(bytes[offset + 2] << 16);
        result |= (uint)(bytes[offset + 3] << 24);
        value = result;
        return 4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int TimeDiff(uint later, uint earlier)
    {
        return (int)(later - earlier);
    }
}
