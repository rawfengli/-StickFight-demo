using System.IO;
using UnityEngine;

public class PlayerParticles : MonoBehaviour
{
    private PlayerController controller;
    private NetStickManManager playerNetManager;

    private void Awake()
    {
        if (NetServer.active)
            return;

        controller = GetComponent<PlayerController>();
        playerNetManager = GetComponent<NetStickManManager>();
    }
    private void OnEnable()
    {
        if (NetServer.active)
            return;
        InGameEventBus.Instance.Register_OnPlayerHit_Event(OnHit);

        controller.RegisterOnLegGroundedEvent(OnFootOnGrounded);
        controller.RegisterOnJumpedEvent(OnJumped);
    }
    private void OnDisable()
    {
        if (NetServer.active)
            return;

        InGameEventBus.Instance.Unregister_OnPlayerHit_Event(OnHit);

        controller.UnregisterOnLegGroundedEvent(OnFootOnGrounded);
        controller.UnregisterOnJumpedEvent(OnJumped);
    }


    private void OnFootOnGrounded(GroundChecker groundChecker)
    {
        if (NetServer.active)
            return;

        DustParticles dustParticles = ParticlesSystemManager.Instance.Get<DustParticles>();
        GameObject gameObject = dustParticles.gameObject;
        gameObject.transform.position = groundChecker.collisionPosition + Vector2.up * DustParticles.HALF_HEIGHT;
    }
    private float jumpPredictedTime = 0.1f;
    private void OnJumped()
    {
        if (NetServer.active)
            return;

        JumpParticles jumpParticles = ParticlesSystemManager.Instance.Get<JumpParticles>();
        GameObject gameObject = jumpParticles.gameObject;
        Vector3 newPos = controller.HipsPostion;
        newPos += Vector3.down * JumpParticles.HIPS_HEIGHT_OFFSET;
        newPos += new Vector3(controller.playerRigidbody.velocity.x, 0, 0) * jumpPredictedTime;
        gameObject.transform.position = newPos;
    }
    private void OnHit(OnPlayerHitData data)
    {
        if (data.playerIndex != playerNetManager.playerIndex)
            return;

        switch (data.hitType)
        {
            case HitType.HandAttack:
                HandAttackParticles(data);
                break;
            case HitType.BulletAttack:
                BulletAttackParticles(data);
                break;
        }
    }
    private void HandAttackParticles(OnPlayerHitData data)
    {
        BodyOnHitParticles hitParticles = ParticlesSystemManager.Instance.Get<BodyOnHitParticles>();
        hitParticles.gameObject.transform.position = data.hitPos;
        hitParticles.gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, data.damageDir);
        hitParticles.SetColorMaterial(playerNetManager.playerIndex);
    }
    private void BulletAttackParticles(OnPlayerHitData data)
    {
        BulletHitBodyParticles hitParticles = ParticlesSystemManager.Instance.Get<BulletHitBodyParticles>();
        hitParticles.gameObject.transform.position = data.hitPos;
        hitParticles.gameObject.transform.rotation = Quaternion.FromToRotation(Vector3.up, -data.damageDir);
        hitParticles.SetColorMaterial(playerNetManager.playerIndex);
    }
}
