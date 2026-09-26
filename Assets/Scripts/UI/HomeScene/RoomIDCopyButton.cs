using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;

public class RoomIDCopyButton : CommonWRButton
{
    [SerializeField] private TextMeshProUGUI roomID; 
    protected override void OnEnable()
    {
        base.OnEnable();
        OnReleased += CopyRoomID;
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        OnReleased -= CopyRoomID;
    }
    private void CopyRoomID(WRButton _)
    {
        GUIUtility.systemCopyBuffer = roomID.text.ToString();
    }
}
