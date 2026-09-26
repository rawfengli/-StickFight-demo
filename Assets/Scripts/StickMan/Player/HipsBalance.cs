using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HipsBalance : MonoBehaviour
{
    [SerializeField] private NetStickManManager playerNetManager;    
    [SerializeField] private AnimationCurve curve;
    [SerializeField] private Rigidbody2D hips;
    [SerializeField] private PlayerData playerData;
    [SerializeField] private float balanceForceScale;
    private void Awake()
    {
        playerData = GetComponent<PlayerData>();
        playerNetManager = GetComponent<NetStickManManager>();
    }
    private void FixedUpdate()
    {
        if(playerNetManager.IsClientPlayer == false)
        {
            if (playerData.isAlive == false || playerData.isReseting)
                return;

            Vector2 hipsUp = hips.transform.up;

            float force = balanceForceScale * curve.Evaluate((Sin(hipsUp, Vector2.up) + 1) / 2.0f);
            if (Cos(hipsUp, Vector2.up) < 0)
                force = -force;

            hips.AddTorque(force * balanceForceScale * hips.mass, ForceMode2D.Force);
        }
    }
    private float Sin(Vector2 a, Vector2 b)
        => a.x * b.x + a.y * b.y;
    private float Cos(Vector2 a, Vector2 b)
        => a.x * b.y - a.y * b.x;
}
