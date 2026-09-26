using System.Collections;
using UnityEngine;

public class Fight : MonoBehaviour
{
    private Weapon weapon;
    private Vector2 localHoldPos;
    [Header("Network")]
    [SerializeField] public PlayerData playerData;
    [SerializeField] public NetStickManManager playerNetManager;
    public uint roomID => playerNetManager.roomID;
    public int playerIndex => playerNetManager.playerIndex;//for server

    [Header("Object")]
    [SerializeField] private Transform Hips;
    [SerializeField] private Transform Chest;
    //right 
    [Space(40)]
    [SerializeField] private Transform UpperRightArm;
    [SerializeField] private Transform LowerRightArm;
    [SerializeField] private Transform rightHand;
    //left 
    [Space(40)]
    [SerializeField] private Transform UpperLeftArm;
    [SerializeField] private Transform LowerLeftArm;
    [SerializeField] private Transform leftHand;
    public bool holdWeapon => weapon != null;
    [Header("Hand Force")]
    [SerializeField] private float lineForce;
    private Rigidbody2D[] rigidbodies;
    private Rigidbody2D lraRigidbody;//lower right arm
    private Rigidbody2D uraRigidbody;
    [Header("Hand Attack")]
    private Rigidbody2D rightHandRigidbody;
    private Rigidbody2D leftHandRigidbody;

    [SerializeField] private HandAttackTriggerManager rightHandTrigger;
    [SerializeField] private HandAttackTriggerManager leftHandTrigger;

    [Space(40)]
    [SerializeField] public int handAttackDamage;
    [SerializeField] public float handAttackOnHitForce;
    [SerializeField] private AnimationCurve handAttackForceCurve;
    [SerializeField] private float handAttackForceScale;
    [Space(40)]
    [SerializeField] private float handAttackCD = 0.3f;
    [SerializeField] private float handAttackDuring = 0.2f;
    private int attackHandIndex = 0;//0 - left, 1 - right
    private float handAttackPassTime;
    private void Awake()
    {
        playerNetManager = GetComponent<NetStickManManager>();

        playerData = GetComponent<PlayerData>();
        lraRigidbody = LowerRightArm.GetComponent<Rigidbody2D>();
        uraRigidbody = UpperRightArm.GetComponent<Rigidbody2D>();
        rightHandRigidbody = rightHand.GetComponent<Rigidbody2D>();
        leftHandRigidbody = leftHand.GetComponent<Rigidbody2D>();

        rigidbodies = GetComponentsInChildren<Rigidbody2D>();

        lraGravityCache = lraRigidbody.gravityScale;
        uraGravityCache = uraRigidbody.gravityScale;
        rightHandGravityCache = rightHandRigidbody.gravityScale;
    }
    private void OnEnable()
    {
        handAttackPassTime = 0;
        UsedWeapon = false;

    }
    private void OnDisable()
    {
        UsedWeapon = false;
    }
    private bool UsedWeapon = false;
    private void Update()
    {
        if (NetClient.ready == false || NetServer.active)
            return;

        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            UsedWeapon = true;
        }
    }
    private void FixedUpdate()
    {
        if (playerNetManager.IsClientPlayer)
        {
#if UNITY_EDITOR || !UNITY_SERVER

            if (playerNetManager.playerNetReady == false)
                return;

            if (GameRoomData.IsSelf(playerIndex) == false)
                return;

            Vector3 mousePos = Input.mousePosition;
            mousePos = Camera.main.ScreenToWorldPoint(mousePos);

            playerNetManager.CmdRotateAimer(mousePos);

            if (UsedWeapon)
            {
                playerNetManager.CmdUseWeapon(mousePos);
            }
            UsedWeapon = false;

#endif
        }
        else
        {
#if UNITY_EDITOR || UNITY_SERVER

            HoldingWeapon();
            handAttackPassTime += Time.fixedDeltaTime;
#endif
        }
    }
    #region Attack
