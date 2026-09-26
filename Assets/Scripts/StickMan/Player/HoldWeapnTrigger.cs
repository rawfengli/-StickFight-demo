using System.Collections;
using System.Collections.Generic;
using Unity.IO.LowLevel.Unsafe;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class HoldWeaponTrigger : MonoBehaviour
{
    public Fight fight;
#if UNITY_EDITOR || UNITY_SERVER

    private void Hold(Collider2D collision)
    {
        if (fight.playerNetManager.IsClientPlayer)
            return;

        if (collision.gameObject.layer != (int)GameLayer.Weapon)
            return;
        if (fight.holdWeapon)
            return;
        Weapon weapon = collision.GetComponent<Weapon>();
        if (weapon.beHold)
            return;
        if(fight.HoldWeapon(weapon, weapon.localHoldPos))
        {
            weapon.OnHolded(fight);
        }

    }
    private void OnTriggerEnter2D(Collider2D collision)
        => Hold(collision);
    private void OnTriggerStay2D(Collider2D collision)
        => Hold(collision);
#endif
}
