using UnityEngine;

// 타격감(게임 필) 매니저 — 히트스톱 + 화면 흔들림 + 플래시. 자동부팅·영구.
//  적 적중: Juice.Hit() / 패링 성공: Juice.ParryHit()
public class Juice : MonoBehaviour
{
    public static Juice Instance { get; private set; }

    private float stopTimer, stopReturn = 1f;
    private Color flashColor; private float flashTimer, flashDur;
    private Texture2D white;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("Juice").AddComponent<Juice>(); }

    // ── 정적 진입점 ──
    public static void HitStop(float dur) { if (Instance != null) Instance.DoHitStop(dur); }
    public static void Shake(float amt, float dur) { if (CameraFollow.Instance != null) CameraFollow.Instance.AddShake(amt, dur); }
    public static void Flash(Color c, float dur) { if (Instance != null) { Instance.flashColor = c; Instance.flashDur = Mathf.Max(0.01f, dur); Instance.flashTimer = Instance.flashDur; } }

    public static void Hit()      { HitStop(0.05f); Shake(0.14f, 0.12f); }                                          // 적 적중
    public static void ParryHit() { HitStop(0.12f); Shake(0.38f, 0.25f); Flash(new Color(1f, 1f, 1f, 0.32f), 0.12f); } // 패링 성공(강하게)

    private void DoHitStop(float dur)
    {
        if (Time.timeScale < 0.9f) return;                 // 이미 일시정지/슬로우(결과창 등)면 무시
        if (stopTimer <= 0f) stopReturn = Time.timeScale;
        stopTimer = Mathf.Max(stopTimer, dur);
        Time.timeScale = 0f;
    }

    void Update()
    {
        if (stopTimer > 0f) { stopTimer -= Time.unscaledDeltaTime; if (stopTimer <= 0f) Time.timeScale = stopReturn; }
        if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
    }

    void OnGUI()
    {
        if (flashTimer <= 0f) return;
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        var prev = GUI.color;
        GUI.color = new Color(flashColor.r, flashColor.g, flashColor.b, flashColor.a * Mathf.Clamp01(flashTimer / flashDur));
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = prev;
    }
}
