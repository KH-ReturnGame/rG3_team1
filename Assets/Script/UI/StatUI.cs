using UnityEngine;

// 플레이어 HUD — 나인 솔즈(九日) 레이아웃 오마주.
//  좌상단: 팔각 메달리온(초상화 자리 — 지금은 붉은 후드 실루엣) + 아래 매달린 골드 배지
//  그 오른쪽: 세그먼트 HP(칸=하트, 반칸 지원, 피격 잔상) → 아래 눈금 있는 얇은 XP 게이지 → 소용돌이 포션 차지(1번 슬롯)
//  좌하단: Q스킬 아이콘 + 기울어진 핍(쿨타임 진행) / 우하단: 예지 눈(착용 시 — 준비/쿨다운)
//  색·크기는 아래 팔레트/치수 상수만 만지면 됨(게임 톤에 맞춘 2차 수정용).
public class StatUI : MonoBehaviour
{
    public static StatUI Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HUD");
        go.AddComponent<Hotbar>();
        go.AddComponent<MenuUI>();
        go.AddComponent<StatUI>();
    }

    // ── 팔레트(나인 솔즈 카피 — 2차에서 게임 톤으로 조정) ──
    private static readonly Color Ink      = new Color(0.05f, 0.085f, 0.085f, 0.94f);   // 요소 바탕(어두운 청록 먹색)
    private static readonly Color Gold     = new Color(0.84f, 0.76f, 0.54f);            // 금테
    private static readonly Color GoldDim  = new Color(0.45f, 0.41f, 0.30f);
    private static readonly Color Teal     = new Color(0.34f, 0.93f, 0.72f);            // 체력/게이지 채움(민트)
    private static readonly Color TealDark = new Color(0.13f, 0.42f, 0.35f);
    private static readonly Color PipBlue  = new Color(0.38f, 0.66f, 0.96f);            // Q스킬 핍
    private static readonly Color HoodRed  = new Color(0.66f, 0.17f, 0.15f);            // 메달리온 후드 실루엣

    [Header("피격 잔상")]
    public float chipDelay = 0.30f;
    public float chipSpeed = 6f;

    private float displayHalf = -1f, chipHalf, chipWait;
    private float qReadyFlash;            // Q스킬 쿨 완료 순간 번쩍(초)
    private bool qWasReady = true;
    private GUIStyle goldStyle, cdStyle, glyphStyle;

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int cur = Mathf.Clamp(gm.CurrentHalf, 0, gm.MaxHalf);
        if (displayHalf < 0f) { displayHalf = cur; chipHalf = cur; return; }

        float dt = Time.unscaledDeltaTime;
        if (cur < displayHalf - 0.001f) { chipHalf = Mathf.Max(chipHalf, displayHalf); displayHalf = cur; chipWait = chipDelay; }
        else if (cur > displayHalf + 0.001f) { displayHalf = cur; chipHalf = Mathf.Max(displayHalf, chipHalf); }

        if (chipWait > 0f) chipWait -= dt;
        else if (chipHalf > displayHalf) chipHalf = Mathf.Max(displayHalf, chipHalf - chipSpeed * dt);

        // Q스킬 쿨 완료 순간 감지 → 번쩍 + 사운드
        var pcQ = PlayerController.Instance;
        if (pcQ != null)
        {
            bool ready = pcQ.SkillCooldownLeft <= 0f;
            if (ready && !qWasReady) { qReadyFlash = 0.6f; AudioManager.Sfx("skill_ready"); }
            qWasReady = ready;
        }
        if (qReadyFlash > 0f) qReadyFlash -= dt;
    }

    void OnGUI()
    {
        if (Letterbox.Covering) return;
        var gm = GameManager.Instance;
        if (gm == null) return;
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "StartScene") return;
        UIScale.Apply();
        EnsureStyles();

        float sw = UIScale.W, sh = UIScale.H;
        float x0 = sw * 0.018f, y0 = sh * 0.028f;

        // ── 1) 팔각 메달리온(초상화 자리) ──
        float mSize = Mathf.Clamp(sh * 0.088f, 64f, 118f);
        Rect medal = new Rect(x0, y0, mSize, mSize);
        GUI.DrawTexture(medal, MedallionTex());

        // ── 2) 매달린 골드 배지 ──
        float bx = medal.center.x - mSize * 0.18f;
        UITheme.Fill(new Rect(bx - 1f, medal.yMax - 4f, 2f, 12f), UITheme.A(Gold, 0.8f));   // 매다는 줄
        float bs = mSize * 0.30f;
        Rect badge = new Rect(bx - bs * 0.5f, medal.yMax + 7f, bs, bs);
        GUI.DrawTexture(badge, BadgeTex());
        goldStyle.fontSize = Mathf.RoundToInt(bs * 0.62f);
        goldStyle.normal.textColor = Gold;
        GUI.Label(new Rect(badge.xMax + 6f, badge.y - 2f, 160f, bs + 4f), gm.Gold.ToString("n0"), goldStyle);

        // ── 3) 세그먼트 HP(칸=하트) ──
        int maxHearts = Mathf.Max(1, gm.MaxHearts);
        int curHalves = Mathf.Clamp(gm.CurrentHalf, 0, gm.MaxHalf);
        if (displayHalf < 0f) { displayHalf = curHalves; chipHalf = curHalves; }
        int ghostTop = Mathf.CeilToInt(chipHalf - 0.001f);

        float segW = Mathf.Clamp(sh * 0.033f, 26f, 44f), segH = segW * 0.62f, segGap = 5f;
        float hx = medal.xMax + 14f, hy = y0 + 2f;
        for (int i = 0; i < maxHearts; i++)
        {
            Rect r = new Rect(hx + i * (segW + segGap), hy, segW, segH);
            int fill = Mathf.Clamp(curHalves - i * 2, 0, 2);
            int ghost = Mathf.Clamp(ghostTop - i * 2, 0, 2) - fill;

            UITheme.Fill(r, Ink);
            // 채움(반칸=왼쪽 절반) + 위기 고동
            if (fill > 0)
            {
                Color fc = Teal;
                if (curHalves <= 1) fc = Color.Lerp(Teal, Color.white, 0.30f + 0.30f * Mathf.Sin(Time.unscaledTime * 8f));
                Rect fr = new Rect(r.x + 2f, r.y + 2f, (r.width - 4f) * (fill == 2 ? 1f : 0.5f), r.height - 4f);
                UITheme.FillV(fr, fc, TealDark);
                UITheme.Fill(new Rect(fr.x, fr.y, fr.width, 2f), UITheme.A(Color.white, 0.35f));   // 윗광
            }
            // 피격 잔상(방금 깎인 반칸 — 크림색)
            if (ghost > 0)
            {
                float gx = r.x + 2f + (fill == 1 ? (r.width - 4f) * 0.5f : 0f);
                float gw2 = (r.width - 4f) * (ghost == 2 ? 1f : 0.5f);
                UITheme.Fill(new Rect(gx, r.y + 2f, gw2, r.height - 4f), new Color(0.95f, 0.90f, 0.70f, 0.9f));
            }
            UITheme.Border2(r, 1.5f, UITheme.A(Gold, 0.85f));
        }

        // ── 4) 얇은 XP 게이지(눈금 + 브래킷 프레임) ──
        float gaugeW = Mathf.Max(maxHearts * (segW + segGap) + segW * 2.5f, sh * 0.26f);
        Rect gauge = new Rect(hx, hy + segH + 10f, gaugeW, 8f);
        UITheme.Fill(gauge, Ink);
        float xpFrac = gm.XpToNext > 0 ? Mathf.Clamp01(gm.xp / (float)gm.XpToNext) : 0f;
        if (xpFrac > 0f) UITheme.FillV(new Rect(gauge.x + 1f, gauge.y + 1f, (gauge.width - 2f) * xpFrac, gauge.height - 2f), Teal, TealDark);
        for (int t = 1; t < 10; t++)   // 눈금
            UITheme.Fill(new Rect(gauge.x + gauge.width * t / 10f, gauge.y + 1f, 1f, gauge.height - 2f), UITheme.A(Color.black, 0.45f));
        UITheme.Border2(gauge, 1f, UITheme.A(Gold, 0.7f));
        UITheme.Fill(new Rect(gauge.x - 3f, gauge.y - 3f, 2f, gauge.height + 6f), Gold);           // 브래킷 좌
        UITheme.Fill(new Rect(gauge.xMax + 1f, gauge.y - 3f, 2f, gauge.height + 6f), Gold);        // 브래킷 우

        // ── 5) 소용돌이 포션 차지(핫바 1번 슬롯 소비 아이템) ──
        ItemData pot = Hotbar.Instance != null ? Hotbar.Instance.GetRegistered(0) : null;
        if (pot != null && pot.kind == ItemData.ItemKind.Consumable && Inventory.Instance != null)
        {
            int cnt = Inventory.Instance.CountOf(pot);
            float cool = gm.PotionCooldownLeft(pot);
            int shown = 5;
            float cd = segW * 0.72f, cGap = 6f;
            float cy = gauge.yMax + 9f;

            // 장식: 배지 쪽에서 차지 행으로 이어지는 꺾인 금선(╱─)
            Color oc = UITheme.A(Gold, 0.55f);
            Line45(new Vector2(hx - 22f, cy + cd * 0.5f + 8f), 11f, oc);
            UITheme.Fill(new Rect(hx - 14f, cy + cd * 0.5f - 1f, 10f, 2f), oc);
            for (int i = 0; i < shown; i++)
            {
                Rect cr = new Rect(hx + i * (cd + cGap), cy, cd, cd);
                bool full = i < cnt;
                var prev = GUI.color;
                GUI.color = full ? (cool > 0f ? new Color(1f, 1f, 1f, 0.38f) : Color.white) : new Color(1f, 1f, 1f, 0.9f);
                GUI.DrawTexture(cr, full ? SwirlTex() : RingTex());
                GUI.color = prev;
            }
            if (cool > 0f)
            {
                cdStyle.normal.textColor = UITheme.A(Gold, 0.9f);
                GUI.Label(new Rect(hx + shown * (cd + cGap) + 4f, cy - 2f, 60f, cd + 4f), Mathf.CeilToInt(cool) + "s", cdStyle);
            }
        }

        // ── 6) 좌하단: Q스킬 + 기울어진 핍 ──
        var pc = PlayerController.Instance;
        if (pc != null)
        {
            float qs = Mathf.Clamp(sh * 0.052f, 40f, 70f);
            Rect q = new Rect(sw * 0.018f, sh - qs - sh * 0.035f, qs, qs);

            bool qReady = pc.SkillCooldownLeft <= 0f;
            float flashK = qReadyFlash > 0f ? qReadyFlash / 0.6f : 0f;                 // 1→0 감쇠
            float readyPulse = qReady ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.5f) : 0f;

            // (a) 준비 완료 지속 — 배지 뒤 은은한 맥동 글로우(아드레날린 '풀' 상태 강조)
            if (qReady) UITheme.Glow(q, PipBlue, 7f + 5f * readyPulse, 0.16f + 0.14f * readyPulse);

            // (b) 쿨 완료 순간 — 강한 글로우 폭발 + 바깥으로 확산하는 링
            if (qReadyFlash > 0f)
            {
                UITheme.Glow(q, PipBlue, 22f, 0.72f * flashK);
                float ex = (1f - flashK) * qs;                                          // 링 확산 0→qs
                Rect ring = new Rect(q.x - ex * 0.5f, q.y - ex * 0.5f, q.width + ex, q.height + ex);
                UITheme.Border2(ring, 1f + 2.5f * flashK, UITheme.A(PipBlue, 0.85f * flashK));
            }

            // (c) 배지 — 완료 순간 스케일 펀치(살짝 커졌다 복귀)
            float punch = 1f + 0.20f * flashK * flashK;
            float gw = qs * punch, gh = qs * punch;
            Rect qDraw = new Rect(q.center.x - gw * 0.5f, q.center.y - gh * 0.5f, gw, gh);
            GUI.DrawTexture(qDraw, BadgeTex());
            glyphStyle.fontSize = Mathf.RoundToInt(qs * 0.46f);
            glyphStyle.normal.textColor = qReady ? UITheme.Lighten(Gold, 0.22f * readyPulse) : UITheme.A(Gold, 0.7f);
            GUI.Label(qDraw, "Q", glyphStyle);

            // (d) 진행 핍 — 쿨 중엔 차오르고, 준비되면 전부 밝게 맥동
            float frac = pc.skillCooldown > 0f ? 1f - Mathf.Clamp01(pc.SkillCooldownLeft / pc.skillCooldown) : 1f;
            int pips = 5, lit = Mathf.FloorToInt(frac * pips + 0.0001f);
            float pw = qs * 0.42f, ph = qs * 0.30f, pGap = 5f;
            float px = q.xMax + 12f, py = q.center.y - ph * 0.5f;
            for (int i = 0; i < pips; i++)
            {
                var prev = GUI.color;
                bool on = i < lit;
                if (qReady) GUI.color = Color.Lerp(PipBlue, UITheme.Lighten(PipBlue, 0.3f), readyPulse);   // 준비: 전부 밝게 맥동
                else        GUI.color = on ? PipBlue : new Color(PipBlue.r, PipBlue.g, PipBlue.b, 0.18f);
                GUI.DrawTexture(new Rect(px + i * (pw + pGap), py, pw, ph), PipTex());
                GUI.color = prev;
            }

            // 장식: 핍 아래 꺾인 금선(── ╱ ──) — 레퍼런스 하단 장식 모티프
            Color qc = UITheme.A(Gold, 0.5f);
            float oy2 = py + ph + 9f;
            UITheme.Fill(new Rect(px - 2f, oy2, pw * 2.4f, 2f), qc);
            Line45(new Vector2(px - 2f + pw * 2.4f, oy2 + 1f), 11f, qc);
            UITheme.Fill(new Rect(px - 2f + pw * 2.4f + 8f, oy2 - 8f, pw * 1.4f, 2f), qc);
            UITheme.Diamond(new Vector2(px - 2f + pw * 2.4f + 8f + pw * 1.4f + 4f, oy2 - 7f), 5f, qc);
        }

        // ── 7) 우하단: 예지 눈(착용 시) ──
        if (PrecogCharm.Equipped)
        {
            bool ready = PrecogCharm.CooldownLeft <= 0f;
            float es = Mathf.Clamp(sh * 0.048f, 36f, 64f);
            Rect eye = new Rect(sw - es * 1.9f - sw * 0.012f, sh - es - sh * 0.035f, es * 1.9f, es);
            var prev = GUI.color;
            GUI.color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTexture(eye, EyeTex());
            GUI.color = prev;
            if (!ready)
            {
                cdStyle.normal.textColor = UITheme.A(Gold, 0.85f);
                GUI.Label(new Rect(eye.x - 52f, eye.y, 48f, es), Mathf.CeilToInt(PrecogCharm.CooldownLeft) + "s", cdStyle);
            }
        }

        // ── 8) 우하단 구석 장식(세로 금선 + 꺾임 — 레퍼런스 모티프, 항상 표시) ──
        {
            Color rc = UITheme.A(Gold, 0.45f);
            float rx = sw - sw * 0.010f;
            float ry = sh - sh * 0.035f;
            UITheme.Fill(new Rect(rx - 2f, ry - sh * 0.085f, 2f, sh * 0.062f), rc);          // 세로선
            Line45(new Vector2(rx - 2f, ry - sh * 0.023f), 10f, rc, true);                    // ╲ 꺾임
            UITheme.Fill(new Rect(rx - 2f - 8f - 26f, ry - sh * 0.023f + 8f, 26f, 2f), rc);   // 아랫단 가로선
            UITheme.Diamond(new Vector2(rx - 2f - 8f - 30f, ry - sh * 0.023f + 9f), 4f, rc);
        }
    }

    // 45도 사선(2px). down=false면 오른쪽 위로(╱), true면 오른쪽 아래로(╲)
    private static void Line45(Vector2 from, float len, Color c, bool down = false)
    {
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(down ? 45f : -45f, from);
        UITheme.Fill(new Rect(from.x, from.y - 1f, len, 2f), c);
        GUI.matrix = m;
    }

    private void EnsureStyles()
    {
        if (goldStyle != null) return;
        goldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        cdStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        glyphStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    // ══════════ 절차 생성 텍스처 ══════════
    // 팔각형 거리함수: max(사각, 45도 사각/1.3) ≤ 1 이 내부
    private static float OctDist(float x, float y)
        => Mathf.Max(Mathf.Max(Mathf.Abs(x), Mathf.Abs(y)), (Mathf.Abs(x) + Mathf.Abs(y)) / 1.30f);

    private static Texture2D _medal;
    private static Texture2D MedallionTex()   // 금테 팔각 + 먹색 바탕 + 붉은 후드 실루엣(초상화 placeholder)
    {
        if (_medal != null) return _medal;
        const int N = 128;
        _medal = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int yy = 0; yy < N; yy++)
            for (int xx = 0; xx < N; xx++)
            {
                float u = (xx + 0.5f) / N * 2f - 1f, v = (yy + 0.5f) / N * 2f - 1f;
                float d = OctDist(u, v);
                Color c = Color.clear;
                if (d <= 1f)
                {
                    if (d > 0.90f) c = Gold;                                     // 외곽 금테
                    else if (d > 0.84f) c = new Color(0.02f, 0.04f, 0.04f, 1f);  // 테 안쪽 어두운 골
                    else if (d > 0.80f) c = UITheme.A(Gold, 0.55f);              // 얇은 안쪽 테
                    else
                    {
                        c = Ink; c.a = 0.96f;
                        // 후드 실루엣: 위로 뾰족, 아래로 퍼지는 두건(placeholder — 초상화 에셋 오면 교체)
                        float hy2 = v * -1f;   // 위가 +
                        if (hy2 > -0.55f && hy2 < 0.42f)
                        {
                            float t = (0.42f - hy2) / 0.97f;                     // 꼭대기 0 → 아래 1
                            float half = 0.50f * Mathf.Pow(t, 0.85f);
                            if (Mathf.Abs(u) < half) c = HoodRed;
                            // 얼굴 구멍(어둡게)
                            float fx = u / 0.22f, fy = (hy2 + 0.08f) / 0.20f;
                            if (fx * fx + fy * fy < 1f && hy2 < 0.18f) c = new Color(0.03f, 0.05f, 0.05f, 1f);
                        }
                    }
                    // 가장자리 안티앨리어스
                    c.a *= Mathf.Clamp01((1f - d) * N * 0.5f);
                }
                px[yy * N + xx] = c;
            }
        _medal.SetPixels(px); _medal.Apply();
        return _medal;
    }

    private static Texture2D _badge;
    private static Texture2D BadgeTex()   // 작은 팔각 배지(금테 + 먹색)
    {
        if (_badge != null) return _badge;
        const int N = 64;
        _badge = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int yy = 0; yy < N; yy++)
            for (int xx = 0; xx < N; xx++)
            {
                float u = (xx + 0.5f) / N * 2f - 1f, v = (yy + 0.5f) / N * 2f - 1f;
                float d = OctDist(u, v);
                Color c = Color.clear;
                if (d <= 1f)
                {
                    c = d > 0.86f ? Gold : Ink;
                    c.a *= Mathf.Clamp01((1f - d) * N * 0.5f);
                }
                px[yy * N + xx] = c;
            }
        _badge.SetPixels(px); _badge.Apply();
        return _badge;
    }

    private static Texture2D _swirl, _ring;
    private static Texture2D SwirlTex()   // 채워진 차지: 금테 원 + 민트 소용돌이
    {
        if (_swirl != null) return _swirl;
        _swirl = BuildCircle(true);
        return _swirl;
    }
    private static Texture2D RingTex()    // 빈 차지: 어두운 원 + 흐린 테
    {
        if (_ring != null) return _ring;
        _ring = BuildCircle(false);
        return _ring;
    }
    private static Texture2D BuildCircle(bool filled)
    {
        const int N = 64;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[N * N];
        for (int yy = 0; yy < N; yy++)
            for (int xx = 0; xx < N; xx++)
            {
                float u = (xx + 0.5f) / N * 2f - 1f, v = (yy + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                Color c = Color.clear;
                if (r <= 1f)
                {
                    if (r > 0.86f) c = filled ? Gold : GoldDim;                  // 테
                    else
                    {
                        c = Ink;
                        if (filled)
                        {
                            // 소용돌이: 나선 팔 2개(각도 + 반지름 위상)
                            float th = Mathf.Atan2(v, u);
                            float arm = Mathf.Sin(th * 2f + r * 7.5f);
                            if (arm > 0.25f && r < 0.78f) c = Color.Lerp(TealDark, Teal, 1f - r);
                            if (r < 0.16f) c = Teal;                             // 중심점
                        }
                    }
                    c.a *= Mathf.Clamp01((1f - r) * N * 0.5f);
                }
                px[yy * N + xx] = c;
            }
        tex.SetPixels(px); tex.Apply();
        return tex;
    }

    private static Texture2D _pip;
    private static Texture2D PipTex()   // 기울어진 평행사변형(색은 GUI.color 틴트)
    {
        if (_pip != null) return _pip;
        const int W = 40, H = 24; const float skew = 0.45f;
        _pip = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int yy = 0; yy < H; yy++)
            for (int xx = 0; xx < W; xx++)
            {
                float fy = yy / (float)(H - 1);                       // 0(위)~1(아래) — GUI에선 뒤집혀도 대칭 무관
                float off = (1f - fy) * skew * H;                     // 위쪽이 오른쪽으로 밀림
                float lx = xx - off;
                float maxW = W - skew * H;
                px[yy * W + xx] = (lx >= 0f && lx <= maxW) ? Color.white : Color.clear;
            }
        _pip.SetPixels(px); _pip.Apply();
        return _pip;
    }

    private static Texture2D _eye;
    private static Texture2D EyeTex()   // 예지 눈: 금테 아몬드 + 민트 동공
    {
        if (_eye != null) return _eye;
        const int W = 96, H = 48;
        _eye = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var px = new Color[W * H];
        for (int yy = 0; yy < H; yy++)
            for (int xx = 0; xx < W; xx++)
            {
                float u = (xx + 0.5f) / W * 2f - 1f, v = (yy + 0.5f) / H * 2f - 1f;
                // 아몬드(렌즈) 모양: 두 원호의 교집합 근사 — |v| <= (1-u^2)*0.85
                float lens = (1f - u * u) * 0.85f;
                Color c = Color.clear;
                if (Mathf.Abs(v) <= lens)
                {
                    float edge = lens - Mathf.Abs(v);
                    c = edge < 0.13f ? Gold : Ink;
                    float pr = Mathf.Sqrt(u * u * 3.2f + v * v * 1.4f);
                    if (edge >= 0.13f && pr < 0.42f) c = pr < 0.20f ? Teal : TealDark;   // 동공
                    c.a *= Mathf.Clamp01(edge * H * 0.5f + 0.35f);
                }
                px[yy * W + xx] = c;
            }
        _eye.SetPixels(px); _eye.Apply();
        return _eye;
    }
}
