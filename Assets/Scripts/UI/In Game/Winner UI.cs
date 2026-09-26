using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.LowLevel;

public class WinnerUI : MonoBehaviour
{
    private Vector2 startPos = new Vector2(0, 600);
    private Vector2[] winnerPos = new Vector2[]
    {
        new Vector2(-1200, 600),
        new Vector2(-1200, 500),
        new Vector2(-1200, 400),
        new Vector2(-1200, 300),
    };
    public void OnEnable()
    {
        InGameEventBus.Instance.Register_GameFinished_Event(GameFinished);
    }
    public void OnDisable()
    {
        InGameEventBus.Instance.Unregister_GameFinished_Event(GameFinished);
    }

    Coroutine moveCoroutine;
    public void GameFinished(int winnerIndex)
    {
        Vector3 pos;
        if(winnerIndex == 0)
            pos = startPos;
        else
            pos = winnerPos[winnerIndex - 1];

        if (moveCoroutine != null)
            StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(MoveCoroutine(pos));
    }
    private float MoveTime = 1.0f;
    private IEnumerator MoveCoroutine(Vector3 targetPos)
    {
        float nowTime = 0f;
        while(nowTime < MoveTime)
        {
            float t = 0.1f;
            gameObject.transform.localPosition = Vector3.Lerp(gameObject.transform.localPosition, targetPos, t);
            nowTime += Time.deltaTime;
            yield return null;
        }
    }
}
