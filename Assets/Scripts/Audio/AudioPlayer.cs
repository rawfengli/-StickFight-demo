using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.VisualScripting.Member;

[RequireComponent(typeof(AudioSource))]
public class AudioPlayer : MonoBehaviour
{
    private static AudioPlayer instance;
    public static AudioPlayer Instance => instance;

    public void Awake()
    {
        if(instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(instance);

        audioSource = GetComponent<AudioSource>();
    }
    AudioSource audioSource;
    public void Play(AudioClip clip)
    {
        audioSource.PlayOneShot(clip);
    }
    public void SetVolumeSize(float size)
    {

    }
    public void SetSoundSize(float size)
    {

    }
}
