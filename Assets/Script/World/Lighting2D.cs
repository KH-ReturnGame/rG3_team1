using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 2D 광원 분위기 토글(자동부팅·영구).
//  ⚠️ 기본 OFF. "플레이어 주변만 밝은" 손전등식은 카메라 구역(방) 구조와 안 맞아서 보류 —
//     맵(합친 메트로배니아 맵)을 제대로 만든 뒤 '구역별 분위기 조명'으로 다시 설계 예정. 코드만 남겨둠.
//  · 켜기: Lighting2D.SetEnabled(true) (또는 Enabled=true 후 씬 진입). 켜면 글로벌을 어둡게 + 플레이어 추종 점광.
//  · 끄기(기본): 글로벌을 1.0(정상)으로 되돌리고 플레이어 점광 끔.
public class Lighting2D : MonoBehaviour
{
    public static Lighting2D Instance;
    public static bool Enabled = false;     // ★ 토글(기본 꺼짐)

    public float darkGlobal = 0.22f;
    public Color darkColor = new Color(0.40f, 0.47f, 0.68f);
    public float playerIntensity = 1.05f, playerOuter = 10f, playerInner = 2f;
    public Color playerColor = new Color(1f, 0.93f, 0.82f);

    private GameObject pLightGo;
    private Light2D pLight;
    private Transform target;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("Lighting2D");
        Instance = go.AddComponent<Lighting2D>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    void OnScene(Scene s, LoadSceneMode m) { Apply(); }
    void Start() { Apply(); }

    public static void SetEnabled(bool on) { Enabled = on; if (Instance != null) Instance.Apply(); }

    private void Apply()
    {
        // 글로벌 라이트: 켜짐=어둡게 / 꺼짐=1.0(정상). (현재 모든 씬이 1.0 글로벌 기준)
        foreach (var l in FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (l == pLight || l.lightType != Light2D.LightType.Global) continue;
            l.intensity = Enabled ? darkGlobal : 1f;
            l.color = Enabled ? darkColor : Color.white;
        }
        EnsurePlayerLight();
        if (pLight != null) pLight.enabled = Enabled;
    }

    private void EnsurePlayerLight()
    {
        if (pLightGo != null) return;
        pLightGo = new GameObject("__PlayerLight2D");
        DontDestroyOnLoad(pLightGo);
        pLight = pLightGo.AddComponent<Light2D>();
        pLight.lightType = Light2D.LightType.Point;
        pLight.color = playerColor;
        pLight.intensity = playerIntensity;
        pLight.pointLightOuterRadius = playerOuter;
        pLight.pointLightInnerRadius = playerInner;
        pLight.shadowIntensity = 0f;
        pLight.enabled = false;
    }

    void LateUpdate()
    {
        if (!Enabled || pLight == null) return;
        if (target == null && PlayerController.Instance != null) target = PlayerController.Instance.transform;
        if (target != null) pLightGo.transform.position = new Vector3(target.position.x, target.position.y, 0f);
    }
}
