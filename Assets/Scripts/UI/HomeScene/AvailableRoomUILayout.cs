using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AvailableRoomUILayout: MonoBehaviour
{

    [Header("Cover UI")]
    [SerializeField] private GameObject coverLayout;
    [SerializeField] private TextMeshProUGUI text;

    private void OnEnable()
    {
        RoomEventManager.Instance.Register_RoomAvailable_Event(RoomAvailableUI);
        RoomEventManager.Instance.Register_RoomExited_Event(RoomExited);
    }
    private void OnDisable()
    {
        RoomEventManager.Instance.Unregister_RoomAvailable_Event(RoomAvailableUI);
        RoomEventManager.Instance.Unregister_RoomExited_Event(RoomExited);
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
        coverLayout.gameObject.SetActive(true);
        text.text = "未连接到服务器";
    }
    private void OnConnected()
    {
        coverLayout.gameObject.SetActive(true);
        text.text = "未创建或加入房间";
    }

    private void RoomAvailableUI(uint roomID)
    {
        coverLayout.gameObject.SetActive(false);
        text.text = "如果你看到了这行字，那么说明这是个bug";
    }
    private void RoomExited()
    {
        coverLayout.gameObject.SetActive(true);
        text.text = "未创建或加入房间";
    }
}
