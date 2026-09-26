using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CreateRoomMenuButton : MenuButton
{
    protected override void OnEnable()
    {
        base.OnEnable();
        OnReleased += ChangeHomeLayout;
    }
    protected override void OnDisable()
    {
        base.OnDisable();
        OnReleased -= ChangeHomeLayout;
    }

    private void ChangeHomeLayout(WRButton _)
    {
        HomeButtonManager.Instance.SetOptionActive((int)HomeOptionIndex.Room, true);
        RoomEventManager.Instance.ChangeRoomUILayout(RoomOptionType.CreateRoom);
    }
}
