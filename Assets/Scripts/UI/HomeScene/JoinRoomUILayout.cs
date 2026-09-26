using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;

public class JoinRoomUILayout : MonoBehaviour
{
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TextMeshProUGUI RoomIDText;
    [SerializeField] private GameObject layout;
    [SerializeField] private WRButton confirmButton;
    [SerializeField] private TextMeshProUGUI text;

    #region Common
    //此处的start是为了保证注册的顺序正确，
    //不过[DefaultExecutionOrder(-1)]后这些就不需要了
    private void Awake()
    {
        confirmButton.gameObject.SetActive(false);
        connectStatus = false;
    }
    private void OnEnable()
    {
        RoomEventManager.Instance.Register_CreateRoom_Event(CreatingRoomUI);

        RoomEventManager.Instance.Register_JoinRoom_Event(JoiningRoomUI);
        RoomEventManager.Instance.Register_RoomAvailable_Event(RoomAvailableUI);
        RoomEventManager.Instance.Register_RoomExited_Event(RoomExitedUI);

        RoomEventManager.Instance.Register_RoomJoined_Event(RoomJoinedUI);

        confirmButton.OnReleased += ClosePromptUI;
    }
    private void OnDisable()
    {
        RoomEventManager.Instance.Unregister_CreateRoom_Event(CreatingRoomUI);

        RoomEventManager.Instance.Unregister_JoinRoom_Event(JoiningRoomUI);
        RoomEventManager.Instance.Unregister_RoomAvailable_Event(RoomAvailableUI);
        RoomEventManager.Instance.Unregister_RoomExited_Event(RoomExitedUI);

        RoomEventManager.Instance.Unregister_RoomJoined_Event(RoomJoinedUI);
        
        confirmButton.OnReleased -= ClosePromptUI;
    }
    public void OnEndEditRoomID()
    {
        try
        {
            string text = inputField.text;
            uint ID = uint.Parse(text);
            if(NetRoomManager.IsRoomIDVaild(ID))
            {
                RoomEventManager.Instance.SetJoinRoomID(ID);
                RoomIDText.text = ID.ToString();
            }
            else
            {
                RoomIDText.text = "非法的房间号，房间号由8位数字组成且房间号开头不得有0";
            }
        }
        catch
        {
            Debug.LogWarning("Invaild Room ID");
            RoomIDText.text = "非法的房间号";
        }
        inputField.text = string.Empty;
    }
    private bool connectStatus;
    private void Update()
    {
        UpdateConnectStatus();
    }
    private void UpdateConnectStatus()
    {
        if(NetClient.ready == false)
        {
            DuringDisconnection();
        }
        else if(connectStatus == false)
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

    public void ClosePromptUI(WRButton _)
    {
        layout.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        text.text = "如果你看到了这行字，那么说明这是个bug";
    }

    private void CreatingRoomUI()
    {
        layout.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(false);
        text.text = "已经正在创建房间";
    }
    private void JoiningRoomUI(uint roomID)
    {
        layout.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(false);
        text.text = "加入房间中，房间号:" + roomID.ToString();
    }
    private void RoomAvailableUI(uint roomID)
    {
        layout.gameObject.SetActive(true);
        confirmButton.gameObject.SetActive(false);
        text.text = "已存在一个房间";
    }
    private void RoomJoinedUI(OnRoomJoinedData data)
    {
        switch (data.status)
        {
            case JoinedRoomStatus.Succeeded:
                break;

            case JoinedRoomStatus.Full:
                layout.gameObject.SetActive(true);
                confirmButton.gameObject.SetActive(false);
                text.text = "加入房间失败，房间人数已满";
                break;

            case JoinedRoomStatus.RoomNotExist:
                layout.gameObject.SetActive(true);
                confirmButton.gameObject.SetActive(true);
                text.text = "加入房间失败，房间不存在";
                break;

            case JoinedRoomStatus.Unkown:
                layout.gameObject.SetActive(true);
                confirmButton.gameObject.SetActive(true);
                text.text = "加入房间失败，未知错误";
                break;
        }
    }
    private void RoomExitedUI()
    {
        layout.gameObject.SetActive(false);
        confirmButton.gameObject.SetActive(false);
        text.text = "如果你看到了这行字，那么说明这是个bug";
    }
}
