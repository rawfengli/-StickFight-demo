using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LegBalance : MonoBehaviour
{
    [SerializeField] private NetStickManManager playerNetManager;
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private float balanceForceScale;
    [SerializeField] private Rigidbody2D UpperLeftLeg;
    [SerializeField] private Rigidbody2D UpperRightLeg;
    public PlayerData playerData;

    void Awake()
    {
        playerData = GetComponent<PlayerData>();
        playerNetManager = GetComponent<NetStickManManager>();
    }

    // Update is called once per frame
    private void FixedUpdate()
    {
        if (playerNetManager.IsClientPlayer == false)
        {
            if (playerData.isRunning)
                return;
            if (playerData.isAlive == false || playerData.isReseting)
                return;

            {
                Vector2 leftLegUp = UpperLeftLeg.transform.up;
                float force = -balanceForceScale * curve.Evaluate((Sin(leftLegUp, Vector2.up) + 1) / 2.0f);
                if (Cos(leftLegUp, Vector2.up) > 0)
                    force = 0;
                UpperLeftLeg.AddTorque(force * balanceForceScale * UpperLeftLeg.mass, ForceMode2D.Force);
            }

            {
                Vector2 rightLegUp = UpperRightLeg.transform.up;
                float force = balanceForceScale * curve.Evaluate((Sin(rightLegUp, Vector2.up) + 1) / 2.0f);
                if (Cos(rightLegUp, Vector2.up) < 0)
                    force = 0;

                UpperRightLeg.AddTorque(force * balanceForceScale * UpperRightLeg.mass, ForceMode2D.Force);
            }
        }
    }
    private float Sin(Vector2 a, Vector2 b)
    => a.x * b.x + a.y * b.y;
    private float Cos(Vector2 a, Vector2 b)
        => a.x * b.y - a.y * b.x;
}
