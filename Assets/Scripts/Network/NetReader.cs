using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements.Experimental;

public class NetReader
{
    private ArraySegment<byte> buffer;

    public int Position;
    public int Remaining => Capacity - Position;
    public int Capacity => buffer.Count;

    private readonly UTF8Encoding encoding = new UTF8Encoding(false, true);

    public NetReader(ArraySegment<byte> segment)
    {
        buffer = segment;
    }

    public void SetBuffer(ArraySegment<byte> segment)
    {
        buffer = segment;
        Position = 0;
    }
    public byte ReadByte() => ReadBlittable<byte>();
    public byte[] ReadBytes(byte[] bytes, int count)
    {
        // user may call ReadBytes(ReadInt()). ensure positive count.
        if (count < 0) throw new ArgumentOutOfRangeException("ReadBytes requires count >= 0");

        // check if passed byte array is big enough
        if (count > bytes.Length)
        {
            throw new EndOfStreamException($"ReadBytes can't read {count} + bytes because the passed byte[] only has length {bytes.Length}");
        }
        // ensure remaining
        if (Remaining < count)
        {
            throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
        }

        Array.Copy(buffer.Array, buffer.Offset + Position, bytes, 0, count);
        Position += count;
        return bytes;
    }

    //mirror 源码
    private unsafe T ReadBlittable<T>()
        where T : unmanaged
    {
        // calculate size
        //   sizeof(T) gets the managed size at compile time.
        //   Marshal.SizeOf<T> gets the unmanaged size at runtime (slow).
        // => our 1mio writes benchmark is 6x slower with Marshal.SizeOf<T>
        // => for blittable types, sizeof(T) is even recommended:
        // https://docs.microsoft.com/en-us/dotnet/standard/native-interop/best-practices
        int size = sizeof(T);

        // ensure remaining
        if (Remaining < size)
        {
            throw new EndOfStreamException($"ReadBlittable<{typeof(T)}> not enough data in buffer to read {size} bytes: {ToString()}");
        }
        // read blittable
        T value;
        fixed (byte* ptr = &buffer.Array[buffer.Offset + Position])
        {
            value = *(T*)ptr;
        }
        Position += size;
        return value;
    }
    private T? ReadBlittableNullable<T>()
        where T : unmanaged
    {
        if (ReadByte() != 0)
            return ReadBlittable<T>();
        else
            return default;
    }
    public ArraySegment<byte> ReadBytesSegment(int count)
    {
        // user may call ReadBytes(ReadInt()). ensure positive count.
        if (count < 0)
            throw new ArgumentOutOfRangeException("ReadBytesSegment requires count >= 0");

        // ensure remaining
        if (Remaining < count)
        {
            throw new EndOfStreamException($"ReadBytesSegment can't read {count} bytes because it would read past the end of the stream. {ToString()}");
        }
        // return the segment
        ArraySegment<byte> result = new ArraySegment<byte>(buffer.Array, buffer.Offset + Position, count);
        Position += count;
        return result;
    }
    public T Read<T>()
    {
        Func<NetReader, T> readerDelegate = Reader<T>.read;
        if (readerDelegate == null)
        {
            Debug.LogError($"struct value type {typeof(T)}not exist for Read");
            return default;
        }
        return readerDelegate(this);
    }
    public T[] ReadArray<T>()
    {
        uint length = (uint)Compression.DecompressVarUInt(this);
        
        if (length == 0) 
            return null;
        
        length -= 1;
        
        T[] result = new T[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = Read<T>();
        }
        return result;
    }

    static NetReader()
    {
        NetMessages.RegisterAllMessage();
        Reader<byte>.read = ReadByte;
        Reader<sbyte>.read = ReadSByte;
        Reader<char>.read = ReadChar;
        Reader<bool>.read = ReadBool;
        Reader<short>.read = ReadShort;
        Reader<ushort>.read = ReadUShort;
        Reader<int>.read = ReadInt;
        Reader<uint>.read = ReadUInt;
        Reader<long>.read = ReadLong;
        Reader<ulong>.read = ReadULong;
        Reader<float>.read = ReadFloat;
        Reader<double>.read = ReadDouble;

        Reader<Vector2>.read = ReadVector2;
        Reader<Vector3>.read = ReadVector3;
        Reader<Vector4>.read = ReadVector4;
        Reader<Quaternion>.read = ReadQuaternion;
        Reader<ArraySegment<byte>>.read = ReadByteArraySegmentAndSize;

        Reader<string>.read = ReadString;

    }

