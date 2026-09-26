using System;
using System.Collections.Generic;


public enum RemoteCallType 
{ 
    CommandFromServerToClient, 
    CommandFromClientToServer
}
public delegate void RemoteCallDelegate(NetBehaviour behaviour, NetReader reader, NetConnectionToClient conn);

public static class RemoteProcedureCalls
{
    class Invoker
    {
        public Type behaviourType;
        public RemoteCallType callType;
        public RemoteCallDelegate function;
        public bool requiresAuthority;
    }

    private static readonly Dictionary<ushort, Invoker> remoteCallDelegates = new();
    private static ushort RegisterDelegate(
        Type componentType,
        string functionName,
        RemoteCallType remoteCallType,
        RemoteCallDelegate func,
        bool cmdRequiresAuthority = true)
    {
        //不支持函数重载，不过因为RemoteCallDelegate的参数恒定，一般也不会出现重载的情况
        //多程序集存在冲突风险 
        string functionFullName = Utils.FullFunctionName(componentType, functionName);

        ushort hash = Utils.GetFunctionHashCode(functionFullName);
        if (remoteCallDelegates.ContainsKey(hash))
            return hash;

        remoteCallDelegates[hash] = new Invoker
        {
            behaviourType = componentType,
            callType = remoteCallType,
            function = func,
            requiresAuthority = cmdRequiresAuthority
        };
        return hash;
    }
    /// <summary>
    /// 从服务端发出的命令，在客户端执行，function变量应该是在服务端上执行的逻辑
    /// </summary>
    public static void RegisterServerCommand(Type componentType, string functionName, RemoteCallDelegate func, bool requiresAuthority)
        => RegisterDelegate(componentType, functionName, RemoteCallType.CommandFromServerToClient, func, requiresAuthority);
    /// <summary>
    /// 从客户端发出的命令，在服务端执行，function变量应该是在客户端上执行的逻辑
    /// </summary>
    public static void RegisterClientCommand(Type componentType, string functionName, RemoteCallDelegate func)
        => RegisterDelegate(componentType, functionName, RemoteCallType.CommandFromClientToServer, func);
    private static string GetMethodName(RemoteCallDelegate function)
    {
        return function.Method.Name;
    }
    public static bool GetFunctionMethodName(ushort functionHash, out string methodName)
    {
        if (remoteCallDelegates.TryGetValue(functionHash, out Invoker invoker))
        {
            string name = GetMethodName(invoker.function);
            methodName = name;
            return true;
        }
        methodName = "";
        return false;
    }
    private static bool GetInvokerForHash(ushort functionHash, RemoteCallType remoteCallType, out Invoker invoker)
    {
        if (remoteCallDelegates.TryGetValue(functionHash, out invoker))
        {
            if(invoker.callType == remoteCallType)
            {
                return true;
            }
        }
        invoker = null;
        return false;
    }
    public static bool Invoke(
        ushort functionHash, 
        RemoteCallType remoteCallType, 
        NetReader reader, 
        NetBehaviour behaviour, 
        NetConnectionToClient connection = null)//for client,conn is unless
    {

        if (GetInvokerForHash(functionHash, remoteCallType, out Invoker invoker) && 
            invoker.behaviourType.IsInstanceOfType(behaviour))
        {
            invoker.function(behaviour, reader, connection);
            return true;
        }
        return false;
    }
}



