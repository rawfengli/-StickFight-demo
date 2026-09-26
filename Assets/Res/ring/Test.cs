using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    [Header("Common")]
    public bool Do;
    public float CD = 1.0f;
    [Header("Content")]
    public Image contentImage;
    Vector3 rawSacle;
    Vector4 targetScale;
    public void Awake()
    {
        rawSacle = transform.localScale;
    }
    public void Update()
    {
        if(Do)
        {
            Do = false;
            StartCoroutine(MyTest());
        }
    }
    IEnumerator MyTest()
    {
        float now = 0;

        while (now < CD + 0.01f)
        {
            float t = now / CD;
            Vector4 color = Vector4.Lerp(new(2, 0, 0, 1), new(0f, 2.0f, 0f, 1), t);
            contentImage.color = new(color.x, color.y, color.z, color.w);

            contentImage.fillAmount = t;

            now += Time.deltaTime;
            yield return null;
        }
    }
}
