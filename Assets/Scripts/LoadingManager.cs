using UnityEngine;

public class LoadingManager : MonoBehaviour
{
    public static LoadingManager Instance;
    [SerializeField] private GameObject loadingOverlay;

    private void Awake()
    {
        if(Instance == null)
        {
        Instance =this;
        DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    private void Start()
    {
        if(loadingOverlay !=null)
        {
            loadingOverlay.SetActive(false);
        }
    }
    public void ShowLoading()
    {
        if(loadingOverlay != null)
        {
            loadingOverlay.SetActive(true);
        }
    }
    public void HideLoading()
    {
        if(loadingOverlay != null)
        {
            loadingOverlay.SetActive(false);
        }
    }
}
