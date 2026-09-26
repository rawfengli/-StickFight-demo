using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
[DefaultExecutionOrder(-1)]
public class RoomCommonUIManager : MonoBehaviour
{
    [Header("Players")]
    [SerializeField] private List<PlayerInRoom> list = new();
    public TextMeshProUGUI roomIDText;
    public void Awake()
    {
        list.Insert(0, null);
    }

    private void OnEnable()
    {
        RoomEventManager.Instance.Register_RoomCreated_Event(OnRoomCreated);
        RoomEventManager.Instance.Register_RoomJoined_Event(OnRoomJoined);
        RoomEventManager.Instance.Register_NewPlayerJoined_Event(OnNewPlayerJoined);
        RoomEventManager.Instance.Register_ReceiveStartGame_Event(OnGameStarted);
    }
    private void OnDisable()
    {
        RoomEventManager.Instance.Unregister_RoomCreated_Event(OnRoomCreated);
        RoomEventManager.Instance.Unregister_RoomJoined_Event(OnRoomJoined);
        RoomEventManager.Instance.Unregister_NewPlayerJoined_Event(OnNewPlayerJoined);
        RoomEventManager.Instance.Unregister_ReceiveStartGame_Event(OnGameStarted);
    }
    private void OnRoomCreated(uint roomID, int playerIndex)
    {
        roomIDText.text = roomID.ToString();

        RoomEventManager.Instance.ChangeRoomUILayout(RoomOptionType.AvailableRoom);

        for (int i = 1; i <= 4; i++)
        {
            list[i].UpdateInfo(-1, string.Empty);
        }
        list[GameRoomData.room.selfIndex].UpdateInfo(PlayerInfo.avatarIndex, PlayerInfo.playerName);
    }
    private void OnNewPlayerJoined(OnNewPlayerJoinedData data)
    {
        list[data.playerIndex].UpdateInfo(data.avatarIndex, data.playerName);
    }
    private void OnRoomJoined(OnRoomJoinedData data)
    {
        if (data.status == JoinedRoomStatus.Succeeded)
        {
            roomIDText.text = GameRoomData.room.roomID.ToString();

            RoomEventManager.Instance.ChangeRoomUILayout(RoomOptionType.AvailableRoom);

            for (int i = 1; i <= 4; i++)
            {
                list[i].UpdateInfo(
                    data.otherPlayerAvatarIndexs[i],
                    data.otherPlayerNames[i]);
            }
        }
    }
    /// <summary>
    /// 没时间写roomexit了 
    /// </summary>
    private void OnRoomExit()
    {
        for (int i = 1; i <= 4; i++)
        {
            list[i].UpdateInfo(-1, string.Empty);
        }
    }

    private void OnGameStarted()
    {
        ScreenTransitionCanvas.Instance.ScreenTransition(
            ScreenTransitionHandle.BlackScreenFadeInAndOut,
            1f, 0.6f, null,
            () => LoadingScreenCanvas.Instance.Load(LoadingScreenCanvas.SCENE_INGAME_KEY),
            () => StartCoroutine(AddressablesDriver.LoadScene(LoadingScreenCanvas.INGAME_SCENE_NAME))
        );
    }
}
