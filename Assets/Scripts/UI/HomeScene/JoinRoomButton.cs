using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JoinRoomButton : WRButton
{
    protected override void Update()
    {
        SetRoomID();
        base.Update();
    }
    private void SetRoomID()
    {
        RoomEventManager.Instance.SetJoinRoomID(0);
    }
}
