using System;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
public abstract class Weapon : MonoBehaviour
{
    public AudioSource audioSource;
    public int spawnedID;
    public bool beHold;
    public bool isOff;
    public string WeaponName;
    public Vector2 localShotPos;
    public Vector2 localHoldPos;
    public GameObject weaponCollider;
    public Action<GameObject> DestoryWeaponHandle;
    protected abstract void Awake();
    public abstract void OffWeapon();
    public abstract bool UseWeapon(Vector2 dir);
    public abstract void ReceiveUseWeapon();
    public abstract void OnHolded(Fight fight);
}

