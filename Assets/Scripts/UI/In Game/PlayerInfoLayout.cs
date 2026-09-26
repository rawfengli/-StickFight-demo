using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;

public class PlayerInfoLayout : MonoBehaviour
{
    #region UI
    [SerializeField] private List<PlayerHpDisplayer> hpInfos = new ();
    public WinnerUI winnerUI;
    private Vector2[] HpDisplayerStartPos = new Vector2[]
    {
        new Vector2(-1100, 600),
        new Vector2(-1100, 500),
        new Vector2(-1100, 400),
        new Vector2(-1100, 300),
    };
    private void Awake()
    {
        StartCoroutine(InitCoroutine());
        
    }
    private IEnumerator InitCoroutine()
    {
        //Reset
        {
            foreach (var hpDisplayer in hpInfos)
            {
                Destroy(hpDisplayer);
            }
            hpInfos.Clear();
            hpInfos.Add(null);

            Task<GameObject> task = AddressablesDriver.LoadAsset<GameObject>("Player Hp Displayer");
            while (true)
            {
                if (task.IsCompleted)
                    break;
                yield return null;
            }

            for (int i = 1; i <= 4; i++)
            {
                GameObject template = task.Result;
                GameObject obj = Instantiate(template);

                obj.transform.SetParent(transform);
                obj.transform.localPosition = HpDisplayerStartPos[i - 1];
                PlayerHpDisplayer displayer = obj.GetComponent<PlayerHpDisplayer>();
                hpInfos.Add(displayer);

                displayer.SetData(i);
                SetMaterial(i);

                obj.SetActive(false);
            }
        }

        {
            if (winnerUI != null)
            {
                Destroy(winnerUI.gameObject);
                winnerUI = null;
            }

            Task<GameObject> task = AddressablesDriver.LoadAsset<GameObject>("Winner UI");
            while (true)
            {
                if (task.IsCompleted)
                    break;
                yield return null;
            }
            GameObject template = task.Result;
            GameObject obj = Instantiate(template);
            obj.transform.SetParent(transform);
            obj.transform.localPosition = new Vector2(0, 600);
        }
    }
    public void SetMaterial(int playerIndex)
    {
        StartCoroutine(LoadHpMaterialCoroutine(playerIndex));
    }
    private IEnumerator LoadHpMaterialCoroutine(int playerIndex)
    {
        string name;
        switch (playerIndex)
        {
            case 1:
                name = "material Hp Red";
                break;
            case 2:
                name = "material Hp Blue";
                break;
            case 3:
                name = "material Hp Green";
                break;
            case 4:
                name = "material Hp Yellow";
                break;
            default:
                name = "material Hp Default";
                break;
        }
        Task<Material> task = AddressablesDriver.LoadAsset<Material>(name);
        while (true)
        {
            if (task.IsCompleted)
                break;
            yield return null;
        }
        hpInfos[playerIndex].SetMaterial(task.Result);
    }

    public void OnEnable()
    {
        InGameEventBus.Instance.Register_PlayerLoop_Event(PlayerLoop);
    }
    public void OnDisable()
    {
        InGameEventBus.Instance.Unregister_PlayerLoop_Event(PlayerLoop);
    }
    public void PlayerLoop(OnPlayerLoopInfo loopInfoData)
    {
        if (loopInfoData.playerIndex == 0)
            return;
        hpInfos[loopInfoData.playerIndex].gameObject.SetActive(true);
    }
    #endregion
}
