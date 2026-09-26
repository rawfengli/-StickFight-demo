using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

public static class NetLoop
{
    internal enum AddMode { Beginning, End }

    private static event Action OnEarlyUpdate;
    private static event Action OnLateUpdate;

    public static void RegisterEventOnEarlyUpdate(Action _event)
        => OnEarlyUpdate += _event;
    public static void UnregisterEventOnEarlyUpdate(Action _event)
        => OnEarlyUpdate -= _event;
    public static void RegisterEventOnLateUpdate(Action _event)
        => OnLateUpdate += _event;
    public static void UnregisterEventOnLateUpdate(Action _event)
        => OnLateUpdate -= _event;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reset()
    {
        OnEarlyUpdate = null;
        OnLateUpdate = null;
    }

    internal static bool AddToPlayerLoop(
        PlayerLoopSystem.UpdateFunction function, Type ownerType,
        ref PlayerLoopSystem playerLoop, Type playerLoopSystemType, AddMode addMode)
    {
        if (playerLoop.type == playerLoopSystemType)
        {
            if (Array.FindIndex(playerLoop.subSystemList, (s => s.updateDelegate == function)) != -1)
                return true;

            int oldListLength;
            if (playerLoop.subSystemList != null)
                oldListLength = playerLoop.subSystemList.Length;
            else
                oldListLength = 0;

            Array.Resize(ref playerLoop.subSystemList, oldListLength + 1);

            PlayerLoopSystem system = new PlayerLoopSystem
            {
                type = ownerType,
                updateDelegate = function
            };

            if (addMode == AddMode.Beginning)
            {
                Array.Copy(
                    playerLoop.subSystemList, 0, 
                    playerLoop.subSystemList, 1, 
                    oldListLength);

                playerLoop.subSystemList[0] = system;
            }
            else if (addMode == AddMode.End)
            {
                playerLoop.subSystemList[oldListLength] = system;
            }
            return true;
        }

        if (playerLoop.subSystemList != null)
        {
            for (int i = 0; i < playerLoop.subSystemList.Length; ++i)
            {
                if (AddToPlayerLoop(function, ownerType, ref playerLoop.subSystemList[i], playerLoopSystemType, addMode))
                    return true;
            }
        }
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RuntimeInitializeOnLoad()
    {
        PlayerLoopSystem playerLoop = PlayerLoop.GetCurrentPlayerLoop();

        AddToPlayerLoop(NetworkEarlyUpdate, typeof(NetLoop), ref playerLoop, typeof(EarlyUpdate), AddMode.End);

        AddToPlayerLoop(NetworkLateUpdate, typeof(NetLoop), ref playerLoop, typeof(PreLateUpdate), AddMode.End);

        PlayerLoop.SetPlayerLoop(playerLoop);

        //先把一些方法的注册放在这个地方
        GameRoomData.RegisterAllEvents();
    }

    static void NetworkEarlyUpdate()
    {
        if (!Application.isPlaying) 
            return;

        NetServer.NetworkEarlyUpdate();
        NetClient.NetworkEarlyUpdate();
        OnEarlyUpdate?.Invoke();
    }

    static void NetworkLateUpdate()
    {
        if (!Application.isPlaying) return;

        OnLateUpdate?.Invoke();
        NetServer.NetworkLateUpdate();
        NetClient.NetworkLateUpdate();
    }
}
