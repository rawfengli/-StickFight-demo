using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 使用泛型约束经可能避免将泛型T直接或隐式赋值给INetTransportData<T>以避免可能存在的装箱问题
/// </summary>
public interface INetTransportData<T> where T : struct, INetTransportData<T>
{
    Action<NetWriter, T> write { get; }
    Func<NetReader, T> read { get; }

}
public static class NetTransportData
{
    public static void Write<T>(NetWriter writer, T value)
        where T : struct, INetTransportData<T>
    {
        if (Writer<T>.write == null)
            Writer<T>.write = value.write;

        writer.Write<T>(value);
    }
    public static void Read<T>(NetReader reader, out T value)
        where T : struct, INetTransportData<T>
    {
        value = default;

        if (Reader<T>.read == null)
            Reader<T>.read = value.read;

        value = reader.Read<T>();
    }
}

