using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 전환 페이드(인/아웃). 자동부팅·영구.
//  - 새 씬이 로드되면 자동으로 검정 → 투명(페이드인).
//  - 전환은 SceneFader.FadeToScene(씬이름) 호출 → 투명 → 검정(페이드아웃) 후 로드.
public class SceneFader : MonoBehaviour
{
    public static SceneFader Instance;
    public float fadeDur = 0.4f;

    private float alpha = 1f;     // 시작은 검정(첫 씬 페이드인)
    private int dir = -1;         // -1 = 페이드인(밝아짐), +1 = 페이드아웃(어두워짐)
    private string pendingScene;
    private Texture2D black;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this; DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("SceneFader").AddComponent<SceneFader>(); }

    void OnEnable() { SceneManager.sceneLoaded += OnLoaded; }
    void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }
    private void OnLoaded(Scene s, LoadSceneMode m) { alpha = 1f; dir = -1; pendingScene = null; }   // 새 씬 → 페이드인

    // 전환: 검게 페이드아웃 후 씬 로드(끝나면 새 씬에서 자동 페이드인)
    public static void FadeToScene(string scene)
    {
        if (Instance == null) { SceneManager.LoadScene(scene); return; }
        Instance.dir = 1; Instance.pendingScene = scene;
    }

    void Update()
    {
        if (dir < 0) { if (alpha > 0f) alpha = Mathf.Max(0f, alpha - Time.unscaledDeltaTime / fadeDur); }
        else
        {
            alpha = Mathf.Min(1f, alpha + Time.unscaledDeltaTime / fadeDur);
            if (alpha >= 1f && pendingScene != null) { var sc = pendingScene; pendingScene = null; SceneManager.LoadScene(sc); }
        }
    }

    void OnGUI()
    {
        if (alpha <= 0.001f) return;
        if (black == null) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply(); }
        GUI.depth = -2000;   // 모든 UI 위(전환 중 전부 가림)
        var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
        GUI.color = prev;
    }
}
