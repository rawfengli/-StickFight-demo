using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CreateRoomUILayout : MonoBehaviour
{
    [SerializeField] private GameObject layout;
    [SerializeField] private TextMeshProUGUI text;

    #region Common
    private void OnEnable()
    {
        RoomEventManager.Instance.Register_JoinRoom_Event(JoiningRoomUI);

        RoomEventManager.Instance.Register_CreateRoom_Event(CreatingRoomUI);
        RoomEventManager.Instance.Register_RoomAvailable_Event(RoomAvailableUI);
        RoomEventManager.Instance.Register_RoomExited_Event(RoomExitedUI);
    }
    private void OnDisable()
    {
        RoomEventManager.Instance.Unregister_JoinRoom_Event(JoiningRoomUI);

        RoomEventManager.Instance.Unregister_CreateRoom_Event(CreatingRoomUI);
        RoomEventManager.Instance.Unregister_RoomAvailable_Event(RoomAvailableUI);
        RoomEventManager.Instance.Unregister_RoomExited_Event(RoomExitedUI);
    }
    private bool connectStatus;
    private void Update()
    {
        UpdateConnectStatus();
    }
    private void UpdateConnectStatus()
    {
        if (NetClient.ready == false)
        {
            DuringDisconnection();
        }
        else if (connectStatus == false)
        {
            OnConnected();
        }
        connectStatus = NetClient.ready;
    }
    private void DuringDisconnection()
    {
        layout.gameObject.SetActive(true);
        text.text = "未连接到服务器";
    }
    private void OnConnected()
    {
        layout.gameObject.SetActive(false);
        text.text = "如果你看到了这行字，那么说明这是个bug";
    }
    #endregion

    private void CreatingRoomUI()
    {
        layout.gameObject.SetActive(true);
        text.text = "创建房间中";
    }
    private void JoiningRoomUI(uint roomID)
    {
        layout.gameObject.SetActive(true);
        text.text = "已经正在加入房间";
    }
    private void RoomAvailableUI(uint roomID)
    {
        layout.gameObject.SetActive(true);
        text.text = "已存在一个房间";
    }
    private void RoomExitedUI()
    {
        layout.gameObject.SetActive(false);
        text.text = "如果你看到了这行字，那么说明这是个bug";
    }
}