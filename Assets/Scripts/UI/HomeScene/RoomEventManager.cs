using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using UnityEngine;
using static NetChatManager;
using static NetRoomManager;
public struct OnNewPlayerJoinedData
{
    public uint joinRoomID;

    public int playerIndex;
    public int avatarIndex;
    public string playerName;
    public OnNewPlayerJoinedData(uint joinRoomID, int playerIndex, int avatarIndex, string playerName)
    {

        this.joinRoomID = joinRoomID;
        this.playerIndex = playerIndex;
        this.avatarIndex = avatarIndex;
        this.playerName = playerName;
    }
}
public struct OnRoomJoinedData
{
    public uint joinRoomID;
    public int playerIndex;
    public JoinedRoomStatus status;
    //这样的话，每次加入房间会产生gc，不过影响应该不大
    public List<int> otherPlayerAvatarIndexs;
    public List<string> otherPlayerNames;
    public OnRoomJoinedData(uint roomID, int playerIndex, JoinedRoomStatus status, int[] avatarIndexs, string[] names)
    {
        otherPlayerAvatarIndexs = new();
        otherPlayerNames = new();

        this.joinRoomID = roomID;
        this.playerIndex = playerIndex;
        this.status = status;
        otherPlayerAvatarIndexs.Add(default);
        otherPlayerNames.Add(default);
        for (int i = 1; i <= GameRoomData.MAX_PLAYER_COUNT; i++)
        {
            otherPlayerAvatarIndexs.Add(avatarIndexs[i]);
            otherPlayerNames.Add(names[i]);
        }

    }
}
[DefaultExecutionOrder(-10)]
public class RoomEventManager : MonoBehaviour
{
    private static RoomEventManager instance;
    public static RoomEventManager Instance => instance;
    [Header("Invoker")]
    [SerializeField] private WRButton CreateRoomInvoker;
    //joinRoomInvoker这个地方要拦截并set join room id
    [SerializeField] private WRButton JoinRoomInvoker;
    [SerializeField] private WRButton StartGameInvoker;
    [SerializeField] private NetRoomManager NetRoomMessageInvoker;
    [SerializeField] private NetChatManager NetChatMessageInvoker;
    [SerializeField] private RoomOptionList OptionList;
    private Action OnCreateRoomEvent;
    private Action<uint> OnJoinRoomEvent;
    private Action<uint> OnStartGameEvent;
    private Action OnReceiveStartGameEvent;

    //join and create and room Available
    private Action<uint, int> OnRoomCreatedEvent;//roomID, playerIndex
    private Action<OnNewPlayerJoinedData> OnNewPlayerJoinedEvent;
    private Action<OnRoomJoinedData> OnRoomJoinedEvent;//joinRoomID, JoinedRoomStatus
    private Action<uint> OnRoomAvailableEvent;

    //exit,also Unavailable
    private Action<int> OnOtherPlayerExitedEvent;
    private Action OnRoomExitedEvent;
    //chat
    private Action<int, string, string> OnReceiveMessageEvent;
    private Action<ChatRoom> OnReceiveHistoricalMessagesEvent;

    private uint joinRoomID;


    private void Awake()
    {
        instance = this;
    }
    void InvokeCreateRoom(WRButton _)
        => OnCreateRoomEvent?.Invoke();
    void InvokeJoinRoom(WRButton _)
        => OnJoinRoomEvent?.Invoke(joinRoomID);

    void InvokeStartGame(WRButton _)
        => OnStartGameEvent.Invoke(GameRoomData.room.roomID);

    //net
    void InvokeNewPlayerJoin(NetRoomManager.NewPlayerNetData data)
        => OnNewPlayerJoinedEvent?.Invoke(new(
            data.roomID, 
            data.newPlayerIndex, 
            data.newPlayerAvatarIndex, 
            data.newPlayerName));
    void InvokeRoomJoined(NetRoomManager.NewPlayerNetData data)
        => OnRoomJoinedEvent?.Invoke(new(data.roomID, data.newPlayerIndex, data.status, data.otherPlayerAvatarIndexs, data.otherPlayerNames));
    void InvokeRoomCreated(NetRoomManager.RoomCreatedNetData data)
        => OnRoomCreatedEvent?.Invoke(data.roomID, data.playerIndex);

    void InvokeOtherPlayerExited(NetRoomManager.OtherPlayerExitedNetData data)
        => OnOtherPlayerExitedEvent?.Invoke(data.playerIndex);
    void InvokeReceiveStartGame(NetRoomManager.ReceiveStartGameData data)
        => OnReceiveStartGameEvent.Invoke();

    //room Available
    void InvokeExitRoom(NetRoomManager.AnswerExitRequestData _)
        => OnRoomExitedEvent?.Invoke();

