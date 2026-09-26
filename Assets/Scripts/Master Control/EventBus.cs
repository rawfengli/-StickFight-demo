using System;
using System.Collections.Generic;
using static NetInGameManager;
using static NetRoomManager;

public enum RoomEventLayer
{
    Data = 0,
    UI = 1,
    Other = 2,
}

public static class EventBus
{
    #region Game Room

    private static List<Action<RoomCreatedNetData>> OnRoomCreatedEvents =
        new List<Action<RoomCreatedNetData>>() { null, null, null };

    private static List<Action<NewPlayerNetData>> OnRoomJoinedEvents =
        new List<Action<NewPlayerNetData>>() { null, null, null };
    private static List<Action<ReceiveStartGameData>> OnReceiveStartGameEvents =
        new List<Action<ReceiveStartGameData>>() { null, null, null };

    private static List<Action<GameFinishedNetData>> OnGameFinishedEvents =
        new List<Action<GameFinishedNetData>>() { null, null, null };

    //-------------------------------------------------------------------

    private static List<Action<NewPlayerNetData>> OnNewPlayerJoinedEvents =
        new List<Action<NewPlayerNetData>>() { null, null, null };

    //-------------------------------------------------------------------

    private static List<Action<OtherPlayerExitedNetData>> OnOtherPlayerExitedEvents =
        new List<Action<OtherPlayerExitedNetData>>() { null, null, null };

    private static List<Action<AnswerExitRequestData>> OnRoomExitedEvents =
        new List<Action<AnswerExitRequestData>>() { null, null, null };

    private static List<Action<GameHasStartedNetData>> OnGameHasStartedEvents =
        new List<Action<GameHasStartedNetData>>() { null, null, null };


    public static void InvokeRoomCreatedEvents(RoomCreatedNetData data)
    {
        foreach (var action in OnRoomCreatedEvents)
            action?.Invoke(data);
    }

    public static void InvokeRoomJoinedEvents(NewPlayerNetData data)
    {
        foreach (var action in OnRoomJoinedEvents)
            action?.Invoke(data);
    }
    public static void InvokeReceiveStartGameEvents(ReceiveStartGameData data)
    {
        foreach (var action in OnReceiveStartGameEvents)
            action?.Invoke(data);
    }

    public static void InvokeGameFinishedEvents(GameFinishedNetData data)
    {
        foreach (var action in OnGameFinishedEvents)
            action?.Invoke(data);
    }
    public static void InvokeNewPlayerJoinedEvents(NewPlayerNetData data)
    {
        foreach (var action in OnNewPlayerJoinedEvents)
            action?.Invoke(data);
    }

    public static void InvokeOtherPlayerExitedEvents(OtherPlayerExitedNetData data)
    {
        foreach (var action in OnOtherPlayerExitedEvents)
            action?.Invoke(data);
    }

    public static void InvokeRoomExitedEvents(AnswerExitRequestData data)
    {
        foreach (var action in OnRoomExitedEvents)
            action?.Invoke(data);
    }
    public static void InvokeGameHasStarted(GameHasStartedNetData data)
    {
        foreach (var action in OnGameHasStartedEvents)
            action?.Invoke(data);
    }
    public static void Register_RoomJoined_Event(
        Action<NewPlayerNetData> _event,
        RoomEventLayer layer)
        => OnRoomJoinedEvents[(int)layer] += _event;

    public static void Unregister_RoomJoined_Event(
        Action<NewPlayerNetData> _event,
        RoomEventLayer layer)
        => OnRoomJoinedEvents[(int)layer] -= _event;

    public static void Register_ReceiveStartGame_Event(
        Action<ReceiveStartGameData> _event,
        RoomEventLayer layer)
        => OnReceiveStartGameEvents[(int)layer] += _event;

    public static void Unregister_ReceiveStartGame_Event(
        Action<ReceiveStartGameData> _event,
        RoomEventLayer layer)
        => OnReceiveStartGameEvents[(int)layer] -= _event;

    public static void Register_GameFinished_Event(
        Action<GameFinishedNetData> _event,
        RoomEventLayer layer)
        => OnGameFinishedEvents[(int)layer] += _event;

    public static void Unregister_GameFinished_Event(
        Action<GameFinishedNetData> _event,
        RoomEventLayer layer)
        => OnGameFinishedEvents[(int)layer] -= _event;


    public static void Register_RoomCreated_Event(
        Action<RoomCreatedNetData> _event,
        RoomEventLayer layer)
        => OnRoomCreatedEvents[(int)layer] += _event;

    public static void Unregister_RoomCreated_Event(
        Action<RoomCreatedNetData> _event,
        RoomEventLayer layer)
        => OnRoomCreatedEvents[(int)layer] -= _event;


    public static void Register_NewPlayerJoined_Event(
        Action<NewPlayerNetData> _event,
        RoomEventLayer layer)
        => OnNewPlayerJoinedEvents[(int)layer] += _event;

    public static void Unregister_NewPlayerJoined_Event(
        Action<NewPlayerNetData> _event,
        RoomEventLayer layer)
        => OnNewPlayerJoinedEvents[(int)layer] -= _event;


    public static void Register_OtherPlayerExited_Event(
        Action<OtherPlayerExitedNetData> _event,
        RoomEventLayer layer)
        => OnOtherPlayerExitedEvents[(int)layer] += _event;

    public static void Unregister_OtherPlayerExited_Event(
        Action<OtherPlayerExitedNetData> _event,
        RoomEventLayer layer)
        => OnOtherPlayerExitedEvents[(int)layer] -= _event;


    public static void Register_RoomExited_Event(
        Action<AnswerExitRequestData> _event,
        RoomEventLayer layer)
        => OnRoomExitedEvents[(int)layer] += _event;

    public static void Unregister_RoomExited_Event(
        Action<AnswerExitRequestData> _event,
        RoomEventLayer layer)
        => OnRoomExitedEvents[(int)layer] -= _event;
    public static void Register_GameHasStarted_Event(
        Action<GameHasStartedNetData> _event,
        RoomEventLayer layer)
        => OnGameHasStartedEvents[(int)layer] += _event;

    public static void Unregister_GameHasStarted_Event(
        Action<GameHasStartedNetData> _event,
        RoomEventLayer layer)
        => OnGameHasStartedEvents[(int)layer] -= _event;

    #endregion
}
