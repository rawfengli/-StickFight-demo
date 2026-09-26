using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

[DefaultExecutionOrder(0)]
public class PlayerController : MonoBehaviour
{
    [Header("Network")]
    public NetStickManManager playerNetManager;
    public uint roomID => playerNetManager.roomID;
    private int playerIndex => playerNetManager.playerIndex;
    [Header("Common")]
    public PlayerData playerData;
    public Rigidbody2D[] rigidbodies;
    public Transform hips;
    public Rigidbody2D playerRigidbody => hips.GetComponent<Rigidbody2D>();
    public Vector3 HipsPostion => hips.position;
    public Fight fight;
    private float offGroundTimeCache;
    private void Awake()
    {
        playerNetManager = GetComponent<NetStickManManager>();
        fight = GetComponent<Fight>();
        playerData = GetComponent<PlayerData>();
        rigidbodies = GetComponentsInChildren<Rigidbody2D>();
        foreach(var rigidbody in rigidbodies)
        {
            if (rigidbody.TryGetComponent<StickBodyPart>(out StickBodyPart bodyPart))
            {
                bodyPart.owner = fight;
                bodyPart.data = playerData;
            }
        }

        for (int i = 0; i < rigidbodies.Count(); i++)
        {
            Collider2D colliderA = rigidbodies[i].GetComponent<Collider2D>();

            for (int j = i + 1; j < rigidbodies.Count(); j++)
            {
                Collider2D colliderB = rigidbodies[j].GetComponent<Collider2D>();

                Physics2D.IgnoreCollision(colliderA, colliderB);
            }
        }
    }
    private void OnEnable()
    {
        offGroundTimeCache = 0;

        leftLegGroundChecker.OnGroundedEvent += InvokeOnLegGroundedEvent;
        rightLegGroundChecker.OnGroundedEvent += InvokeOnLegGroundedEvent;

        RegisterOnGroundedEvent(StopLandingImpulse);
        RegisterOnGroundedEvent(ResetJumpedTime);
    }
    private void OnDisable()
    {
        leftLegGroundChecker.OnGroundedEvent -= InvokeOnLegGroundedEvent;
        rightLegGroundChecker.OnGroundedEvent -= InvokeOnLegGroundedEvent;
        
        UnregisterOnGroundedEvent(StopLandingImpulse);
        UnregisterOnGroundedEvent(ResetJumpedTime);
    }
    private void Update()
    {
#if UNITY_EDITOR || !UNITY_SERVER
        JumpInput();
#endif
    }
    private bool lastFrameIsAlive;
    private void FixedUpdate()
    {
        //client cmd
        if (playerNetManager.IsClientPlayer)
        {
#if UNITY_EDITOR || !UNITY_SERVER
            if (playerNetManager.playerNetReady == false)
                return;

            if (GameRoomData.IsSelf(playerIndex) == false)
                return;

            if (jumpPressed)
            {
                playerNetManager.CmdDoJump();
            }
            jumpPressed = false;
#endif
        }
        else
        {
#if UNITY_EDITOR || UNITY_SERVER
            //server logic

            if (lastFrameIsAlive && !playerData.isAlive)
            {
                InvokeOnPlayerDieEvent();
                //服务器段这边也要写一个单独的事件总线
                fight.OffWeapon();
            }

            lastFrameIsAlive = playerData.isAlive;
            if (playerData.isOnGround && offGroundTimeCache > 0.1f && jumpPressedTime > 0.2f)
            {
                InvokeOnPlayerGroundedEvent();
            }
            offGroundTimeCache = playerData.OffGroundTime;
            jumpPressedTime += Time.fixedDeltaTime;
#endif
        }
    }

    #region Jumped

    [Header("Jump")]
    [SerializeField] private float jumpPressedTime = 0;
    [SerializeField] private float sinceJumpedTime = 0;
    [SerializeField] private float jumpForce = 10f;
    [SerializeField] private bool jumpPressed = false;

#if UNITY_EDITOR || !UNITY_SERVER

