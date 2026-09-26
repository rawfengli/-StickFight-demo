using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class EdgeDetection : MonoBehaviour
{
    public float threshold = 0.1f;
    public Vector2 texelSize = new Vector2(0.03f, 0.03f);
    public Color edgeColor = Color.white;
    public float test = 1;
    [SerializeField] private Material material;
    [SerializeField] private Image image;
    public bool _enable;

    public void Awake()
    {
        image = GetComponent<Image>();
        material = image.material;
    }
    public void Update()
    {
        if(enabled)
        {
            SetPara();
        }
    }
    private void MyRenderer()
    {

    }
    public void SetPara()
    {
        /*
        Vector2 _texelSize = SpriteAtlasUtils.TexelSizeFromSpriteToSpriteAtlas(texelSize, image.sprite);
        material.SetFloat("_BlurSize", test);
        material.SetColor("_EdgeColor", edgeColor);
        material.SetFloat("_Threshold", threshold);
        material.SetVector("_TexelSize", _texelSize);
        */
    }
}
