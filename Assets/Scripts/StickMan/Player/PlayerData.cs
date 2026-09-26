using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1)]
public class PlayerData : MonoBehaviour
{
    [Header("Player Info")]
    [SerializeField] private int hp;
    public int MaxHp;
    public int Hp { get => hp; private set => hp = value; }

    public bool isAlive => Hp > 0;
    public bool isReseting = false;

    [Header("Running Time Data")]
    [SerializeField] private GroundChecker[] groundCheckers;
    public float jumpPassTime = 0f;
    public bool isOnGround;
    public bool isDraging = false;
    public bool isRunning = false;
    public bool allowToMove => !isDraging && isAlive && !isReseting;

    [SerializeField] private float offGroundTime = 0f;
    public float OffGroundTime
    {
        get => offGroundTime;
        private set => offGroundTime = value;
    }
    public float shortestDistanceToHead = 0f;
    public GameObject Head;
    public Vector2 HeadPos => Head.transform.position;

    private void Awake()
    {
        groundCheckers = GetComponentsInChildren<GroundChecker>();
    }
    private void FixedUpdate()
    {
        jumpPassTime += Time.fixedDeltaTime;
        isOnGround = false;
        shortestDistanceToHead = float.MaxValue;
        foreach (GroundChecker groundChecker in groundCheckers)
        {
            if (groundChecker.isGrounded)
            {
                float dis = Vector2.Distance(groundChecker.collisionPosition, HeadPos);
                shortestDistanceToHead = Mathf.Min(shortestDistanceToHead, dis);

                if (jumpPassTime > 0.2f)
                {
                    isOnGround |= true;
                }
            }
        }
    }
    private void Update()
    {
        if (!isOnGround)
            OffGroundTime += Time.deltaTime;
        else
            OffGroundTime = 0f;
    }
    #region Info
    //server
    public void ResetPlayerData()
    {
        hp = MaxHp;
    }
    public void DecreaseHP(int value)
    {
        hp -= value;
    }
    public void Die()
    {
        hp = -1;
    }
    /// <summary>
    /// 设置对象的生命值（HP）。
    /// 只能在client端调用
    /// </summary>
    public void SetHp(int value)
    {
        hp = value;
    }
    #endregion
}
