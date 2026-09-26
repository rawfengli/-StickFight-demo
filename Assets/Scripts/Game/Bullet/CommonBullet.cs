using System.Collections;
using UnityEngine;
public abstract class CommonBullet<T> : Bullet
    where T : CommonBullet<T>
{
    public LayerMask layerMask;

    private Fight owner;
    private float onHitForce;

    private int damage;
    private float speed;
    private float lifeTime = 100f;
    private Vector2 direction;

    public T instance => (T)this;

    protected TrailRenderer trail;

    Coroutine lifeCoroutine;
    Coroutine moveCoroutine;
    public void SetValue(
        Fight owner,
        int damage,
        float speed,
        float lifeTime,
        float onHitForce,
        Vector2 direction)
    {
        this.onHitForce = onHitForce;
        this.owner = owner;
        this.damage = damage;
        this.speed = speed;
        this.lifeTime = lifeTime;
        this.direction = direction;
        this.direction = this.direction.normalized;
    }
    protected virtual void Awake()
    {
        this.trail = GetComponent<TrailRenderer>();
    }
    protected override void Init() { }
    protected override void OnDisable()
    {
        if (NetServer.active == false || NetClient.active == true)
        {
            trail.emitting = true;
            trail.Clear();
            return;
        }

        lifeTime = 100f;
    }
    protected override void OnEnable()
    {
        if (NetServer.active == false || NetClient.active == true)
        {
            trail.emitting = true;
            return;
        }

#if UNITY_EDITOR || UNITY_SERVER

        LastPosition = Vector3.zero;

        if (lifeCoroutine != null)
            StopCoroutine(lifeCoroutine);
        lifeCoroutine = StartCoroutine(LifeCoroutine());

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveCoroutine());
#endif

    }
    public override void OnBulletHitGround(Vector3 pos, Vector3 dir, int shotPlayerIndex)
    {
#if UNITY_EDITOR || !UNITY_SERVER
        ClientOnBulletHitGround(pos, dir, shotPlayerIndex);
#endif
    }
#if UNITY_EDITOR || !UNITY_SERVER
    //如果这个地方还要写更多逻辑，
    //就用action
    public void ClientOnBulletHitGround(Vector3 pos, Vector3 dir, int shotPlayerIndex)
    {
        BulletHitGroundParticles hitParticles = ParticlesSystemManager.Instance.Get<BulletHitGroundParticles>();
        hitParticles.gameObject.transform.position = pos;
        hitParticles.gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, -dir);

        hitParticles.SetColorMaterial(shotPlayerIndex);
    }
#endif

    private Vector3 LastPosition;
    private int InterpolationCount = 5;
    public void FixedUpdate()
    {
        if (NetServer.active == false || NetClient.active == true)
            return;
#if UNITY_EDITOR || UNITY_SERVER

        if (LastPosition != Vector3.zero)
        {
            for (int i = 1; i <= InterpolationCount; i++)
            {
                float t = (float)i / (InterpolationCount + 1);
                Vector3 positionInterpolation = Vector3.Lerp(LastPosition, gameObject.transform.position, t);
                if (BulletHitChecker(positionInterpolation))
                    return;
            }
        }
        BulletHitChecker(gameObject.transform.position);
        LastPosition = gameObject.transform.position;
#endif

    }
#if UNITY_EDITOR || UNITY_SERVER
    IEnumerator LifeCoroutine()
    {
        float _time = 0;
        while (_time < lifeTime)
        {
            _time += Time.deltaTime;
            yield return null;
        }

        BulletsManager.Instance.Return(instance);
        BulletsManager.Instance.NetworkUnspawnBullet(owner.roomID, instance.gameObject);
    }
    IEnumerator MoveCoroutine()
    {
        while (true)
        {
            Vector3 movement = new(direction.x, direction.y, 0.0f);
            movement = movement * speed * Time.deltaTime;
            gameObject.transform.position += movement;

            yield return null;
        }
    }
    #region Hit Checker
    private const int ITERATION_COUNT = 4;
    private const float RAY_DISTANCE = 0.6f;
    public bool BulletHitChecker(Vector3 position)
    {
        Vector2 dir = Vector2.up;
        for (int i = 0; i < ITERATION_COUNT; i++)
        {

            RaycastHit2D[] hits = Physics2D.RaycastAll(position, dir, RAY_DISTANCE, layerMask);
            foreach (var hit in hits)
            {
                Collider2D collider = hit.collider;
                if (collider.isTrigger)
                    continue;

                Vector2 normal = hit.normal;
                Vector2 hitPos = hit.point;
                if (collider.gameObject.layer == (int)GameLayer.StickMan)
                {
                    OnHitOtherPlayer(collider, hitPos);
                    return true;
                }

                if (collider.gameObject.layer == (int)GameLayer.Ground)
                {
                    OnHitGround(collider, hitPos);
                    return true;
                }
            }
            Vector2 Rotate(Vector2 a, Vector2 b)
            {
                Vector2 res;
                res.x = a.x * b.x - a.y * b.y;
                res.y = a.x * b.y + a.y * b.x;
                return res;
            }
            Vector2 rot = new Vector2(0, 1f);
            dir = Rotate(dir, rot);
        }
        return false;
    }
    public void OnHitGround(Collider2D collision, Vector2 hitPoint)
    {
        if (NetServer.active == false || NetClient.active == true)
            return;

        BulletsManager.Instance.Return(instance);
        BulletsManager.Instance.CmdNetworkOnBulletHit(owner.roomID, hitPoint, direction, HitInfo.Ground, this, 0, owner.playerIndex);
        //前后两句不能调换，两个是kcp传输，Unspawn后client端找不到相应的netid物体
        BulletsManager.Instance.NetworkUnspawnBullet(owner.roomID, instance.gameObject);
    }
    public void OnHitOtherPlayer(Collider2D collision, Vector2 hitPoint)
    {
        if (NetServer.active == false || NetClient.active == true)
            return;

        Fight player = collision.gameObject.GetComponent<StickBodyPart>().owner;

        if (player.playerIndex == owner.playerIndex)
            return;

        player.OnHit(onHitForce, damage, direction, hitPoint, collision.gameObject, HitType.BulletAttack);

        BulletsManager.Instance.Return(instance);

        BulletsManager.Instance.NetworkUnspawnBullet(owner.roomID, instance.gameObject);
    }
    #endregion
#endif

    #region Network Client Create
    public override GameObject Spawn(ObjectSpawnMessage msg)
    {
        T bullet = BulletsManager.Instance.Get<T>();
        return bullet.gameObject;
    }
    public override GameObject Unspawn(GameObject go)
    {
        if (go.TryGetComponent<T>(out T bullet))
        {
            BulletsManager.Instance.Return(bullet);
            return go;
        }
        else
            return null;
    }
    #endregion
}
