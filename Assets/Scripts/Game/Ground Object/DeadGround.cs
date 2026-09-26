using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DeadGround : MonoBehaviour
{
    private void DeadUpdate(Collision2D collision)
    {
        if(collision.gameObject.layer == (int)GameLayer.StickMan)
        {
            StickBodyPart bodyPart = collision.gameObject.GetComponent<StickBodyPart>();
            bodyPart.data.Die();
        }
    }
    public void OnCollisionEnter2D(Collision2D collision)
        => DeadUpdate(collision);
    public void OnCollisionStay2D(Collision2D collision)
        => DeadUpdate(collision);

}