    void InvokeRoomAvailable(uint roomID, int playerIndex)
        => OnRoomAvailableEvent?.Invoke(roomID);
    void InvokeRoomAvailable(OnRoomJoinedData data)
        => OnRoomAvailableEvent?.Invoke(data.joinRoomID);
    //chat 
    void InvokeReceiveMessage(ReplyChatMessageData data)
        => OnReceiveMessageEvent?.Invoke(data.playerIndex, data.playerName, data.content);
    void InvokeReceiveHistoricalMessages(ChatRoom room)
    => OnReceiveHistoricalMessagesEvent?.Invoke(room);
    //方法变量封装
    private void OnEnable()
    {
        // common UI
        CreateRoomInvoker.OnReleased += InvokeCreateRoom;
        JoinRoomInvoker.OnReleased += InvokeJoinRoom;
        StartGameInvoker.OnReleased += InvokeStartGame;

        //event bus
        EventBus.Register_NewPlayerJoined_Event(InvokeNewPlayerJoin, RoomEventLayer.UI);
        EventBus.Register_RoomCreated_Event(InvokeRoomCreated, RoomEventLayer.UI);
        EventBus.Register_RoomJoined_Event(InvokeRoomJoined, RoomEventLayer.UI);

        EventBus.Register_RoomExited_Event(InvokeExitRoom, RoomEventLayer.UI);
        EventBus.Register_OtherPlayerExited_Event(InvokeOtherPlayerExited, RoomEventLayer.UI);
        EventBus.Register_ReceiveStartGame_Event(InvokeReceiveStartGame, RoomEventLayer.UI);

        //chat
        NetChatMessageInvoker.OnReceiveMessage += InvokeReceiveMessage;
        NetChatMessageInvoker.OnReceiveHistoricalMessages += InvokeReceiveHistoricalMessages;

        Register_RoomCreated_Event(InvokeRoomAvailable);
        Register_RoomJoined_Event(InvokeRoomAvailable);
    }
    private void OnDisable()
    {
        CreateRoomInvoker.OnReleased -= InvokeCreateRoom;
        JoinRoomInvoker.OnReleased -= InvokeJoinRoom;
        StartGameInvoker.OnReleased -= InvokeStartGame;

        EventBus.Unregister_NewPlayerJoined_Event(InvokeNewPlayerJoin, RoomEventLayer.UI);
        EventBus.Unregister_RoomCreated_Event(InvokeRoomCreated, RoomEventLayer.UI);
        EventBus.Unregister_RoomJoined_Event(InvokeRoomJoined, RoomEventLayer.UI);
                 
        EventBus.Unregister_RoomExited_Event(InvokeExitRoom, RoomEventLayer.UI);
        EventBus.Unregister_OtherPlayerExited_Event(InvokeOtherPlayerExited, RoomEventLayer.UI);
        EventBus.Unregister_ReceiveStartGame_Event(InvokeReceiveStartGame, RoomEventLayer.UI);

        NetChatMessageInvoker.OnReceiveMessage -= InvokeReceiveMessage;
        NetChatMessageInvoker.OnReceiveHistoricalMessages -= InvokeReceiveHistoricalMessages;

        Unregister_RoomCreated_Event(InvokeRoomAvailable);
        Unregister_RoomJoined_Event(InvokeRoomAvailable);
    }

    //注册方法可以像mirror一样同质化处理,不过这个地方就算了
    #region Event Register
    #region Register
    //common
    public void Register_CreateRoom_Event(Action _event)
        => OnCreateRoomEvent += _event;
    public void Register_JoinRoom_Event(Action<uint> _event)
        => OnJoinRoomEvent += _event;
    public void Register_StartGame_Event(Action<uint> _event)
        => OnStartGameEvent += _event;
    // net
    public void Register_NewPlayerJoined_Event(Action<OnNewPlayerJoinedData> _event)
        => OnNewPlayerJoinedEvent += _event;

    public void Register_RoomCreated_Event(Action<uint, int> _event)
        => OnRoomCreatedEvent += _event;
    public void Register_RoomJoined_Event(Action<OnRoomJoinedData> _event)
        => OnRoomJoinedEvent += _event;
    public void Register_OnOtherPlayerExited_Event(Action<int> _event)
        => OnOtherPlayerExitedEvent += _event;
    public void Register_ReceiveStartGame_Event(Action _event)
        => OnReceiveStartGameEvent += _event;

    //room Available
    public void Register_RoomAvailable_Event(Action<uint> _event)
        => OnRoomAvailableEvent += _event;
    public void Register_RoomExited_Event(Action _event)
        => OnRoomExitedEvent += _event;

    // chat
    public void Register_ReceiveMessage_Event(Action<int, string, string> _event)
        => OnReceiveMessageEvent += _event;
    public void Register_ReceiveHistoricalMessages_Event(Action<ChatRoom> _event)
        => OnReceiveHistoricalMessagesEvent += _event;
    #endregion

    #region Unregister
    //common
    public void Unregister_CreateRoom_Event(Action _event)
        => OnCreateRoomEvent -= _event;
    public void Unregister_JoinRoom_Event(Action<uint> _event)
        => OnJoinRoomEvent -= _event;
    public void Unregister_StartGame_Event(Action<uint> _event)
        => OnStartGameEvent -= _event;

    // net
    public void Unregister_NewPlayerJoined_Event(Action<OnNewPlayerJoinedData> _event)
        => OnNewPlayerJoinedEvent -= _event;
    public void Unregister_RoomCreated_Event(Action<uint, int> _event)
        => OnRoomCreatedEvent -= _event;
    public void Unregister_RoomJoined_Event(Action<OnRoomJoinedData> _event)
        => OnRoomJoinedEvent -= _event;
    public void Unregister_OnOtherPlayerExited_Event(Action<int> _event)
        => OnOtherPlayerExitedEvent -= _event;
    public void Unregister_ReceiveStartGame_Event(Action _event)
        => OnReceiveStartGameEvent -= _event;

    //room Available
    public void Unregister_RoomAvailable_Event(Action<uint> _event)
        => OnRoomAvailableEvent -= _event;

    public void Unregister_RoomExited_Event(Action _event)
        => OnRoomExitedEvent -= _event;

    // chat
    public void Unregister_ReceiveMessage_Event(Action<int, string, string> _event)
        => OnReceiveMessageEvent -= _event;
    public void Unregister_ReceiveHistoricalMessages_Event(Action<ChatRoom> _event)
        => OnReceiveHistoricalMessagesEvent -= _event;
    #endregion

    #endregion

    #region Common
    public void SetJoinRoomID(uint id)
        => joinRoomID = id;
    public void ChangeRoomUILayout(RoomOptionType option)
        => OptionList.SetOptionActive(option, true);
    #endregion
}