#if UNITY_EDITOR || UNITY_SERVER
    public void UseRecoil(Vector2 recoilDir, float recoilOnHandValue, float recoilOnBodyValue)
    {
        rightHandRigidbody.AddForce(recoilDir * recoilOnHandValue * rightHandRigidbody.mass, ForceMode2D.Impulse);
        foreach (var rigidbody in rigidbodies)
        {
            rigidbody.AddForce(recoilDir * recoilOnBodyValue * rigidbody.mass, ForceMode2D.Impulse);
        }
    }
    //0 failed to use, 1 use weapon, 2 hand
    public bool UseWeapon(Vector2 mousePos)
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return false;

        Vector2 dir = weapon.transform.rotation * Vector2.right;
        return weapon.UseWeapon(dir);
    }
    public bool HandAttack(Vector3 mousePos, out Vector3 handStartPos, out Vector3 dir)
    {
        handStartPos = Vector3.zero;
        dir = Vector3.up;

        if (playerData.isAlive == false || playerData.isReseting)
            return false;

        if (handAttackPassTime > handAttackCD)
        {
            attackHandIndex ^= 1;
            handAttackPassTime = 0f;

            if (attackHandIndex == 0)
            {
                handStartPos = leftHand.position;
                dir = (mousePos - leftHand.position).normalized;

                StartCoroutine(HandAttackCoroutine(mousePos, attackHandIndex));
                return true;
            }
            else if (attackHandIndex == 1)
            {
                handStartPos = rightHand.position;
                dir = (mousePos - rightHand.position).normalized;

                StartCoroutine(HandAttackCoroutine(mousePos, attackHandIndex));
                return true;
            }

            return false;
        }
        else
        {
            return false;
        }

    }
    private WaitForFixedUpdate waitForFixedUpdate = new();
    private IEnumerator HandAttackCoroutine(Vector3 target, int handIndex)
    {
        float nowTime = 0;

        if (handIndex == 1)
        {
            Vector2 attackDir = target - rightHand.position;

            rightHandTrigger.ResetState(attackDir);
            rightHandTrigger.EnableAttackTrigger(true);
        }
        else
        {
            Vector2 attackDir = target - leftHand.position;

            leftHandTrigger.ResetState(attackDir);
            leftHandTrigger.EnableAttackTrigger(true);
        }
        while (nowTime < handAttackDuring)
        {
            float t = nowTime / handAttackDuring;
            float force = handAttackForceCurve.Evaluate(t);

            if (handIndex == 1)
            {
                Vector2 attackDir = target - rightHand.position;
                Vector2 forceVec = attackDir * force * handAttackForceScale * rightHandRigidbody.mass;
                rightHandRigidbody.AddForce(forceVec, ForceMode2D.Force);

                float handSpeed = rightHandRigidbody.velocity.magnitude;
                handSpeed = Mathf.Min(handSpeed, 60f);

                rightHandRigidbody.velocity = handSpeed * rightHandRigidbody.velocity.normalized;
                rightHandRigidbody.angularVelocity *= 0.0f;
                UpperRightArm.GetComponent<Rigidbody2D>().angularVelocity *= 0.0f;
                LowerRightArm.GetComponent<Rigidbody2D>().angularVelocity *= 0.0f;
            }
            else
            {
                Vector2 attackDir = target - leftHand.position;
                Vector2 forceVec = attackDir * force * handAttackForceScale * rightHandRigidbody.mass;
                leftHandRigidbody.AddForce(forceVec, ForceMode2D.Force);

                float handSpeed = leftHandRigidbody.velocity.magnitude;
                handSpeed = Mathf.Min(handSpeed, 60f);

                leftHandRigidbody.velocity = handSpeed * leftHandRigidbody.velocity.normalized;
                leftHandRigidbody.angularVelocity *= 0.0f;
                UpperLeftArm.GetComponent<Rigidbody2D>().angularVelocity *= 0.0f;
                LowerLeftArm.GetComponent<Rigidbody2D>().angularVelocity *= 0.0f;
            }

            nowTime += Time.fixedDeltaTime;
            yield return waitForFixedUpdate;
        }

        if (handIndex == 1)
        {
            rightHandTrigger.EnableAttackTrigger(false);
        }
        else
        {
            leftHandTrigger.EnableAttackTrigger(false);
        }
    }
