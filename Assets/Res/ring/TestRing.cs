using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class TestRing : MonoBehaviour
{
    [SerializeField] private Material material;
    [SerializeField] public float totalCD;
    [SerializeField] public float nowCD;
    [SerializeField] public float fadeTime;
    [SerializeField] private Image image;

    public bool _Do;
    public void Awake()
    {
        if(image == null)
            image = GetComponent<Image>();
        material = image.material;
    }
    public void Update()
    {
        if(_Do)
        {
            CDUI();
            _Do = false;
        }
    }
    public void CDUI()
    {
        nowCD = 0;
        StartCoroutine(DuringCD());
    }
    IEnumerator DuringCD()
    {
        material.SetFloat("_TotalCD", totalCD);
        material.SetFloat("_FadeTime", fadeTime);
        while (nowCD < totalCD + fadeTime)
        {
            nowCD += Time.deltaTime;
            material.SetFloat("_NowCD", nowCD);
            yield return null;
        }
    }
}
