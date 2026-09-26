using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(0)]

public class Movement : MonoBehaviour
{
    private enum FacingDirection
    {
        Left,
        Right
    }
    private Rigidbody2D[] rigidbodies;
    private float nowStepTime;
    private PlayerData playerData;

    [Header("Network")]
    [SerializeField] private NetStickManManager playerNetManager;
    public uint roomID => playerNetManager.roomID;
    private int playerIndex => playerNetManager.playerIndex;

    [Header("Leg")]
    [SerializeField] private float torqueSize;
    [SerializeField] private float angle = 60f;
    [SerializeField] private float legSwitchInterval = 0.1f;
    [Header("Upper Legs")]
    [SerializeField] private Rigidbody2D LeftUpperLeg;
    [SerializeField] private Rigidbody2D RightUpperLeg;
    [SerializeField] private bool isLeftLegForward;

    [Header("Lower Legs")]

    [SerializeField] private Rigidbody2D LeftLowerLeg;
    [SerializeField] private Rigidbody2D RightLowerLeg;
    private HingeJoint2D LeftHingeJoint2D;
    private HingeJoint2D RightHingeJoint2D;

    private Coroutine switchLegCoroutine;
    private Vector4 hingeJointRangeAnchor = new Vector4(-90, 10, -10, 90);

    [SerializeField] private FacingDirection facingDirection;
    [SerializeField] private float t = 0f;
    [SerializeField] private float switchDirectionTime = 0.1f;
    [Header("Hand")]
    [SerializeField] private float handForce = 10f;
    [SerializeField] private Rigidbody2D LeftLowerArm;
    [SerializeField] private Rigidbody2D RightLowerArm;
    [Header("Move")]
    [SerializeField] private float acceleration;
    private int Moving = 0;
    private void Awake()
    {
        playerNetManager = GetComponent<NetStickManManager>();

        playerData = GetComponent<PlayerData>();
        rigidbodies = GetComponentsInChildren<Rigidbody2D>();

        LeftHingeJoint2D = LeftLowerLeg.GetComponent<HingeJoint2D>();
        RightHingeJoint2D = RightLowerLeg.GetComponent<HingeJoint2D>();

        nowStepTime = 0;

        isLeftLegForward = false;
    }
#if UNITY_EDITOR || !UNITY_SERVER

    private void Update()
    {
        MoveInput();
    }
    private void MoveInput()
    {
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            Moving = -1;
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            Moving = 1;
        }
        else
        {
            Moving = 0;
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

            playerNetManager.CmdMove(Moving);
#endif

        }
    }
#if UNITY_EDITOR || UNITY_SERVER
    private void MoveLeft()
    {
        foreach (Rigidbody2D rigidbody in rigidbodies)
        {
            rigidbody.AddForce(Vector3.left * acceleration * rigidbody.mass, ForceMode2D.Force);
        }
    }
    private void MoveRight()
    {
        foreach (Rigidbody2D rigidbody in rigidbodies)
        {
            rigidbody.AddForce(Vector3.right * acceleration * rigidbody.mass, ForceMode2D.Force);
        }
    }
    private void SwitchLeg(FacingDirection direction)
    {
        if (switchLegCoroutine != null)
        {
            if (facingDirection == direction)
                return;
            StopCoroutine(switchLegCoroutine);

            switchLegCoroutine = StartCoroutine(SwitchLegRoutine(direction));
        }
        else
        {
            switchLegCoroutine = StartCoroutine(SwitchLegRoutine(direction));
        }
    }
    IEnumerator SwitchLegRoutine(FacingDirection direction)
    {
        float targetT;

        if (facingDirection == FacingDirection.Left)
            targetT = 0f;
        else
            targetT = 1f;

        facingDirection = direction;

        while (Mathf.Abs(targetT - t) > 0.01f)
        {
            if (facingDirection == FacingDirection.Left)
                t -= Time.fixedDeltaTime / switchDirectionTime;
            else
                t += Time.fixedDeltaTime / switchDirectionTime;
            t = Mathf.Clamp(t, 0f, 1f);

            LeftHingeJoint2D.limits = new JointAngleLimits2D
            {
                min = Mathf.Lerp(hingeJointRangeAnchor.x, hingeJointRangeAnchor.z, t),
                max = Mathf.Lerp(hingeJointRangeAnchor.y, hingeJointRangeAnchor.w, t)
            };
            RightHingeJoint2D.limits = new JointAngleLimits2D
            {
                min = Mathf.Lerp(hingeJointRangeAnchor.x, hingeJointRangeAnchor.z, t),
                max = Mathf.Lerp(hingeJointRangeAnchor.y, hingeJointRangeAnchor.w, t)
            };

            yield return null;
        }

    }
    //正力逆时针
    private void DoAnimation()
    {
        if (playerData.OffGroundTime > 0.3f)
            return;
        nowStepTime += Time.fixedDeltaTime;

        if (nowStepTime > legSwitchInterval &&
            Vector2.Angle(LeftUpperLeg.transform.up, RightUpperLeg.transform.up) > angle)
        {
            nowStepTime = 0;
            isLeftLegForward ^= true;
        }

        if (isLeftLegForward)
        {
            LeftUpperLeg.AddTorque(torqueSize * LeftUpperLeg.mass, ForceMode2D.Force);
            RightUpperLeg.AddTorque(-torqueSize * RightUpperLeg.mass , ForceMode2D.Force);
        }
        else
        {
            LeftUpperLeg.AddTorque(-torqueSize * LeftUpperLeg.mass, ForceMode2D.Force);
            RightUpperLeg.AddTorque(torqueSize * RightUpperLeg.mass, ForceMode2D.Force);
        }
    }
    private void AddForceOnHand()
    {
        if(facingDirection == FacingDirection.Left)
        {
            LeftLowerArm.AddForce(Vector3.left * handForce * LeftLowerArm.mass * 0.8f, ForceMode2D.Force);
            RightLowerArm.AddForce(Vector3.left * handForce * RightLowerArm.mass * 1.0f, ForceMode2D.Force);
        }
        else
        {
            LeftLowerArm.AddForce(Vector3.right * handForce * LeftLowerArm.mass * 0.8f, ForceMode2D.Force);
            RightLowerArm.AddForce(Vector3.right * handForce * RightLowerArm.mass * 1.0f, ForceMode2D.Force);
        }
    }
    //common
    public void DoMove(int dir)
    {
        if (playerData.isAlive == false || playerData.isReseting)
            return;

        if (dir == 0)
        {
            playerData.isRunning = false;
            return;
        }
        else if (dir == -1)
        {
            playerData.isRunning = true;
            MoveLeft();
            DoAnimation();
            SwitchLeg(FacingDirection.Left);
            AddForceOnHand();
        }
        else if (dir == 1)
        {
            playerData.isRunning = true;
            MoveRight();
            DoAnimation();
            SwitchLeg(FacingDirection.Right);
            AddForceOnHand();
        }
    }
#endif
}