#endif

#if UNITY_EDITOR || !UNITY_SERVER
    public void ReceiveUseWeapon()
    {
        weapon?.ReceiveUseWeapon();
    }
    public void ReceiveHandAttack(Vector2 startHandPos, Vector2 dir)
    {
        HandAttackParticles particles = ParticlesSystemManager.Instance.Get<HandAttackParticles>();
        particles.SetValue(startHandPos, dir);
    }
#endif

    #endregion

    #region On Hit
#if UNITY_EDITOR || UNITY_SERVER

    public void OnHit(float force, int damage, Vector3 damageDir, Vector3 hitPos, GameObject bodyPart, HitType hitType)
    {
        playerData.DecreaseHP(damage);

        Rigidbody2D rig2D = bodyPart.GetComponent<Rigidbody2D>();
        rig2D.AddForce(force * damageDir, ForceMode2D.Impulse);

        playerNetManager.CmdReceiveHit(roomID, damageDir, hitPos, hitType);
    }
#endif
#if UNITY_EDITOR || !UNITY_SERVER

    public void ReceiveHit(Vector3 damageDir, Vector3 hitPos, HitType hitType)
    {
        OnPlayerHitData data = new();
        data.playerIndex = playerIndex;
        data.damageDir = damageDir;
        data.hitPos = hitPos;
        data.hitType = hitType;

        InGameEventBus.Instance.InvokeOnPlayerHit(data);
    }
#endif
    #endregion

    #region HoldWeapon
    private float lraGravityCache;
    private float uraGravityCache;
    private float rightHandGravityCache;
    public void HoldWeaponClient(Weapon weapon)
    {
        this.weapon = weapon;
    }
#if UNITY_EDITOR || UNITY_SERVER

    public bool HoldWeapon(Weapon weapon, Vector2 localHoldPos)
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return false;

        NetIdentity identity = weapon.GetComponent<NetIdentity>();
        playerNetManager.CmdHoldWeapon(playerNetManager.roomID, identity.netID);

        weapon.transform.SetParent(rightHand);
        this.weapon = weapon;
        this.localHoldPos = localHoldPos;
        return true;
    }
    public void OffWeapon()
    {
        if(holdWeapon)
        {
            weapon.OffWeapon();
            weapon = null;
        }
    }
    public void KeepTransform()
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return;

        weapon.transform.localPosition = localHoldPos;
        weapon.transform.localRotation = Quaternion.Euler(0, 0, 90);
    }
    public void HoldingWeapon()
    {
        if (!holdWeapon || !playerData.isAlive || playerData.isReseting)
        {
            lraRigidbody.gravityScale = lraGravityCache;
            uraRigidbody.gravityScale = uraGravityCache;
            rightHandRigidbody.gravityScale = rightHandGravityCache;
            return;
        }

        lraRigidbody.gravityScale = 0;
        uraRigidbody.gravityScale = 0;
        rightHandRigidbody.gravityScale = 0;

        KeepTransform();

    }
    public void RotateAim(Vector3 mousePos)
    {
        if (!holdWeapon || !playerData.isAlive || playerData.isReseting)
        {
            return;
        }

        Vector2 targetDirection = (mousePos - Chest.position).normalized;
        if (targetDirection.sqrMagnitude < 0.0001f)
            return;

        if (targetDirection.sqrMagnitude > 0.0001f)
        {
            Vector2 nowDirction;

            nowDirction = (UpperRightArm.position - Chest.position).normalized;

            rightHandRigidbody.AddForce(targetDirection * lineForce * rightHandRigidbody.mass, ForceMode2D.Force);
        }
    }
#endif
    #endregion
    public void IgnorePlayerCollision(Collider2D collider)
    {
        foreach(var rigidbody in rigidbodies)
        {
            Physics2D.IgnoreCollision(collider, rigidbody.GetComponent<Collider2D>());
        }
    }
}
