using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HandAttackTrigger : MonoBehaviour
{
    private HandAttackTriggerManager manager;

    private void Start()
    {
        gameObject.SetActive(false);
    }
    public void Init(HandAttackTriggerManager manager)
    {
        this.manager = manager;
    }
#if UNITY_EDITOR || UNITY_SERVER
    public void OnCollisionEnter2D(Collision2D collision)
    {
        if (NetServer.active == false || NetClient.active == true)
            return;

        if (collision.gameObject.layer == (int)GameLayer.StickMan)
            manager.InvokeAttackTrigger(collision);
    }
    public void OnCollisionStay2D(Collision2D collision)
    {
        if (NetServer.active == false || NetClient.active == true)
            return;

        if (collision.gameObject.layer == (int)GameLayer.StickMan)
            manager.InvokeAttackTrigger(collision);

    }
#endif
}
