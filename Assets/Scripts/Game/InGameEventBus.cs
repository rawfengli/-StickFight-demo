using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static NetInGameManager;

public struct OnPlayerHitData
{
    public int playerIndex;
    public Vector3 damageDir;
    public Vector3 hitPos;
    public HitType hitType;
}
public struct OnPlayerLoopInfo
{
    public int playerIndex;
    public int hp;
    public float hpPercent;

}
[DefaultExecutionOrder(-10)]
public class InGameEventBus : MonoBehaviour
{
    private static InGameEventBus instance;
    public static InGameEventBus Instance => instance;

    public Action<OnPlayerHitData> OnPlayerHitEvent;
    public Action<OnPlayerLoopInfo> OnPlayerLoopEvent;
    private Action OnGameHasStartedEvent;
    private Action<int> OnGameFinishedEvent;
    public void Awake()
    {
        instance = this;
    }
    public void OnEnable()
    {
        EventBus.Register_GameHasStarted_Event(InvokeGameHasStarted, RoomEventLayer.Data);
        EventBus.Register_GameFinished_Event(InvokeGameFinished, RoomEventLayer.Data);

    }
    public void OnDisable()
    {
        EventBus.Unregister_GameHasStarted_Event(InvokeGameHasStarted, RoomEventLayer.Data);
        EventBus.Unregister_GameFinished_Event(InvokeGameFinished, RoomEventLayer.Data);

    }

    public void InvokeOnPlayerHit(OnPlayerHitData data)
        => OnPlayerHitEvent?.Invoke(data);
    public void InvokePlayerLoop(OnPlayerLoopInfo data)
        => OnPlayerLoopEvent?.Invoke(data);
    public void InvokeGameHasStarted(GameHasStartedNetData data)
        => OnGameHasStartedEvent?.Invoke();
    public void InvokeGameFinished(GameFinishedNetData data)
        => OnGameFinishedEvent?.Invoke(data.winnerIndex);

    public void Register_OnPlayerHit_Event(Action<OnPlayerHitData> _event)
        => OnPlayerHitEvent += _event;

    public void Unregister_OnPlayerHit_Event(Action<OnPlayerHitData> _event)
        => OnPlayerHitEvent += _event;

    public void Register_PlayerLoop_Event(Action<OnPlayerLoopInfo> _event)
        => OnPlayerLoopEvent += _event;

    public void Unregister_PlayerLoop_Event(Action<OnPlayerLoopInfo> _event)
        => OnPlayerLoopEvent += _event;
    public void Register_GameHasStarted_Event(Action _event)
        => OnGameHasStartedEvent += _event;

    public void Unregister_GameHasStarted_Event(Action _event)
        => OnGameHasStartedEvent += _event;

    public void Register_GameFinished_Event(Action<int> _event)
    => OnGameFinishedEvent += _event;

    public void Unregister_GameFinished_Event(Action<int> _event)
        => OnGameFinishedEvent += _event;

}
