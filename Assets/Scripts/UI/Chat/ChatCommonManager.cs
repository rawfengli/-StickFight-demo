using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//这里就不做ui和逻辑的分离了
public class ChatCommonManager : MonoBehaviour
{
    [Header("Network")]
    public NetChatManager netChatManager;
    [Header("UI Object")]
    public Image coverUI;
    public TextMeshProUGUI coverText;
    [Header("Message Send And Receive")]
    public TextMeshProUGUI content;
    public TMP_InputField inputMessage;

    private bool connectStatus;
#if UNITY_EDITOR || !UNITY_SERVER
    private void OnEnable()
    {
        RoomEventManager.Instance.Register_RoomExited_Event(OnRoomExited);
        RoomEventManager.Instance.Register_RoomAvailable_Event(OnRoomAvailable);

        RoomEventManager.Instance.Register_ReceiveMessage_Event(AppendText);
        RoomEventManager.Instance.Register_ReceiveHistoricalMessages_Event(Reflush);

    }
    private void OnDisable()
    {
        RoomEventManager.Instance.Unregister_RoomExited_Event(OnRoomExited);
        RoomEventManager.Instance.Unregister_RoomAvailable_Event(OnRoomAvailable);

        RoomEventManager.Instance.Unregister_ReceiveMessage_Event(AppendText);
        RoomEventManager.Instance.Unregister_ReceiveHistoricalMessages_Event(Reflush);
    }
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
        coverUI.gameObject.SetActive(true);
        content.text = string.Empty;
        coverText.text = "未连接到服务器";
    }
    private void OnConnected()
    {
        coverUI.gameObject.SetActive(true);
        content.text = string.Empty;
        coverText.text = "未创建或加入房间";
    }
    private void OnRoomExited()
    {
        coverUI.gameObject.SetActive(true);
        content.text = string.Empty;
        coverText.text = "未创建或加入房间";
    }
    public void OnRoomAvailable(uint _)
    {
        coverUI.gameObject.SetActive(false);
        content.text = string.Empty;
        coverText.text = "如果你看到了这行字，那么说明这是个bug";
    }
    public void OnEndInputMessage()
    {
        string content = inputMessage.text;

        netChatManager.CmdSendMessage(content);
        
        inputMessage.text = string.Empty;
    }
    public void AppendText(int playerIndex, string playerName, string content)
    {
        string message = playerName;
        PlayerInfo.DrawTextWithPlayerColor(playerIndex, ref message);
        message = message + " : " + content;
        this.content.text += message;
        this.content.text += "\n";
    }
    public void Reflush(ChatRoom room)
    {
        this.content.text = string.Empty;

        int count = Math.Min(room.totalConversationCount, ChatRoom.MAX_CONVERSATION_COUNT);

        int index;

        if (room.totalConversationCount >= ChatRoom.MAX_CONVERSATION_COUNT)
            index = room.latestConversationIndex;
        else
            index = 0;

        
        for (int i = 0; i < count; i++)
        {

            int playerIndex = room.conversationLog[index].Item1;
            string playerName = room.conversationLog[index].Item2;
            string content = room.conversationLog[index].Item3;

            string message = playerName;

            PlayerInfo.DrawTextWithPlayerColor(playerIndex, ref message);

            message = message + " : " + content;

            this.content.text += message;
            this.content.text += "\n";

            index++;

            if (index >= ChatRoom.MAX_CONVERSATION_COUNT)
                index = 0;
        }
    }
#endif
}