    private void JumpInput()
    {
        if (NetClient.ready == false || NetServer.active)
            return;

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpPressed = true;
        }
    }
    public void ReceiveJump()
    {
        InvokeOnJumpedEvent();
    }
#endif
#if UNITY_EDITOR || UNITY_SERVER
    public bool Jump()
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return false;

        if (playerData.OffGroundTime < 0.2f && sinceJumpedTime > 0.4f)
        {
            DoJump();
            if (coroutine != null)
            {
                StopCoroutine(coroutine);
            }
            coroutine = StartCoroutine(JumpPassedTimeCoroutine());
            return true;
        }
        else
        {
            return false;
        }
    }
    private void DoJump()
    {
        jumpPressedTime = 0f;
        foreach (Rigidbody2D rigidbody in rigidbodies)
        {
            rigidbody.AddForce(Vector3.up * jumpForce * rigidbody.mass, ForceMode2D.Impulse);
        }
        InvokeOnJumpedEvent();
    }
    Coroutine coroutine;
    IEnumerator JumpPassedTimeCoroutine()
    {
        sinceJumpedTime = 0;
        while (sinceJumpedTime < 0.5f)
        {
            sinceJumpedTime += Time.fixedDeltaTime;
            yield return null;
        }

    }
#endif
    #region Enable/Disable gravity
#if UNITY_EDITOR || UNITY_SERVER
    private float[] gravityScaleCache = new float[0];
    public void DisableGravity()
    {
        if (gravityScaleCache.Count() == 0)
            gravityScaleCache = new float[rigidbodies.Count()];
        int i = 0;
        foreach (var rigidbody in rigidbodies)
        {
            gravityScaleCache[i] = rigidbody.gravityScale;
            i++;
            rigidbody.gravityScale = 0;
        }
    }
    public void EnableGravity()
    {
        int i = 0;
        foreach (var rigidbody in rigidbodies)
        {
            rigidbody.gravityScale = gravityScaleCache[i];
            i++;
        }
    }

#endif


    #endregion

    #endregion

    #region Event, Event are shared
    [Header("Event Invoker")]
    [SerializeField] private GroundChecker leftLegGroundChecker;
    [SerializeField] private GroundChecker rightLegGroundChecker;
    public Action OnJumpedEvent;
    public Action OnPlayerGroundedEvent;
    public Action<GroundChecker> OnLegGroundedEvent;
    public Action OnPlayerDieEvent;

    private void InvokeOnJumpedEvent()
        => OnJumpedEvent?.Invoke();
    private void InvokeOnPlayerGroundedEvent()
        => OnPlayerGroundedEvent?.Invoke();

    private void InvokeOnLegGroundedEvent(GroundChecker groundChecker)
        => OnLegGroundedEvent?.Invoke(groundChecker);

    private void InvokeOnPlayerDieEvent()
        => OnPlayerDieEvent?.Invoke();
    public void RegisterOnGroundedEvent(Action _event)
        => OnPlayerGroundedEvent += _event;
    public void UnregisterOnGroundedEvent(Action _event)
        => OnPlayerGroundedEvent -= _event;

    public void RegisterOnLegGroundedEvent(Action<GroundChecker> _event)
        => OnLegGroundedEvent += _event;
    public void UnregisterOnLegGroundedEvent(Action<GroundChecker> _event)
        => OnLegGroundedEvent -= _event;

    public void RegisterOnJumpedEvent(Action _event)
    => OnJumpedEvent += _event;
    public void UnregisterOnJumpedEvent(Action _event)
        => OnJumpedEvent -= _event;

    public void RegisterOnPlayerDieEvent(Action _event)
        => OnPlayerDieEvent += _event;
    public void UnregisterOnPlayerDieEvent(Action _event)
        => OnPlayerDieEvent -= _event;

    #endregion
    #region Client And Server 
    private void StopLandingImpulse()
    {
        foreach (var rigidbody in rigidbodies)
        {
            Vector2 velocity = rigidbody.velocity;
            velocity.y *= 0.1f;
            rigidbody.velocity = velocity;
        }
    }
    private void ResetJumpedTime()
        => sinceJumpedTime = 1f;
    #endregion
}