    public static byte ReadByte(NetReader reader) => reader.ReadBlittable<byte>();
    public static sbyte ReadSByte(NetReader reader) => reader.ReadBlittable<sbyte>();
    public static char ReadChar(NetReader reader) => (char)reader.ReadBlittable<ushort>();

    // bool is not blittable. read as byte.
    public static bool ReadBool(NetReader reader) => reader.ReadBlittable<byte>() != 0;
    public static short ReadShort(NetReader reader) => (short)ReadUShort(reader);
    public static ushort ReadUShort(NetReader reader) => reader.ReadBlittable<ushort>();
    public static int ReadInt(NetReader reader) => reader.ReadBlittable<int>();
    public static uint ReadUInt(NetReader reader) => reader.ReadBlittable<uint>();
    public static long ReadLong(NetReader reader) => reader.ReadBlittable<long>();
    public static ulong ReadULong(NetReader reader) => reader.ReadBlittable<ulong>();

    public static float ReadFloat(NetReader reader) => reader.ReadBlittable<float>();

    public static double ReadDouble(NetReader reader) => reader.ReadBlittable<double>();

    public static Vector2 ReadVector2(NetReader reader) => reader.ReadBlittable<Vector2>();
    public static Vector3 ReadVector3(NetReader reader) => reader.ReadBlittable<Vector3>();
    public static Vector4 ReadVector4(NetReader reader) => reader.ReadBlittable<Vector4>();
    public static Quaternion ReadQuaternion(NetReader reader) => reader.ReadBlittable<Quaternion>();

    public static ArraySegment<byte> ReadByteArraySegmentAndSize(NetReader reader)
    {
        uint count = (uint)Compression.DecompressVarUInt(reader);
        //这里mirror会将empty bytes数组置为1，null为0，所以所有的大小会+1
        //read的时候要还原-1
        if (count == 0)
            return default;
        else
            return reader.ReadBytesSegment(checked((int)(count - 1u)));
    }
    public static string ReadString(NetReader reader)
    {
        // read number of bytes
        ushort size = ReadUShort(reader);

        // null support, see NetworkWriter
        if (size == 0)
            return null;

        ushort realSize = (ushort)(size - 1);

        // make sure it's within limits to avoid allocation attacks etc.
        if (realSize > NetWriter.MaxStringLength)
            throw new EndOfStreamException($"NetworkReader.ReadString - Value too long: {realSize} bytes. Limit is: {NetWriter.MaxStringLength} bytes");

        ArraySegment<byte> data = reader.ReadBytesSegment(realSize);

        // convert directly from buffer to string via encoding
        // throws in case of invalid utf8.
        // see test: ReadString_InvalidUTF8()
        return reader.encoding.GetString(data.Array, data.Offset, data.Count);
    }

}
public static class Reader<T>
{
    public static Func<NetReader, T> read;
}
/// pool

public static class NetReaderPool
{
    private static readonly Pool<NetReaderPooled> pool = new
    (
        () => new NetReaderPooled(new byte[] { }),
        1000
    );
    public static int Count => pool.count;
    public static NetReaderPooled Get(byte[] bytes)
    {
        NetReaderPooled reader = pool.Get();
        reader.SetBuffer(bytes);
        return reader;
    }
    public static NetReaderPooled Get(ArraySegment<byte> segment)
    {
        NetReaderPooled reader = pool.Get();
        reader.SetBuffer(segment);
        return reader;
    }
    public static void Return(NetReaderPooled reader)
        => pool.Return(reader);
}
public class NetReaderPooled : NetReader, IDisposable
{
    public NetReaderPooled(byte[] bytes) : base(bytes) { }
    public NetReaderPooled(ArraySegment<byte> segment) : base(segment) { }

    public void Dispose()
        => NetReaderPool.Return(this);

}
