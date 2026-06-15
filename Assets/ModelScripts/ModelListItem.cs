using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GLTFast;
using UnityEngine.SceneManagement;
using System.Threading.Tasks;

public class ModelListItem : MonoBehaviour
{
    [Header("UI References")]
    public RawImage previewImage;
    public TMP_Text nameText;
    public Button clickButton;

    private string currentUrl;
    private Camera renderCamera;
    private RenderTexture renderTexture;
    private GameObject modelContainer;

    public void Setup(string modelName, string modelUrl, int index)
    {
        nameText.text = modelName;
        currentUrl = modelUrl.Replace(" ", "%20");

        clickButton.onClick.RemoveAllListeners();
        clickButton.onClick.AddListener(OnItemClicked);

        renderTexture = new RenderTexture(512, 512, 24);
        previewImage.texture = renderTexture;

        Vector3 studioPos = new Vector3(0, 2000 + (index * 20), 0);

        GameObject camObj = new GameObject("PreviewCamera_" + index);
        camObj.transform.position = studioPos + new Vector3(0, 0, -3.5f);

        renderCamera = camObj.AddComponent<Camera>();
        renderCamera.targetTexture = renderTexture;
        renderCamera.clearFlags = CameraClearFlags.SolidColor;
        renderCamera.backgroundColor = new Color(0.9f, 0.9f, 0.9f);

        GameObject lightObj = new GameObject("PreviewLight_" + index);
        lightObj.transform.position = studioPos + new Vector3(0, 3, -3);
        lightObj.transform.rotation = Quaternion.Euler(45, 0, 0);

        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;

        modelContainer = new GameObject("ModelContainer_" + index);
        modelContainer.transform.position = studioPos;

        LoadModelAsync();
    }

    private async void LoadModelAsync()
    {
        GltfAsset gltfAsset = modelContainer.AddComponent<GltfAsset>();
        bool success = await gltfAsset.Load(currentUrl);

        if (success)
        {
            await Task.Delay(200);
            NormalizeModelSize(modelContainer);
        }
        else
        {
            Debug.LogError("Lỗi tải Preview Model: " + currentUrl);
        }
    }

    private void NormalizeModelSize(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            return;
        }

        Vector3 studioPos = model.transform.position;

        Bounds bounds = renderers[0].bounds;

        foreach (Renderer r in renderers)
        {
            bounds.Encapsulate(r.bounds);
        }

        float maxDimension = Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            bounds.size.z
        );

        if (maxDimension <= 0.0001f)
        {
            return;
        }

        float scaleFactor = 2.5f / maxDimension;
        model.transform.localScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);

        Bounds newBounds = renderers[0].bounds;

        foreach (Renderer r in renderers)
        {
            newBounds.Encapsulate(r.bounds);
        }

        Vector3 offset = model.transform.position - newBounds.center;
        model.transform.position = studioPos + offset;

        if (model.GetComponent<AutoRotator>() == null)
        {
            model.AddComponent<AutoRotator>();
        }
    }

    private void OnItemClicked()
    {
        UniverseModelViewer.modelFirebaseUrl = currentUrl;
        SceneManager.LoadScene("3DViewerScene");
    }

    private void OnDestroy()
    {
        if (renderCamera != null)
        {
            Destroy(renderCamera.gameObject);
        }

        if (modelContainer != null)
        {
            Destroy(modelContainer);
        }

        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }
}

public class AutoRotator : MonoBehaviour
{
    private void Update()
    {
        transform.Rotate(Vector3.up * 25f * Time.deltaTime, Space.World);
    }
}