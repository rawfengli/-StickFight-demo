using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

[DefaultExecutionOrder(1)]
public class HandAttackTriggerManager : MonoBehaviour
{
    [NonSerialized] public Vector2 attackDir;
    public Fight owner;
    public HandAttackTrigger lowerArmTrigger;
    public HandAttackTrigger handTrigger;
    private bool[] AttackPlayers = new bool[5]
    {
        false, false, false, false, false
    };
    private bool enableAttackTrigger = false;

    private void Awake()
    {
        lowerArmTrigger.Init(this);
        handTrigger.Init(this);
    }
    private void OnEnable()
    {
        enableAttackTrigger = false;
        lowerArmTrigger.gameObject.SetActive(false);
        handTrigger.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        enableAttackTrigger = false;
    }
#if UNITY_EDITOR || UNITY_SERVER
    public void EnableAttackTrigger(bool status)
    {
        enableAttackTrigger = status;
        lowerArmTrigger.gameObject.SetActive(status);
        handTrigger.gameObject.SetActive(status);
        if(status == true)
        {
            owner.IgnorePlayerCollision(lowerArmTrigger.GetComponent<Collider2D>());
            owner.IgnorePlayerCollision(handTrigger.GetComponent<Collider2D>());
        }

    }
    public void ResetState(Vector2 attackDir)
    {
        this.attackDir = attackDir;
        for (int i = 0; i < AttackPlayers.Length; i++)
        {
            AttackPlayers[i] = false;
        }
    }
    public void InvokeAttackTrigger(Collision2D collision)
    {
        if (!enableAttackTrigger)
            return;
        Fight player = collision.gameObject.GetComponent<StickBodyPart>().owner;
        Vector3 pos = collision.GetContact(0).point;

        if (AttackPlayers[player.playerIndex])
            return;

        for (int i = 0; i < AttackPlayers.Length; i++)
        {
            if (i == owner.playerIndex)
                continue;
            if (i != player.playerIndex)
                continue;
            AttackPlayers[player.playerIndex] = true;
            player.OnHit(owner.handAttackOnHitForce, owner.handAttackDamage, attackDir, pos, collision.gameObject, HitType.HandAttack);
            lowerArmTrigger.gameObject.SetActive(false);
            handTrigger.gameObject.SetActive(false);
            return;
        }
    }
#endif

}
