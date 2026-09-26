using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHpDisplayer : MonoBehaviour
{
    [SerializeField] private int playerIndex;
    [SerializeField] private Image Background;
    [SerializeField] private Image HpImage;
    private Material material => HpImage.material;
    private Vector3 pos;
    private void OnEnable()
    {
        pos = gameObject.transform.position;

        InGameEventBus.Instance.Register_OnPlayerHit_Event(OnPlayerHit);
        InGameEventBus.Instance.Register_PlayerLoop_Event(PlayerLoop);
    }
    private void OnDisable()
    {
        InGameEventBus.Instance.Unregister_OnPlayerHit_Event(OnPlayerHit);
        InGameEventBus.Instance.Unregister_PlayerLoop_Event(PlayerLoop);
    }
    public void SetData(int index)
    {
        playerIndex = index;
    }
    public void SetMaterial(Material material)
    {
        HpImage.material = material;
    }
    private Coroutine onHitCoroutine;
    public float shakeTime = 0.3f;
    public float shakeAmplitude = 0.3f;
    public float shakeFrequency = 0.3f;
    public void OnPlayerHit(OnPlayerHitData hitData)
    {
        if (hitData.playerIndex != playerIndex)
            return;

        if (onHitCoroutine != null)
            StopCoroutine(onHitCoroutine);

        onHitCoroutine = StartCoroutine(OnHitCoroutine());
    }
    private IEnumerator OnHitCoroutine()
    {
        float nowTime = 0;
        while (nowTime < shakeTime)
        {
            nowTime += Time.deltaTime;

            float offset = 0;
            offset = Mathf.Sin(nowTime * shakeFrequency) * (1 - nowTime / shakeTime) * shakeAmplitude;
            gameObject.transform.position = pos + new Vector3(offset, 0, 0);
            yield return null;
        }
    }
    public void PlayerLoop(OnPlayerLoopInfo loopInfoData)
    {
        if (loopInfoData.playerIndex != playerIndex)
            return;
        PlayerInfo.GetPlayerColor(loopInfoData.playerIndex, out Color color);
        material.SetColor("_Color", color);
        material.SetFloat("_Height", loopInfoData.hpPercent);

        color.r *= 0.4f;
        color.g *= 0.4f;
        color.b *= 0.4f;
        Background.color = color;
    }
}
