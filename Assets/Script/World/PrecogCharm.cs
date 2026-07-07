using UnityEngine;

// 예지안 장신구 효과(자동부팅·영구). 적이 공격을 시작하는 순간(Enemy.WindupStarted) 시간이 잠깐 느려져
// 반응(가드/패링/회피) 기회를 준다. 착용 중인 장신구 중 precogSlow가 있을 때만, 쿨다운(precogCooldown, 기본 20초)마다.
// 발동 시 플레이어 '눈'에서 붉은 빛이 번쩍이는 VFX(절차 생성 글로우 — 눈 위치는 eyeOffset으로 조정).
public class PrecogCharm : MonoBehaviour
{
    public static PrecogCharm Instance;
    public float slowScale = 0.22f;     // 감속 배율
    public float slowDuration = 1.7f;   // 실시간 지속(반응 창)

    [Header("발동 연출 — 붉은 눈빛")]
    public Vector2 eyeOffset = Vector2.zero;   // 자동 계산된 눈 위치에 더하는 미세조정(x는 바라보는 방향으로 반전)
    public float eyeFxDuration = 1.1f;         // 눈빛 지속(실시간)
    public float eyeSize = 0.95f;              // 글로우 크기(월드)
    public float screenPulse = 0.5f;           // 발동 순간 화면 가장자리 붉은 펄스 시간(0=끔)
    private float pulseStart = -99f;
    private static Texture2D glowTex, vigTex;
    private static Sprite glowSprite;
    private static Material addMat;            // 가산 블렌딩(있으면 진짜 발광)

