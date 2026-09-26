using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(0)]

public class Standing : MonoBehaviour
{
    private Rigidbody2D[] rigidbodies;

    [Header("Network")]
    [SerializeField] private NetStickManManager playerNetManager;
    public uint roomID => playerNetManager.roomID;
    private int playerIndex => playerNetManager.playerIndex;
    [Header("Stand")]
    [SerializeField] private Rigidbody2D Head;
    [SerializeField] private Rigidbody2D Hips;
    [SerializeField] private float DragValue;
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private float standForceScale;
    private PlayerData playerData;
    private bool isDraging = false;

    [Header("Gravity")]
    [SerializeField] private float gravityScale = 1f;
    [SerializeField] private AnimationCurve gravityIncrementalCurve;
    [SerializeField] private float gravity = 0;
    [SerializeField] private float maxGravity = 20f;
    private bool netDraging = false;
    private void Awake()
    {
        if (Head == null)
            Debug.LogError("Head Object is Null");
        playerNetManager = GetComponent<NetStickManManager>();

        playerData = GetComponent<PlayerData>();

        rigidbodies = GetComponentsInChildren<Rigidbody2D>();
    }
    private void Update()
    {
#if UNITY_EDITOR || !UNITY_SERVER
        DragInput();
#endif
    }
#if UNITY_EDITOR || !UNITY_SERVER
    private void DragInput()
    {
        if (Input.GetKey(KeyCode.DownArrow))
        {
            isDraging = true;
        }
    }
#endif
    private void FixedUpdate()
    {
        if (playerNetManager.IsClientPlayer)
        {
#if UNITY_EDITOR || !UNITY_SERVER
            if (playerNetManager.playerNetReady == false)
                return;

            if (GameRoomData.IsSelf(playerIndex) == false)
                return;

            if (isDraging)
            {
                playerNetManager.CmdDragBody();
            }
            isDraging = false;
#endif
        }
        else
        {
#if UNITY_EDITOR || UNITY_SERVER

            Fall();

            Stand();
            if (netDraging)
            {
                netDraging = false;
            }
            else
            {
                isDraging = false;
                playerData.isDraging = false;
            }
#endif
        }
    }
#if UNITY_EDITOR || UNITY_SERVER
    public void ResetGravity()
    {
        gravity = 0;
    }
    private void Fall()
    {
        if (playerData.isReseting)
            return;

        gravity += gravityScale * Time.fixedDeltaTime * gravityIncrementalCurve.Evaluate(playerData.OffGroundTime);
        gravity = Mathf.Clamp(gravity, 0f, maxGravity);
        if (playerData.isOnGround)
        {
            gravity = 0f;
        }

        foreach (Rigidbody2D rigidbody in rigidbodies)
        {
            rigidbody.AddForce(Vector3.down * gravity * rigidbody.mass, ForceMode2D.Force);
        }
    }
    private void Stand()
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return;

        if (isDraging)
            return;

        if (playerData.isOnGround)
        {
            float t = curve.Evaluate(playerData.shortestDistanceToHead);
            float force = t * standForceScale;
            Head.AddForce(force * Vector2.up * Head.mass, ForceMode2D.Force);
        }
    }
    public void Drag()
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return;

        netDraging = true;
        isDraging = true;
        playerData.isDraging = true;

        float force = DragValue;
        Hips.AddForce(force * Vector2.down * Head.mass, ForceMode2D.Force);
    }
#endif
}
