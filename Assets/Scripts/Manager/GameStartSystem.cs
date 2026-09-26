using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.U2D;



public class GameStartSystem : MonoBehaviour
{

    private void OnEnable()
    {
        LoadingScreenCanvas.Instance.OnAllLoadedEvent += LoadHomeScene;
    }
    private void OnDisable()
    {
        LoadingScreenCanvas.Instance.OnAllLoadedEvent -= LoadHomeScene;
    }
    public void Start()
    {
        Screen.SetResolution(2560, 1440, FullScreenMode.FullScreenWindow);
        LoadingScreenCanvas.Instance.Load(LoadingScreenCanvas.SCENE_HOME_KEY);
    }
    private void LoadHomeScene()
    {
        StartCoroutine(LoadHomeSceneCoroutine());
    }
    private IEnumerator LoadHomeSceneCoroutine()
    {
        yield return new WaitForSeconds(2f);

        ScreenTransitionCanvas.Instance.ScreenTransition(
            ScreenTransitionHandle.BlackScreenFadeInAndOut,
            1f, 0.6f,
            () =>
            {
                StartCoroutine(AddressablesDriver.LoadScene(
                    LoadingScreenCanvas.HOME_SCENE_NAME,
                    LoadingScreenCanvas.Instance.Disable));
            });
    }

}