    public static float CooldownLeft { get; private set; }   // 남은 쿨다운(HUD용)
    public static bool Equipped { get; private set; }        // 예지안 착용 여부(HUD용)
    private float nextReadyTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) { var go = new GameObject("PrecogCharm"); Instance = go.AddComponent<PrecogCharm>(); DontDestroyOnLoad(go); } }

    void OnEnable() { Enemy.WindupStarted += OnWindup; }
    void OnDisable() { Enemy.WindupStarted -= OnWindup; }

    void Update()
    {
        CooldownLeft = Mathf.Max(0f, nextReadyTime - Time.unscaledTime);
        Equipped = EquippedPrecog() != null;
    }

    private ItemData EquippedPrecog()
    {
        var eq = Equipment.Instance;
        if (eq == null) return null;
        foreach (var it in eq.Items())
            if (it.precogSlow) return it;
        return null;
    }

    [Header("발동 타이밍")]
    [Range(0f, 0.95f)] public float windupLateFraction = 0.85f;   // 예비동작이 이만큼 지난 '직전'에 발동
    private bool armed;

    private void OnWindup(Enemy e)
    {
        if (e == null || SlowMoFx.Active || armed) return;    // 이미 슬로우/대기 중이면 중복 방지
        if (Time.unscaledTime < nextReadyTime) return;        // 쿨다운
        var pc = PlayerController.Instance;
        if (pc == null || e.TargetPlayer != pc.transform) return;   // 플레이어를 노리는 공격만
        if (EquippedPrecog() == null) return;                 // 예지안 미착용
        StartCoroutine(FireLate(e, pc));
    }

    // 예비동작 후반까지 기다렸다가 '맞기 직전'에 발동
    private System.Collections.IEnumerator FireLate(Enemy e, PlayerController pc)
    {
        armed = true;
        float wait = Mathf.Max(0f, e.attackWindup * windupLateFraction);
        float t = 0f;
        while (t < wait)
        {
            if (e == null || !e.IsAttacking) { armed = false; yield break; }   // 공격 취소
            t += Time.deltaTime;
            yield return null;
        }
        armed = false;
        var charm = EquippedPrecog();
        if (e == null || !e.IsAttacking || charm == null || SlowMoFx.Active) yield break;

        SlowMoFx.BeginTimed(slowScale, slowDuration);
        nextReadyTime = Time.unscaledTime + Mathf.Max(1f, charm.precogCooldown);
        SpawnEyeFlash(pc);
        Toast.Show("예지안 — 시간이 느려진다", 1.2f);
    }

    // 외부(튜토리얼 각성 등)에서 눈빛 연출만 재생
    public static void PlayEyeFlash(PlayerController pc)
    {
        if (Instance != null && pc != null) Instance.SpawnEyeFlash(pc);
    }

    // ── 붉은 눈빛 VFX: 3겹 글로우(코어/미드/아우터, 가능하면 가산 블렌딩)가 번쩍 터졌다가 잦아듦 ──
    private void SpawnEyeFlash(PlayerController pc)
    {
        var sr = pc.GetComponent<SpriteRenderer>();
        float dir = (sr != null && sr.flipX) ? -1f : 1f;

        // 눈 위치: 스프라이트 크기 기반 자동(얼굴 근처 = 위 55%, 바라보는 쪽 30%) + eyeOffset 미세조정
        Vector3 pos;
        if (sr != null && sr.sprite != null)
        {
            var b = sr.bounds;
            pos = new Vector3(b.center.x + dir * b.extents.x * 0.30f, b.center.y + b.extents.y * 0.55f, 0f);
        }
        else pos = pc.transform.position + new Vector3(0.1f * dir, 0.4f, 0f);
        pos += new Vector3(eyeOffset.x * dir, eyeOffset.y, 0f);

        var root = new GameObject("PrecogEyeFlash");
        root.transform.SetParent(pc.transform);
        root.transform.position = pos;

        // 3겹: 크기·알파 다르게 겹쳐 훨씬 밝게
        var layers = new SpriteRenderer[3];
        float[] scales = { 0.38f, 0.80f, 1.55f };
        for (int i = 0; i < 3; i++)
        {
            var child = new GameObject("glow" + i);
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = Vector3.one * scales[i];
            var r = child.AddComponent<SpriteRenderer>();
            r.sprite = GlowSprite();
            r.sortingOrder = 62 - i;   // 코어가 맨 앞
            if (AdditiveMat() != null) r.material = AdditiveMat();   // 가산 = 진짜 발광
            layers[i] = r;
        }

        pulseStart = Time.unscaledTime;   // 화면 가장자리 붉은 펄스 시작
        StartCoroutine(EyeFlashRoutine(root, layers));
    }

    private System.Collections.IEnumerator EyeFlashRoutine(GameObject root, SpriteRenderer[] layers)
    {
        float t = 0f;
        var core = new Color(1f, 0.55f, 0.45f);   // 코어(밝음)
        var mid  = new Color(1f, 0.18f, 0.10f);
        var outer= new Color(1f, 0.05f, 0.03f);
        while (t < eyeFxDuration && root != null)
        {
            float k = t / eyeFxDuration;                                        // 0→1
            float pop = 1f + 0.9f * Mathf.Exp(-k * 9f);                          // 시작 순간 확 터짐
            float flick = 0.82f + 0.18f * Mathf.Sin(Time.unscaledTime * 46f);   // 빠른 점멸
            float fade = 1f - k * k;                                             // 끝에서 급감
            float a = fade * flick;

            if (layers[0] != null) layers[0].color = new Color(core.r, core.g, core.b, a);
            if (layers[1] != null) layers[1].color = new Color(mid.r, mid.g, mid.b, a * 0.9f);
            if (layers[2] != null) layers[2].color = new Color(outer.r, outer.g, outer.b, a * 0.55f);
            root.transform.localScale = Vector3.one * (eyeSize * pop);
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (root != null) Destroy(root);
    }

    // 발동 순간 화면 가장자리 붉은 펄스(강렬함 보강)
    void OnGUI()
    {
        float el = Time.unscaledTime - pulseStart;
        if (screenPulse <= 0f || el < 0f || el >= screenPulse) return;
        if (vigTex == null) BuildVignette();
        float k = el / screenPulse;
        float a = 0.55f * (1f - k) * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 40f));
        var prev = GUI.color;
        GUI.color = new Color(1f, 0.10f, 0.06f, a);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), vigTex);
        GUI.color = prev;
    }

    private static void BuildVignette()
    {
        const int N = 128;
        vigTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x / (float)(N - 1) - 0.5f) * 2f, dy = (y / (float)(N - 1) - 0.5f) * 2f;
                float rr = Mathf.Sqrt(dx * dx + dy * dy) / 1.41421f;
                float t = Mathf.Clamp01((rr - 0.45f) / (1.0f - 0.45f));          // 수동 smoothstep(가장자리만)
                float a = t * t * (3f - 2f * t);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        vigTex.SetPixels(px); vigTex.Apply();
    }

    // 가산 블렌딩 머티리얼(없으면 null → 일반 알파 블렌딩으로 폴백)
    private static Material AdditiveMat()
    {
        if (addMat != null) return addMat;
        var sh = Shader.Find("Legacy Shaders/Particles/Additive");
        if (sh == null) sh = Shader.Find("Particles/Additive");
        if (sh == null) return null;
        addMat = new Material(sh);
        return addMat;
    }

    // 절차 생성: 중심이 밝은(흰빛 섞인) 붉은 소프트 글로우
    private static Sprite GlowSprite()
    {
        if (glowSprite != null) return glowSprite;
        const int N = 64;
        glowTex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x / (float)(N - 1) - 0.5f) * 2f, dy = (y / (float)(N - 1) - 0.5f) * 2f;
                float rr = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - rr);
                a = a * a;                                        // 소프트 감쇠
                float coreK = Mathf.Clamp01(1f - rr * 3.2f);      // 중심 하이라이트
                px[y * N + x] = new Color(1f, 0.35f + 0.65f * coreK, 0.30f + 0.6f * coreK, a);
            }
        glowTex.SetPixels(px); glowTex.Apply();
        glowSprite = Sprite.Create(glowTex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), N);   // 1유닛 크기
        return glowSprite;
    }
}
