using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UIElements;

public class NetWriter
{
    public const ushort MaxStringLength = ushort.MaxValue - 1;
    public const int DEFAULT_CAPACITY = 1024;
    internal byte[] buffer = new byte[DEFAULT_CAPACITY];

    public int Position;

    private readonly UTF8Encoding encoding = new UTF8Encoding(false, true);
    public void Reset()
    {
        Position = 0;
    }
    public byte[] ToArray()
    {
        byte[] data = new byte[Position];
        Array.ConstrainedCopy(buffer, 0, data, 0, Position);
        return data;
    }
    public ArraySegment<byte> ToArraySegment() =>
        new ArraySegment<byte>(buffer, 0, Position);
    private unsafe void WriteBlittable<T>(T value)
        where T : unmanaged
    {
        int size = sizeof(T);

        EnsureCapacity(Position + size);

        // write blittable
        fixed (byte* ptr = &buffer[Position])
        {
            // cast buffer to T* pointer, then assign value to the area
            *(T*)ptr = value;
        }
        Position += size;
    }
    private void WriteBlittableNullable<T>(T? value)
        where T : unmanaged
    {
        WriteByte((byte)(value.HasValue ? 0x01 : 0x00));

        if (value.HasValue)
            WriteBlittable(value.Value);
    }
    private void EnsureCapacity(int value)
    {
        if (buffer.Length < value)
        {
            int capacity = Math.Max(value, buffer.Length * 2);
            Array.Resize(ref buffer, capacity);
        }
    }
    public void WriteByte(byte value) => WriteBlittable(value);
    public void WriteBytes(byte[] array, int offset, int count)
    {
        EnsureCapacity(Position + count);//将array写入buffer
        Array.ConstrainedCopy(array, offset, this.buffer, Position, count);
        Position += count;
    }
    public void Write<T>(T value)
    {
        Action<NetWriter, T> writeDelegate = Writer<T>.write;
        if (writeDelegate == null)
        {
            Debug.LogError($"struct value type {typeof(T)}not exist for Write");
        }
        else
        {
            writeDelegate(this, value);
        }
    }
    public void WriteArray<T>(T[] array)
    {
        if (array is null)
        {
            Compression.CompressVarUInt(this, 0u);
            return;
        }

        Compression.CompressVarUInt(this, checked((uint)array.Length) + 1u);
        for (int i = 0; i < array.Length; i++)
            Write(array[i]);

    }

    //不用IL只能用这种方式来处理一些简单的情况
    static NetWriter()
    {
        NetMessages.RegisterAllMessage();
        Writer<byte>.write = WriteByte;
        Writer<sbyte>.write = WriteSByte;
        Writer<char>.write = WriteChar;
        Writer<bool>.write = WriteBool;
        Writer<short>.write = WriteShort;
        Writer<ushort>.write = WriteUShort;
        Writer<int>.write = WriteInt;
        Writer<uint>.write = WriteUInt;
        Writer<long>.write = WriteLong;
        Writer<ulong>.write = WriteULong;
        Writer<float>.write = WriteFloat;
        Writer<double>.write = WriteDouble;

        Writer<Vector2>.write = WriteVector2;
        Writer<Vector3>.write = WriteVector3;
        Writer<Vector4>.write = WriteVector4;
        Writer<Quaternion>.write = WriteQuaternion;
        Writer<ArraySegment<byte>>.write = WriteByteArraySegmentAndSize;
        Writer<string>.write = WriteString;
    }
    #region all type to write
    public static void WriteByte(NetWriter writer, byte value) => writer.WriteBlittable(value);
    public static void WriteSByte(NetWriter writer, sbyte value) => writer.WriteBlittable(value);
    public static void WriteSByteNullable(NetWriter writer, sbyte? value) => writer.WriteBlittableNullable(value);
    // char is not blittable. convert to ushort.
    public static void WriteChar(NetWriter writer, char value) => writer.WriteBlittable((ushort)value);
    // bool is not blittable. convert to byte.
    public static void WriteBool(NetWriter writer, bool value) => writer.WriteBlittable((byte)(value ? 1 : 0));

    public static void WriteShort(NetWriter writer, short value) => writer.WriteBlittable(value);

    public static void WriteUShort(NetWriter writer, ushort value) => writer.WriteBlittable(value);

    public static void WriteInt(NetWriter writer, int value) => writer.WriteBlittable(value);

    public static void WriteUInt(NetWriter writer, uint value) => writer.WriteBlittable(value);

    public static void WriteLong(NetWriter writer, long value) => writer.WriteBlittable(value);

    public static void WriteULong(NetWriter writer, ulong value) => writer.WriteBlittable(value);
    public static void WriteFloat(NetWriter writer, float value) => writer.WriteBlittable(value);
    public static void WriteDouble(NetWriter writer, double value) => writer.WriteBlittable(value);

    public static void WriteVector2(NetWriter writer, Vector2 value) => writer.WriteBlittable(value);
    public static void WriteVector3(NetWriter writer, Vector3 value) => writer.WriteBlittable(value);
    public static void WriteVector4(NetWriter writer, Vector4 value) => writer.WriteBlittable(value);
    public static void WriteQuaternion(NetWriter writer, Quaternion value) => writer.WriteBlittable(value);

    public static void WriteBytesAndSize(NetWriter writer, byte[] buffer, int offset, int count)
    {
        if (buffer == null)
        {
            Compression.CompressVarUInt(writer, 0u);
            return;
        }
        //这里mirror会将empty bytes数组置为1，null为0，所以所有的大小会+1
        Compression.CompressVarUInt(writer, checked((uint)count) + 1u);
        writer.WriteBytes(buffer, offset, count);
    }
    public static void WriteByteArraySegmentAndSize(NetWriter writer, ArraySegment<byte> segment)
        => WriteBytesAndSize(writer, segment.Array, segment.Offset, segment.Count);
    public static void WriteString(NetWriter writer, string value)
    {
        // we offset count by '1' to easily support null without writing another byte.
        // encoding null as '0' instead of '-1' also allows for better compression
        // (ushort vs. short / varuint vs. varint) etc.
        if (value == null)
        {
            WriteUShort(writer, 0);
            return;
        }

        // WriteString copies into the buffer manually.
        // need to ensure capacity here first, manually.
        int maxSize = writer.encoding.GetMaxByteCount(value.Length);
        writer.EnsureCapacity(writer.Position + 2 + maxSize); // 2 bytes position + N bytes encoding

        int written = writer.encoding.GetBytes(value, 0, value.Length, writer.buffer, writer.Position + 2);

        if (written > NetWriter.MaxStringLength)
            throw new IndexOutOfRangeException($"NetworkWriter.WriteString - Value too long: {written} bytes. Limit: {NetWriter.MaxStringLength} bytes");
        WriteUShort(writer, checked((ushort)(written + 1))); // Position += 2
        writer.Position += written;
    }

    #endregion
}
public static class Writer<T>
{
    public static Action<NetWriter, T> write;
}
//pool
public static class NetWriterPool
{
    private static readonly Pool<NetWriterPooled> pool = new
    (
        () => new NetWriterPooled(),
        1000
    );

    public static int Count => pool.count;
    public static NetWriterPooled Get()
    {
        // grab from pool & reset position
        NetWriterPooled writer = pool.Get();
        writer.Reset();
        return writer;
    }
    public static void Return(NetWriterPooled writer)
        => pool.Return(writer);
}
public class NetWriterPooled : NetWriter, IDisposable
{
    public void Dispose()
        => NetWriterPool.Return(this);
}
