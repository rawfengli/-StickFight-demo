using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HourglassUI : MonoBehaviour
{
    [SerializeField] private float turnInterval = 0.2f;
    [SerializeField] private float CDTime = 1.5f;
    [SerializeField] private float turnLerp = 0.02f;
    [SerializeField] private Image image;
    [SerializeField] private Material material;
    private float HourglassHeight = 0.4f;
    private bool isDoTween;
    private int target = 1;
    private int count = 0;
    private void Awake()
    {
        material = image.material;
    }
    private void OnEnable()
    {
        count = 0;
        target = 1;
        isDoTween = false;
        gameObject.transform.rotation = Quaternion.identity;
    }
    private void Update()
    {
        if(isDoTween == false)
        {
            StartCoroutine(Loading());
        }
    }

    IEnumerator Loading()
    {
        isDoTween = true;
        count++;

        float nowTime;
        nowTime = 0;
        //cd
        while (nowTime < CDTime)
        {
            nowTime += Time.deltaTime;
            float t = nowTime / CDTime;
            if (target == 1)
            {
                float h1 = HourglassHeight * (1 - t);
                float h2 = HourglassHeight * t;

                material.SetFloat("_TopLine1", 1 - HourglassHeight + h1);
                material.SetFloat("_BottomLine1", 1 - HourglassHeight);

                material.SetFloat("_TopLine2", h2);
                material.SetFloat("_BottomLine2", 0);
            }
            else
            {
                float h1 = HourglassHeight * (1 - t);
                float h2 = HourglassHeight * t;

                material.SetFloat("_TopLine1", 1);
                material.SetFloat("_BottomLine1", 1 - h2);

                material.SetFloat("_TopLine2", HourglassHeight);
                material.SetFloat("_BottomLine2", h2);
            }
            yield return null;
        }
        //wait for 0.2f
        nowTime = 0;
        while (nowTime < turnInterval)
        {
            nowTime += Time.deltaTime;
            yield return null;
        }
        Quaternion targetRot = Quaternion.Euler(0, 0, 180 * count);
        while (Quaternion.Angle(transform.rotation, targetRot) > 1f)
        {
            transform.rotation = Slerp(transform.rotation, targetRot, turnLerp);
            yield return null;
        }
        //wait for 0.2f
        nowTime = 0;
        while (nowTime < turnInterval)
        {
            nowTime += Time.deltaTime;
            yield return null;
        }
        target ^= 1;
        isDoTween = false;
    }
    public Quaternion Slerp(Quaternion q1, Quaternion q2, float t)
    {
        t = Mathf.Clamp01(t);

        float dot = Quaternion.Dot(q1, q2);

        float theta = Mathf.Acos(Mathf.Clamp(dot, -1.0f, 1.0f));

        if (theta < 0.0001f) 
            return q1;

        float sinTheta = Mathf.Sin(theta);
        float w1 = Mathf.Sin((1.0f - t) * theta) / sinTheta;
        float w2 = Mathf.Sin(t * theta) / sinTheta;

        return new Quaternion(
            w1 * q1.x + w2 * q2.x,
            w1 * q1.y + w2 * q2.y,
            w1 * q1.z + w2 * q2.z,
            w1 * q1.w + w2 * q2.w
        );
    }
}
