using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.SceneManagement;
using System.Collections;
using GLTFast;

// ============================================================
//  UNIVERSE MODEL VIEWER — PREMIUM EDITION
//  Cách 2: 1 camera duy nhất (AR Camera = Normal Camera trong XR Origin)
//
//  NÂNG CẤP SO VỚI BẢN CŨ:
//  - Func5  XRay        : Fade in/out mượt + màu xanh phát sáng như scanner
//  - Func6  Interaction : Lock/Unlock với hiệu ứng flash outline trên model
//  - Func7  Exploded    : Từng mảnh bay ra/vào mượt (không còn teleport giật cục)
//  - Func9  Lighting    : 3 chế độ đèn (Normal / Studio / Night) chuyển mượt
//  - Func10 Measure     : Marker đẹp + label khoảng cách nổi 3D + line gradient
// ============================================================
public class UniverseModelViewer : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────
    //  INSPECTOR FIELDS
    // ─────────────────────────────────────────────────────────
    [Header("Setup")]
    public Transform    modelTarget;
    public Camera       mainCamera;       // Kéo Normal Camera (bên trong XR Origin) vào đây
    public RectTransform modelScreenArea;
    private Vector3     AR_MODEL_SCALE = new Vector3(0.15f, 0.15f, 0.15f);

    [Header("AR Setup")]
    public GameObject    arSession;
    public GameObject    xrOrigin;
    public ARRaycastManager arRaycastManager;
    public ARPlaneManager   arPlaneManager;
    public GameObject    exitARButton;
    private bool         isARModeOn         = false;
    private bool         modelPlacedOnPlane = false;

    [Header("UI Remote Setup")]
    public GameObject blueRemotePanel;
    private bool      isRemoteOpen = false;

    [Header("UI Background Setup")]
    public Image     backgroundImage;
    public Sprite[]  bgSprites;
    private int      currentBgIndex = 0;

    [Header("Hotspot UI Setup")]
    public GameObject      hotspotPopupPanel;
    public TextMeshProUGUI textTitle;
    public TextMeshProUGUI textDesc;

    [Header("Dynamic Data")]
    public static string modelFirebaseUrl = "";
    private Dictionary<string, string> currentHotspotData = new Dictionary<string, string>();

    [Header("Interaction Settings")]
    public float rotationSpeed  = 0.5f;
    public float zoomSpeed      = 0.05f;
    public float minZoom        = 15f;
    public float maxZoom        = 90f;
    public float pinchZoomSpeed = 0.01f;

    [Header("Lighting Settings")]
    public Light directionalLight;

    // ─────────────────────────────────────────────────────────
    //  PRIVATE STATE
    // ─────────────────────────────────────────────────────────
    private GameObject currentModel;
    private Renderer[] modelRenderers;
    private Dictionary<Renderer, Material[]> originalMaterials  = new Dictionary<Renderer, Material[]>();
    private Dictionary<Transform, Vector3>   originalPositions  = new Dictionary<Transform, Vector3>();

    private bool    isAutoRotating     = false;
    private Vector3 defaultScale;
    private Quaternion defaultRotation;
    private Vector3 defaultPosition;

    private Vector2 touchStartPos;
    private Vector2 lastInputPos;
    private bool    isSwiping = false;

    // Feature flags
    private bool isXRayOn          = false;
    private bool isExploded        = false;
    private bool isHotspotModeOn   = false;
    private bool isInteractionAllowed = true;
    private bool isMeasureModeOn   = false;

    // Lighting cycle: 0=Normal  1=Studio  2=Night
    private int  lightingMode = 0;

    // Rotation
    private float rotationYaw   = 0f;
    private float rotationPitch = 0f;

    // Measure tool
    private int     measureTapCount = 0;
    private Vector3 measurePointA;
    private Vector3 measurePointB;
    private GameObject markerA;
    private GameObject markerB;
    private LineRenderer measureLine;
    private GameObject  measureLabelObj;
    private TextMeshPro measureLabelTMP;

    // Running coroutines (để cancel khi gọi lại)
    private Coroutine xrayCoroutine;
    private Coroutine explodeCoroutine;
    private Coroutine lightCoroutine;
    private Coroutine lockFlashCoroutine;

    // Hotspot data
    private Dictionary<string, string> heartData = new Dictionary<string, string>()
    {
        {"LeftAtrium",    "Tâm nhĩ trái\n\nNhận máu giàu oxy từ tĩnh mạch phổi và bơm xuống tâm thất trái."},
        {"RightAtrium",   "Tâm nhĩ phải\n\nNhận máu nghèo oxy từ tĩnh mạch chủ và bơm xuống tâm thất phải."},
        {"LeftVentricle", "Tâm thất trái\n\nCó cơ bắp dày nhất, chịu trách nhiệm bơm máu đi nuôi toàn bộ cơ thể."},
        {"RightVentricle","Tâm thất phải\n\nBơm máu nghèo oxy lên phổi để thực hiện quá trình trao đổi khí."}
    };

    // ─────────────────────────────────────────────────────────
    //  START
    // ─────────────────────────────────────────────────────────
    void Start()
    {
        bool isARScene = SceneManager.GetActiveScene().name == "ArViewerScene";

        if (isARScene)
        {
            if (backgroundImage  != null) backgroundImage.gameObject.SetActive(false);
            if (blueRemotePanel  != null) blueRemotePanel.SetActive(false);
            if (hotspotPopupPanel!= null) hotspotPopupPanel.SetActive(false);
            if (exitARButton     != null) exitARButton.SetActive(true);

            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = false;
                arPlaneManager.SetTrackablesActive(false);
            }

            isARModeOn         = true;
            modelPlacedOnPlane = false;
            LoadModel();
        }
        else
        {
            if (arSession        != null) arSession.SetActive(false);
            if (xrOrigin         != null) xrOrigin.SetActive(false);
            if (blueRemotePanel  != null) blueRemotePanel.SetActive(false);
            if (hotspotPopupPanel!= null) hotspotPopupPanel.SetActive(false);
            if (backgroundImage  != null) backgroundImage.gameObject.SetActive(true);

            isARModeOn = false;
            LoadModel();
        }
    }

    // ─────────────────────────────────────────────────────────
    //  UPDATE
    // ─────────────────────────────────────────────────────────
    void Update()
    {
        if (!isARModeOn)
            UpdateModelPositionToScreen();

        HandleTouchInteraction();

        if (isARModeOn && !modelPlacedOnPlane)
            HandleARPlacement();

        if (isAutoRotating && currentModel != null && !isARModeOn)
            currentModel.transform.Rotate(Vector3.up, 30f * Time.deltaTime);

        // Cập nhật label đo lường luôn quay về camera
        UpdateMeasureLabel();
    }

    // ─────────────────────────────────────────────────────────
    //  AR PLACEMENT
    // ─────────────────────────────────────────────────────────
    private void HandleARPlacement()
    {
        bool isClick = false;
        Vector2 pos  = Vector2.zero;
        int pid      = -1;

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            isClick = true; pos = Input.GetTouch(0).position; pid = Input.GetTouch(0).fingerId;
        }
        else if (Input.GetMouseButtonDown(0))
        {
            isClick = true; pos = Input.mousePosition;
        }

        if (!isClick) return;
        if (EventSystem.current.IsPointerOverGameObject(pid)) return;
        if (arRaycastManager == null || currentModel == null) return;

        var hits = new List<ARRaycastHit>();
        if (arRaycastManager.Raycast(pos, hits, TrackableType.PlaneWithinBounds))
        {
            currentModel.transform.position   = hits[0].pose.position;
            currentModel.transform.localScale  = AR_MODEL_SCALE;

            Vector3 dir = (mainCamera.transform.position - currentModel.transform.position).normalized;
            currentModel.transform.rotation = Quaternion.LookRotation(new Vector3(dir.x, 0, dir.z));

            if (arPlaneManager != null)
            {
                arPlaneManager.enabled = false;
                foreach (var plane in arPlaneManager.trackables)
                    plane.gameObject.SetActive(false);
            }
            modelPlacedOnPlane = true;
            if (hotspotPopupPanel != null) hotspotPopupPanel.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────
    //  LOAD MODEL
    // ─────────────────────────────────────────────────────────
    private async void LoadModel()
    {
        if (string.IsNullOrEmpty(modelFirebaseUrl))
        {
            Debug.LogError("Không có URL mô hình từ Firebase!"); return;
        }

        currentModel = new GameObject("DownloadedModel");
        currentModel.transform.SetParent(modelTarget);
        currentModel.transform.localPosition = Vector3.zero;

        var gltfAsset = currentModel.AddComponent<GltfAsset>();
        string safeUrl = modelFirebaseUrl.Replace(" ", "%20");
        bool success   = await gltfAsset.Load(safeUrl);

        if (!success) { Debug.LogError("Lỗi khi giải mã model GLB!"); return; }

        Debug.Log("Model load thành công!");
        await System.Threading.Tasks.Task.Yield();

        foreach (var anim in currentModel.GetComponentsInChildren<Animation>())
        { anim.playAutomatically = false; anim.Stop(); }

        NormalizeModelSize(currentModel);

        defaultScale    = currentModel.transform.localScale;
        defaultRotation = currentModel.transform.rotation;
        defaultPosition = currentModel.transform.position;

        modelRenderers = currentModel.GetComponentsInChildren<Renderer>();
        foreach (Renderer r in modelRenderers)
        {
            originalMaterials.Add(r, r.sharedMaterials);
            originalPositions.Add(r.transform, r.transform.localPosition);
            if (r.gameObject.GetComponent<Collider>() == null)
                r.gameObject.AddComponent<MeshCollider>();
        }

        if (isARModeOn && arPlaneManager != null)
        {
            arPlaneManager.enabled = true;
            arPlaneManager.SetTrackablesActive(true);
            if (mainCamera != null)
            {
                currentModel.transform.position   = mainCamera.transform.position + mainCamera.transform.forward * 1.5f;
                currentModel.transform.localScale  = AR_MODEL_SCALE;
            }
            ShowPopupUI("Chế độ AR Đang Mở",
                "1. Quét camera xuống sàn / mặt bàn.\n2. Chạm vào lưới xanh để đặt mô hình.");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  UPDATE MODEL POSITION (3D MODE)
    // ─────────────────────────────────────────────────────────
    private void UpdateModelPositionToScreen()
{
    if (modelScreenArea == null || mainCamera == null || modelTarget == null) return;
    Canvas canvas = modelScreenArea.GetComponentInParent<Canvas>();
    if (canvas == null) return;

    Vector3 screenPos;
    if (isRemoteOpen)
    {
        Vector3[] corners = new Vector3[4];
        modelScreenArea.GetWorldCorners(corners);
        Vector3 cw = (corners[0] + corners[2]) / 2f;
        screenPos = (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            ? cw
            : RectTransformUtility.WorldToScreenPoint(canvas.worldCamera ?? mainCamera, cw);
    }
    else
    {
        screenPos = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
    }

    // Kéo model lại gần camera hơn (đổi từ 8f thành 5.5f)
    screenPos.z = 5.5f;
    modelTarget.position = Vector3.Lerp(modelTarget.position,
        mainCamera.ScreenToWorldPoint(screenPos), Time.deltaTime * 10f);

    if (currentModel != null)
    {
        // Loại bỏ logic thu nhỏ, luôn lerp về defaultScale để giữ nguyên kích thước
        currentModel.transform.localScale = Vector3.Lerp(
            currentModel.transform.localScale, defaultScale, Time.deltaTime * 10f);
    }
}

    // ─────────────────────────────────────────────────────────
    //  NORMALIZE MODEL SIZE
    // ─────────────────────────────────────────────────────────
    private void NormalizeModelSize(GameObject model)
{
    Renderer[] rs = model.GetComponentsInChildren<Renderer>();
    if (rs.Length == 0) return;

    Bounds b = new Bounds(model.transform.position, Vector3.zero);
    bool ok  = false;
    foreach (Renderer r in rs)
    {
        if (r.bounds.size.sqrMagnitude > 0) { b.Encapsulate(r.bounds); ok = true; }
    }
    if (!ok) return;

    float maxD  = Mathf.Max(b.size.x, b.size.y, b.size.z);
    
    // Tăng hằng số mục tiêu từ 4f lên 7f để model hiển thị to hơn gấp rưỡi
    float sf    = maxD > 0.001f ? 5f / maxD : 1f; 
    
    model.transform.localScale  = new Vector3(sf, sf, sf);
    model.transform.position    = modelTarget.position + (model.transform.position - b.center);
    defaultScale                = model.transform.localScale;
}
    // ─────────────────────────────────────────────────────────
    //  TOUCH / MOUSE INTERACTION
    // ─────────────────────────────────────────────────────────
    private void HandleTouchInteraction()
    {
        if (!isInteractionAllowed || currentModel == null) return;
        if (EventSystem.current.IsPointerOverGameObject() ||
           (Input.touchCount > 0 && EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId)))
            return;

        if (Input.touchCount > 0)
        {
            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began)
                {
                    touchStartPos = t.position; isSwiping = false;
                    SyncRotation();
                }
                else if (t.phase == TouchPhase.Moved)
                {
                    if (!isSwiping && Vector2.Distance(touchStartPos, t.position) > 10f) isSwiping = true;
                    if (isSwiping)
                    {
                        rotationYaw   -= t.deltaPosition.x * rotationSpeed;
                        rotationPitch += t.deltaPosition.y * rotationSpeed;
                        rotationPitch  = Mathf.Clamp(rotationPitch, -80f, 80f);
                        currentModel.transform.rotation = Quaternion.Euler(rotationPitch, rotationYaw, 0f);
                    }
                }
                else if (t.phase == TouchPhase.Ended && !isSwiping)
                {
                    if (isHotspotModeOn) ShootRaycast();
                    else if (isMeasureModeOn) ShootRaycastForMeasure();
                }
            }
            else if (Input.touchCount == 2 && !isARModeOn)
            {
                Touch t0 = Input.GetTouch(0), t1 = Input.GetTouch(1);
                float prev = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
                float cur  = (t0.position - t1.position).magnitude;
                mainCamera.fieldOfView = Mathf.Clamp(
                    mainCamera.fieldOfView + (prev - cur) * pinchZoomSpeed, minZoom, maxZoom);
            }
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            touchStartPos = Input.mousePosition; lastInputPos = Input.mousePosition;
            isSwiping = false; SyncRotation();
        }
        if (Input.GetMouseButton(0))
        {
            Vector2 cur = Input.mousePosition;
            if (!isSwiping && Vector2.Distance(touchStartPos, cur) > 5f) isSwiping = true;
            if (isSwiping)
            {
                Vector2 d  = cur - lastInputPos;
                rotationYaw   -= d.x * rotationSpeed * 0.3f;
                rotationPitch += d.y * rotationSpeed * 0.3f;
                rotationPitch  = Mathf.Clamp(rotationPitch, -80f, 80f);
                currentModel.transform.rotation = Quaternion.Euler(rotationPitch, rotationYaw, 0f);
            }
            lastInputPos = cur;
        }
        if (Input.GetMouseButtonUp(0) && !isSwiping)
        {
            if (isHotspotModeOn) ShootRaycast();
            else if (isMeasureModeOn) ShootRaycastForMeasure();
        }
        if (!isARModeOn)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0)
                mainCamera.fieldOfView = Mathf.Clamp(
                    mainCamera.fieldOfView - scroll * 100f * zoomSpeed, minZoom, maxZoom);
        }
    }

    private void SyncRotation()
    {
        rotationYaw   = currentModel.transform.eulerAngles.y;
        rotationPitch = currentModel.transform.eulerAngles.x;
        if (rotationPitch > 180f) rotationPitch -= 360f;
    }

    // ─────────────────────────────────────────────────────────
    //  RAYCAST
    // ─────────────────────────────────────────────────────────
    private void ShootRaycast()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit))
            ShowHotspotData(hit.collider.gameObject.name);
        else if (hotspotPopupPanel != null)
            hotspotPopupPanel.SetActive(false);
    }

    private void ShootRaycastForMeasure()
    {
        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit)) return;

        if (measureTapCount == 0 || measureTapCount == 2)
        {
            measurePointA   = hit.point;
            measureTapCount = 1;
            AnimateMarker(ref markerA, measurePointA, new Color(1f, 0.8f, 0f));  // vàng
            if (measureLine != null) measureLine.enabled = false;
            if (markerB     != null) markerB.SetActive(false);
            HideMeasureLabel();
            ShowPopupUI("Đo lường – Điểm A", "Đã ghim điểm A.\nVuốt xoay model rồi chạm điểm B.");
        }
        else if (measureTapCount == 1)
        {
            measurePointB   = hit.point;
            measureTapCount = 2;
            AnimateMarker(ref markerB, measurePointB, new Color(0f, 1f, 0.5f));  // xanh ngọc
            DrawGradientLine(measurePointA, measurePointB);

            float dist = Vector3.Distance(measurePointA, measurePointB) * 2.5f;
            ShowMeasureLabel((measurePointA + measurePointB) * 0.5f, dist.ToString("F1") + " cm");
            ShowPopupUI("Kết quả Đo lường",
                "Khoảng cách: <b>" + dist.ToString("F1") + " cm</b>\n\n<i>Chạm lại để đo lần mới.</i>");
        }
    }

    // ─────────────────────────────────────────────────────────
    //  POPUP UI
    // ─────────────────────────────────────────────────────────
    private void ShowPopupUI(string title, string desc)
    {
        if (textTitle        != null) textTitle.text = title;
        if (textDesc         != null) textDesc.text  = desc;
        if (hotspotPopupPanel != null) hotspotPopupPanel.SetActive(true);
    }

    private void ShowHotspotData(string partName)
    {
        string raw = currentHotspotData.ContainsKey(partName) ? currentHotspotData[partName]
            : "Chưa có thông tin\n\nGiáo viên chưa cập nhật bộ phận: " + partName;
        string[] sp = raw.Split(new string[] {"\n\n"}, System.StringSplitOptions.None);
        if (textTitle != null) textTitle.text = sp[0];
        if (textDesc  != null) textDesc.text  = sp.Length > 1 ? sp[1] : "";
        if (hotspotPopupPanel != null) hotspotPopupPanel.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────
    //  TOGGLE REMOTE PANEL
    // ─────────────────────────────────────────────────────────
    public void ToggleRemotePanel()
    {
        isRemoteOpen = !isRemoteOpen;
        if (blueRemotePanel != null) blueRemotePanel.SetActive(isRemoteOpen);
        
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 1  RESET VIEW
    // ─────────────────────────────────────────────────────────
    public void Func1_ResetView()
    {
        if (currentModel == null) return;
        currentModel.transform.rotation = defaultRotation;
        mainCamera.fieldOfView = 60f;
        isAutoRotating = false;

        if (isExploded) Func7_ExplodedView();
        if (isXRayOn)   Func5_ToggleXRay();

        isHotspotModeOn = isInteractionAllowed = true;
        isMeasureModeOn = false;
        if (measureLine  != null) measureLine.enabled = false;
        if (markerA      != null) markerA.SetActive(false);
        if (markerB      != null) markerB.SetActive(false);
        HideMeasureLabel();
        if (hotspotPopupPanel != null) hotspotPopupPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 2  AUTO ROTATE
    // ─────────────────────────────────────────────────────────
    public void Func2_ToggleAutoRotate() => isAutoRotating = !isAutoRotating;

    // ─────────────────────────────────────────────────────────
    //  FUNC 3  ANIMATION
    // ─────────────────────────────────────────────────────────
    public void Func3_PlayAnimation()
    {
        if (currentModel == null) return;
        var anims = currentModel.GetComponentsInChildren<Animation>();
        if (anims.Length > 0)
            foreach (var a in anims) { if (a.isPlaying) a.Stop(); else a.Play(); }
        else
            ShowPopupUI("Hoạt ảnh", "Mô hình này là tĩnh, không có chuyển động.");
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 4  CHANGE BACKGROUND
    // ─────────────────────────────────────────────────────────
    public void Func4_ChangeBackground()
    {
        if (backgroundImage != null && bgSprites.Length > 0)
        {
            currentBgIndex = (currentBgIndex + 1) % bgSprites.Length;
            backgroundImage.sprite = bgSprites[currentBgIndex];
        }
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 5  XRAY — fade mượt + màu scanner
    // ─────────────────────────────────────────────────────────
    public void Func5_ToggleXRay()
    {
        if (modelRenderers == null) return;
        isXRayOn = !isXRayOn;

        if (xrayCoroutine != null) StopCoroutine(xrayCoroutine);
        xrayCoroutine = StartCoroutine(XRayTransition(isXRayOn));
    }

    private IEnumerator XRayTransition(bool turningOn)
    {
        float duration = 0.45f;
        float t        = 0f;

        // Màu XRay: xanh lam phát sáng như scanner
        Color xrayTint = new Color(0.1f, 0.6f, 1f, 0.22f);

        // Bước 1: Nếu đang BẬT XRay, trước hết clone materials sang transparent
        if (turningOn)
        {
            foreach (Renderer r in modelRenderers)
            {
                Material[] mats = new Material[r.materials.Length];
                for (int i = 0; i < r.materials.Length; i++)
                {
                    mats[i] = new Material(r.materials[i]);
                    SetMaterialTransparent(mats[i]);
                }
                r.materials = mats;
            }
        }

        // Bước 2: Animate alpha
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = Mathf.SmoothStep(0f, 1f, t / duration);

            foreach (Renderer r in modelRenderers)
            {
                foreach (Material mat in r.materials)
                {
                    // Hỗ trợ cả Standard (_Color) lẫn glTF shader (_BaseColor)
                    string cp = mat.HasProperty("_BaseColor") ? "_BaseColor"
                              : mat.HasProperty("_Color")     ? "_Color" : null;
                    if (cp == null) continue;

                    Material[] origMats = originalMaterials.ContainsKey(r) ? originalMaterials[r] : null;
                    Color orig = (origMats != null && origMats.Length > 0 && origMats[0].HasProperty(cp))
                        ? origMats[0].GetColor(cp) : Color.white;

                    Color target = turningOn ? xrayTint : new Color(orig.r, orig.g, orig.b, 1f);
                    Color from   = turningOn ? new Color(orig.r, orig.g, orig.b, 1f) : xrayTint;
                    mat.SetColor(cp, Color.Lerp(from, target, progress));

                    // Emission glow — chỉ dùng nếu shader hỗ trợ
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.EnableKeyword("_EMISSION");
                        Color emitC = turningOn
                            ? new Color(0.05f, 0.3f, 0.6f) * (progress * 0.6f)
                            : Color.black * (1f - progress);
                        mat.SetColor("_EmissionColor", emitC);
                    }
                }
            }
            yield return null;
        }

        // Bước 3: Khi TẮT xong → trả về material gốc
        if (!turningOn)
        {
            foreach (Renderer r in modelRenderers)
                r.materials = originalMaterials[r];
        }
    }

    private void SetMaterialTransparent(Material mat)
    {
        mat.SetFloat("_Mode", 3);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = 3000;
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 6  TOGGLE INTERACTION — flash outline khi lock
    // ─────────────────────────────────────────────────────────
    public void Func6_ToggleInteraction()
    {
        isInteractionAllowed = !isInteractionAllowed;

        if (lockFlashCoroutine != null) StopCoroutine(lockFlashCoroutine);
        lockFlashCoroutine = StartCoroutine(LockFlashEffect(isInteractionAllowed));

        ShowPopupUI(
            isInteractionAllowed ? "[OK] Tuong Tac: BAT"   : "[X] Tuong Tac: KHOA",
            isInteractionAllowed ? "Bạn có thể xoay và zoom mô hình tự do."
                                 : "Mô hình đã bị khoá. Bấm lại để mở khóa.");
    }

    private IEnumerator LockFlashEffect(bool unlocking)
    {
        if (modelRenderers == null) yield break;

        // Flash màu đỏ (lock) hoặc xanh lá (unlock) 2 lần rồi tắt
        Color flashColor = unlocking ? new Color(0.2f, 1f, 0.4f) : new Color(1f, 0.2f, 0.2f);
        int   flashes    = 2;
        float halfPeriod = 0.12f;

        for (int i = 0; i < flashes * 2; i++)
        {
            bool bright = (i % 2 == 0);
            foreach (Renderer r in modelRenderers)
            {
                foreach (Material mat in r.materials)
                {
                    bool hasEmission = mat.HasProperty("_EmissionColor");
                    if (hasEmission)
                    {
                        mat.EnableKeyword("_EMISSION");
                        mat.SetColor("_EmissionColor", bright ? flashColor * 0.8f : Color.black);
                    }
                }
            }
            yield return new WaitForSeconds(halfPeriod);
        }

        // Tắt emission sau flash
        foreach (Renderer r in modelRenderers)
            foreach (Material mat in r.materials)
                if (mat.HasProperty("_EmissionColor"))
                    mat.SetColor("_EmissionColor", Color.black);
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 7  EXPLODED VIEW — từng mảnh bay ra mượt
    // ─────────────────────────────────────────────────────────
    public void Func7_ExplodedView()
    {
        if (modelRenderers == null) return;
        isExploded = !isExploded;

        if (explodeCoroutine != null) StopCoroutine(explodeCoroutine);
        explodeCoroutine = StartCoroutine(ExplodeTransition(isExploded));
    }

    private IEnumerator ExplodeTransition(bool exploding)
    {
        float duration = 0.6f;
        float t        = 0f;

        // Snapshot vị trí hiện tại của từng part
        var startPositions = new Dictionary<Transform, Vector3>();
        foreach (Renderer r in modelRenderers)
        {
            Transform part = r.transform;
            if (part != currentModel.transform)
                startPositions[part] = part.localPosition;
        }

        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = EaseOutCubic(t / duration);   // easing mượt

            foreach (Renderer r in modelRenderers)
            {
                Transform part = r.transform;
                if (part == currentModel.transform || !startPositions.ContainsKey(part)) continue;
                if (!originalPositions.ContainsKey(part)) continue;

                Vector3 origin    = originalPositions[part];
                Vector3 exploded  = origin + (part.position - currentModel.transform.position).normalized * 2f;

                Vector3 from = startPositions[part];
                Vector3 to   = exploding ? exploded : origin;
                part.localPosition = Vector3.Lerp(from, to, ease);
            }
            yield return null;
        }

        // Snap cuối để tránh floating point drift
        foreach (Renderer r in modelRenderers)
        {
            Transform part = r.transform;
            if (part == currentModel.transform || !originalPositions.ContainsKey(part)) continue;
            part.localPosition = exploding
                ? originalPositions[part] + (part.position - currentModel.transform.position).normalized * 2f
                : originalPositions[part];
        }
    }

    private float EaseOutCubic(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp01(x), 3f);

    // ─────────────────────────────────────────────────────────
    //  FUNC 8  HOTSPOTS
    // ─────────────────────────────────────────────────────────
    public void Func8_ToggleHotspots()
    {
        isHotspotModeOn = !isHotspotModeOn;
        if (!isHotspotModeOn && hotspotPopupPanel != null)
            hotspotPopupPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 9  LIGHTING — 3 chế độ chuyển mượt
    //          0 = Normal (trắng, intensity 1)
    //          1 = Studio (xanh lạnh, intensity 1.4 — nổi bật chi tiết)
    //          2 = Night  (xanh tím, intensity 0.25 — huyền bí)
    // ─────────────────────────────────────────────────────────
    public void Func9_ChangeLighting()
    {
        if (directionalLight == null) { Debug.LogWarning("Chưa gán Directional Light!"); return; }

        lightingMode = (lightingMode + 1) % 3;

        Color   targetColor;
        float   targetIntensity;
        string  modeName;

        switch (lightingMode)
        {
            case 1:
                targetColor     = new Color(0.85f, 0.92f, 1f);    // trắng xanh lạnh
                targetIntensity = 1.4f;
                modeName        = "Studio";
                break;
            case 2:
                targetColor     = new Color(0.15f, 0.1f, 0.35f);  // tím đêm
                targetIntensity = 0.25f;
                modeName        = "Night";
                break;
            default:
                targetColor     = Color.white;
                targetIntensity = 1f;
                modeName        = "Normal";
                break;
        }

        if (lightCoroutine != null) StopCoroutine(lightCoroutine);
        lightCoroutine = StartCoroutine(LightTransition(targetColor, targetIntensity));

        ShowPopupUI("Anh sang: " + modeName, "Đang chuyển chế độ ánh sáng...");
        if (hotspotPopupPanel != null)
            StartCoroutine(AutoHidePopup(1.5f));
    }

    private IEnumerator LightTransition(Color toColor, float toIntensity)
    {
        float duration    = 0.7f;
        float t           = 0f;
        Color fromColor   = directionalLight.color;
        float fromIntense = directionalLight.intensity;

        while (t < duration)
        {
            t += Time.deltaTime;
            float ease = EaseOutCubic(t / duration);
            directionalLight.color     = Color.Lerp(fromColor,   toColor,      ease);
            directionalLight.intensity = Mathf.Lerp(fromIntense, toIntensity,  ease);
            yield return null;
        }
        directionalLight.color     = toColor;
        directionalLight.intensity = toIntensity;
    }

    private IEnumerator AutoHidePopup(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (hotspotPopupPanel != null) hotspotPopupPanel.SetActive(false);
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 10  MEASURE TOOL
    // ─────────────────────────────────────────────────────────
    public void Func10_MeasureTool()
    {
        // isMeasureModeOn = !isMeasureModeOn;
        // isHotspotModeOn  = false;
        // measureTapCount  = 0;

        // if (!isMeasureModeOn)
        // {
        //     if (measureLine != null) measureLine.enabled = false;
        //     if (markerA     != null) markerA.SetActive(false);
        //     if (markerB     != null) markerB.SetActive(false);
        //     HideMeasureLabel();
        //     if (hotspotPopupPanel != null) hotspotPopupPanel.SetActive(false);
        // }
        // else
        // {
        //     ShowPopupUI("Do luong", "Chạm vào bề mặt model để ghim Điểm A.");
        // }
        ShowPopupUI("Chế độ Đo đạc", "Đang được phát triển.");
    }

    // ─────────────────────────────────────────────────────────
    //  MEASURE HELPERS — marker đẹp + label 3D nổi
    // ─────────────────────────────────────────────────────────

    /// Tạo/cập nhật marker hình cầu nhỏ, có hiệu ứng scale pop
    private void AnimateMarker(ref GameObject marker, Vector3 pos, Color color)
    {
        if (marker == null)
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(marker.GetComponent<Collider>());

            // Material tự phát sáng
            Material mat = new Material(Shader.Find("Standard"));
            mat.EnableKeyword("_EMISSION");
            string cp2 = mat.HasProperty("_BaseColor") ? "_BaseColor"
                       : mat.HasProperty("_Color")     ? "_Color" : "_Color";
            mat.SetColor(cp2, color);
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", color * 0.7f);
            marker.GetComponent<Renderer>().material = mat;
        }

        if (currentModel != null) marker.transform.SetParent(currentModel.transform, true);
        marker.transform.position = pos;
        marker.SetActive(true);

        // Pop scale animation
        StartCoroutine(PopScale(marker.transform, 0.055f));
    }

    private IEnumerator PopScale(Transform t, float finalSize)
    {
        Vector3 target = new Vector3(finalSize, finalSize, finalSize);
        float dur = 0.25f, elapsed = 0f;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            float s = EaseOutCubic(elapsed / dur);
            // Overshoot nhẹ rồi về đúng size
            float overshoot = 1f + 0.35f * Mathf.Sin(s * Mathf.PI);
            t.localScale = target * overshoot;
            yield return null;
        }
        t.localScale = target;
    }

    /// Vẽ đường thẳng màu gradient vàng→xanh
    private void DrawGradientLine(Vector3 a, Vector3 b)
    {
        if (measureLine == null)
        {
            GameObject obj = new GameObject("MeasureLine");
            measureLine = obj.AddComponent<LineRenderer>();
            measureLine.positionCount = 2;
            measureLine.material      = new Material(Shader.Find("Sprites/Default"));
            measureLine.widthCurve    = AnimationCurve.Constant(0, 1, 1f);
        }

        // Gradient vàng → xanh ngọc
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.85f, 0f), 0f),
                new GradientColorKey(new Color(0f, 1f, 0.5f),  1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            }
        );
        measureLine.colorGradient = grad;

        if (currentModel != null)
        {
            measureLine.transform.SetParent(currentModel.transform);
            measureLine.transform.localPosition = Vector3.zero;
            measureLine.transform.localRotation = Quaternion.identity;
            measureLine.transform.localScale    = Vector3.one;
            measureLine.useWorldSpace = false;
            float lw = 0.04f / currentModel.transform.localScale.x;
            measureLine.startWidth = lw;
            measureLine.endWidth   = lw;
            measureLine.SetPosition(0, currentModel.transform.InverseTransformPoint(a));
            measureLine.SetPosition(1, currentModel.transform.InverseTransformPoint(b));
        }
        else
        {
            measureLine.useWorldSpace = true;
            measureLine.startWidth = measureLine.endWidth = 0.04f;
            measureLine.SetPosition(0, a);
            measureLine.SetPosition(1, b);
        }
        measureLine.enabled = true;
    }

    /// Label nổi 3D giữa 2 điểm đo
    private void ShowMeasureLabel(Vector3 worldPos, string text)
    {
        if (measureLabelObj == null)
        {
            measureLabelObj = new GameObject("MeasureLabel");
            measureLabelTMP = measureLabelObj.AddComponent<TextMeshPro>();
            measureLabelTMP.fontSize        = 1.8f;
            measureLabelTMP.alignment       = TextAlignmentOptions.Center;
            measureLabelTMP.fontStyle       = FontStyles.Bold;
            measureLabelTMP.color           = Color.white;
            measureLabelTMP.outlineWidth    = 0.25f;
            measureLabelTMP.outlineColor    = new Color32(0, 0, 0, 220);
        }

        if (currentModel != null)
            measureLabelObj.transform.SetParent(currentModel.transform, true);

        measureLabelObj.transform.position = worldPos + Vector3.up * 0.12f;
        measureLabelTMP.text    = text;
        measureLabelObj.SetActive(true);
    }

    private void HideMeasureLabel()
    {
        if (measureLabelObj != null) measureLabelObj.SetActive(false);
    }

    /// Luôn quay label về phía camera
    private void UpdateMeasureLabel()
    {
        if (measureLabelObj == null || !measureLabelObj.activeSelf || mainCamera == null) return;
        measureLabelObj.transform.LookAt(mainCamera.transform);
        measureLabelObj.transform.Rotate(0, 180f, 0);
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 11  AR MODE
    // ─────────────────────────────────────────────────────────
    public void Func11_EnterARMode()
    {
        // isARModeOn = !isARModeOn;

        // if (isARModeOn)
        // {
        //     if (xrOrigin  != null) xrOrigin.SetActive(true);
        //     if (arSession != null) arSession.SetActive(true);
        //     if (backgroundImage  != null) backgroundImage.gameObject.SetActive(false);
        //     if (exitARButton     != null) exitARButton.SetActive(true);
        //     if (blueRemotePanel  != null) blueRemotePanel.SetActive(false);

        //     modelPlacedOnPlane = false;

        //     if (arPlaneManager != null)
        //     {
        //         arPlaneManager.enabled = true;
        //         arPlaneManager.SetTrackablesActive(true);
        //     }

        //     if (currentModel != null)
        //     {
        //         currentModel.transform.localScale = AR_MODEL_SCALE;
        //         currentModel.SetActive(true);
        //         if (mainCamera != null)
        //             currentModel.transform.position = mainCamera.transform.position
        //                                             + mainCamera.transform.forward * 1.5f;
        //     }

        //     ShowPopupUI("Chế độ AR", "Quét camera xuống sàn.\nChạm vào lưới để đặt mô hình.");
        // }
        // else
        // {
        //     if (arSession != null) arSession.SetActive(false);
        //     if (xrOrigin  != null) xrOrigin.SetActive(false);
        //     if (backgroundImage != null) backgroundImage.gameObject.SetActive(true);
        //     if (exitARButton    != null) exitARButton.SetActive(false);
        //     isARModeOn = false;
        //     Func1_ResetView();
        // }
        ShowPopupUI("Chế độ AR", "Đang được phát triển.");
    }

    // ─────────────────────────────────────────────────────────
    //  FUNC 12  VR MODE
    // ─────────────────────────────────────────────────────────
    public void Func12_EnterVRMode() =>
        ShowPopupUI("Chế độ VR", "Đang được phát triển.");
}