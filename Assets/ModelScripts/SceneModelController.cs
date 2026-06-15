using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneModelController : MonoBehaviour
{
    public void GoToARScene()
    {
        SceneManager.LoadScene("ArViewerScene");
    }

    public void GoTo3DScene()
    {
        SceneManager.LoadScene("3DViewerScene");
    }

    public void GoTo3DListScene()
    {
        SceneManager.LoadScene("ModelListScene");
    }

    public static void NormalizeModel(GameObject modelRoot, float targetSize = 2.0f)
    {
        if (modelRoot == null)
        {
            return;
        }

        Bounds bounds = CalculateBounds(modelRoot);

        float maxSize = Mathf.Max(
            bounds.size.x,
            bounds.size.y,
            bounds.size.z
        );

        if (maxSize > 0.0001f)
        {
            float scale = targetSize / maxSize;
            modelRoot.transform.localScale = modelRoot.transform.localScale * scale;
        }

        bounds = CalculateBounds(modelRoot);

        Vector3 offset = modelRoot.transform.position - bounds.center;
        modelRoot.transform.position = offset;
    }

    private static Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return new Bounds(obj.transform.position, Vector3.one);
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }
}