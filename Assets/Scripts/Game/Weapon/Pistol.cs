using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class Pistol : Weapon
{
    [Header("Audio")]
    public AudioClip shotClip;
    [Header("Object")]
    [SerializeField] private GameObject bulletFire;

    [Header("Weapon Data")]
    [SerializeField] private float hitForce;

    [SerializeField] private float recoilOnHandValue;
    [SerializeField] private float recoilOnBodyValue;
    [Space(40)]
    [SerializeField] private int damage;
    [SerializeField] private float speed;
    [SerializeField] private float lifeTime;
    [SerializeField] private float useCD;
    [Header("Owner")]
    [SerializeField] private Fight fight;
    private float usePassedTime;

    private Rigidbody2D _rigidbody;
    protected override void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
    }
    public void OnEnable()
    {
        bulletFire.SetActive(false);
        isOff = false;
        beHold = false;
    }

    public void OnDisable() { }
    public void Update()
    {
        usePassedTime += Time.deltaTime;
    }

    public override bool UseWeapon(Vector2 dir)
    {
#if UNITY_EDITOR || UNITY_SERVER
        return ServerUseWeapon(dir);
#else
        return false;
#endif
    }
    public override void OnHolded(Fight fight)
    {
#if UNITY_EDITOR || UNITY_SERVER
        ServerOnHolded(fight);
#endif
    }
    public override void ReceiveUseWeapon()
    {
#if UNITY_EDITOR || !UNITY_SERVER
        ClientReceiveUseWeapon();
#endif
    }
    public override void OffWeapon()
    {
#if UNITY_EDITOR || UNITY_SERVER
        ServerOffWeapon();
#endif
    }


#if UNITY_EDITOR || !UNITY_SERVER

    public void ClientReceiveUseWeapon()
    {
        PlayShotAudioClip();

        if (bulletFireCoroutine != null)
            StopCoroutine(bulletFireCoroutine);
        bulletFireCoroutine = StartCoroutine(BulletFireCoroutine());
    }
    public void PlayShotAudioClip()
    {
        audioSource?.PlayOneShot(shotClip);
    }
    private Coroutine bulletFireCoroutine;
    private float GUN_FIRE_LIFE_TIME = 0.04f;
    IEnumerator BulletFireCoroutine()
    {
        float t = 0;
        bulletFire.SetActive(true);
        while(t < GUN_FIRE_LIFE_TIME)
        {
            t += Time.deltaTime;
            yield return null;
        }
        bulletFire.SetActive(false);
    }
#endif

#if UNITY_EDITOR || UNITY_SERVER
    public void ServerOffWeapon()
    {
        GetComponent<Collider2D>().enabled = false;
        weaponCollider.GetComponent<Collider2D>().enabled = false;

        isOff = true;
        _rigidbody.gravityScale = 1;
        _rigidbody.AddForce(Vector2.up * 10f * _rigidbody.mass, ForceMode2D.Impulse);
        StartCoroutine(DestoryWeaponCoroutine());
    }
    private IEnumerator DestoryWeaponCoroutine()
    {
        float nowTime = 0f;
        while(nowTime < 5f)
        {
            nowTime += Time.deltaTime;
            yield return null;
        }
        DestoryWeaponHandle?.Invoke(gameObject);
    }
    public void ServerOnHolded(Fight fight)
    {
        beHold = true;

        GetComponent<Collider2D>().enabled = false;
        this.fight = fight;
        _rigidbody.gravityScale = 0;
        _rigidbody.velocity = Vector2.zero;
        usePassedTime = 100f;
    }
    public bool ServerUseWeapon(Vector2 dir)
    {
        if (usePassedTime < useCD)
            return false;

        usePassedTime = 0;

        PistolBullet bullet = BulletsManager.Instance.Get<PistolBullet>();

        Vector2 shotPos = transform.position + transform.rotation * localShotPos;
        bullet.transform.position = shotPos;
        bullet.transform.rotation = transform.rotation;

        bullet.SetValue(fight, damage, speed, lifeTime, hitForce, dir);

        fight.UseRecoil(-dir, recoilOnHandValue, recoilOnBodyValue);

        BulletsManager.Instance.NetworkSpawnBullet(fight.roomID, bullet.gameObject);

        return true;
    }
#endif
}
