using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-2)]
public class GroundChecker : MonoBehaviour
{
    public Action<GroundChecker> OnGroundedEvent;
    public bool isGrounded = false;
    public Vector2 collisionPosition;
    private bool lastFixedFrameOnGrounded = false;
    private int offGroundedFrameCount = 0;
    private void FixedUpdate()
    {
        if (lastFixedFrameOnGrounded == false)
        {
            isGrounded = false;
            offGroundedFrameCount++;
        }
        else
        {
            lastFixedFrameOnGrounded = false;
        }
    }
    private void GroundInfoUpdate(Collision2D collision)
    {

        if (collision.transform.root == transform.root)//self
            return;
        if (Vector3.Angle(Vector3.up, collision.GetContact(0).normal) > 75f)
            return;

        collisionPosition = collision.GetContact(0).point;

        if (offGroundedFrameCount > 5)
            OnGroundedEvent?.Invoke(this);

        isGrounded = true;
        offGroundedFrameCount = 0;
        lastFixedFrameOnGrounded = true;
    }
    public void OnCollisionEnter2D(Collision2D collision)
        => GroundInfoUpdate(collision);
    public void OnCollisionStay2D(Collision2D collision)
        => GroundInfoUpdate(collision);
}
