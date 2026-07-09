using UnityEngine;
using System.Collections.Generic;

// 전 UI 공용 테마(사이버펑크: 어두운 건메탈 남청 + 시안 네온(주) + 앰버(부, 따뜻한 대비)).
//  · 색은 여기만 바꾸면 전 UI 반영. 그리기 헬퍼(그라데이션/소프트 그림자/패널/슬롯/글로우)로 평면 단색 탈피.
public static class UITheme
{
    // ── 팔레트 (먹색 청록 + 금테 + 민트 — 나인 솔즈풍, HUD(StatUI)와 동일 톤) ──
    public static readonly Color Bg        = new Color(0.035f, 0.055f, 0.055f, 0.96f); // (구) 단색 배경
    public static readonly Color BgSolid   = new Color(0.030f, 0.050f, 0.050f, 1f);
    public static readonly Color Panel     = new Color(0.075f, 0.110f, 0.105f, 0.98f); // (구) 단색 패널 — 하위호환
    public static readonly Color PanelDim  = new Color(0.045f, 0.070f, 0.068f, 0.98f);
    public static readonly Color PanelTop  = new Color(0.105f, 0.150f, 0.145f, 1f);    // 패널 그라데 상단(짙은 청록 먹색)
    public static readonly Color PanelBot  = new Color(0.048f, 0.078f, 0.075f, 1f);    // 패널 그라데 하단
    public static readonly Color SlotTop   = new Color(0.070f, 0.100f, 0.096f, 1f);    // 슬롯 그라데 상단(어두운 칸)
    public static readonly Color SlotBot   = new Color(0.032f, 0.050f, 0.048f, 1f);    // 슬롯 그라데 하단
    public static readonly Color Accent    = new Color(0.84f, 0.76f, 0.54f);           // 금테(주 강조: 선택·프레임·헤더)
    public static readonly Color AccentDim = new Color(0.40f, 0.36f, 0.25f);
    public static readonly Color Warm      = new Color(1.00f, 0.85f, 0.48f);           // 밝은 골드(부 강조: 핫키·배지)
    public static readonly Color WarmDim   = new Color(0.50f, 0.43f, 0.24f);
    public static readonly Color Border    = new Color(0.46f, 0.42f, 0.30f);           // 흐린 금테(구분선·기본 테두리)
    public static readonly Color Text      = new Color(0.91f, 0.89f, 0.80f);           // 크림
    public static readonly Color TextDim   = new Color(0.56f, 0.54f, 0.45f);
    public static readonly Color Gold      = new Color(1f, 0.85f, 0.45f);
    public static readonly Color Good      = new Color(0.34f, 0.93f, 0.72f);           // 민트(성공/긍정/게이지 채움)
    public static readonly Color Danger    = new Color(0.90f, 0.30f, 0.28f);

    // 알파만 바꿔 재사용
    public static Color A(Color c, float a) { c.a = a; return c; }
    // 밝게(하이라이트/호버)
    public static Color Lighten(Color c, float amt) => new Color(Mathf.Min(1f, c.r + amt), Mathf.Min(1f, c.g + amt), Mathf.Min(1f, c.b + amt), c.a);

    // ── 텍스처 ──
    private static Texture2D _white;
    public static Texture2D White()
    {
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        return _white;
    }

    private static readonly Dictionary<int, Texture2D> _grads = new Dictionary<int, Texture2D>();
    // 세로 그라데이션 텍스처(위=top, 아래=bot). GUI는 텍스처 상단이 rect 상단.
    private static Texture2D VGrad(Color top, Color bot)
    {
        int key = top.GetHashCode() * 397 ^ bot.GetHashCode();
        if (_grads.TryGetValue(key, out var t) && t != null) return t;
        const int H = 64;
        t = new Texture2D(1, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[H];
        for (int i = 0; i < H; i++) { float f = i / (float)(H - 1); px[i] = Color.Lerp(bot, top, f); }  // row0=bot(아래), rowH-1=top(위)
        t.SetPixels(px); t.Apply();
        _grads[key] = t; return t;
    }

    private static Texture2D _soft;
    // 소프트 박스(가장자리 페더 알파) — 그림자/글로우 공용
    private static Texture2D SoftBox()
    {
        if (_soft != null) return _soft;
        const int N = 64;
        _soft = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float fx = Mathf.Min(x, N - 1 - x) / (N * 0.5f);   // 0(가장자리)~1(중앙)
                float fy = Mathf.Min(y, N - 1 - y) / (N * 0.5f);
                float e = Mathf.Clamp01(Mathf.Min(fx, fy) / 0.5f); // 25% 페더
                float a = e * e * (3f - 2f * e);
                px[y * N + x] = new Color(1f, 1f, 1f, a);
            }
        _soft.SetPixels(px); _soft.Apply(); return _soft;
    }

    // ── 그리기 ──
    public static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, White()); GUI.color = o; }
    public static void FillV(Rect r, Color top, Color bot) { var o = GUI.color; GUI.color = Color.white; GUI.DrawTexture(r, VGrad(top, bot)); GUI.color = o; }

    public static void Border2(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // 소프트 드롭 그림자(패널 뒤)
    public static void Shadow(Rect r, float grow = 16f, float alpha = 0.38f)
    {
        var o = GUI.color; GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(r.x - grow, r.y - grow + 8f, r.width + grow * 2f, r.height + grow * 2f), SoftBox());
        GUI.color = o;
    }

    // 발광(선택/중요 강조) — 색 글로우
    public static void Glow(Rect r, Color c, float grow = 7f, float alpha = 0.38f)
    {
        var o = GUI.color; GUI.color = new Color(c.r, c.g, c.b, alpha);
        GUI.DrawTexture(new Rect(r.x - grow, r.y - grow, r.width + grow * 2f, r.height + grow * 2f), SoftBox());
        GUI.color = o;
    }

    // 창 패널(고급 프레임): 그림자 + 먹색 그라데 + 얇은 금테 + 안쪽 이중 헤어라인 + 금색 코너 브래킷
    public static void DrawPanel(Rect r, bool accentBar = true)
    {
        Shadow(r, 18f, 0.45f);
        FillV(r, PanelTop, PanelBot);
        Fill(new Rect(r.x, r.y, r.width, 1f), A(Color.white, 0.05f));                  // 상단 베벨 하이라이트
        Border2(r, 1.5f, A(Accent, 0.85f));                                            // 외곽 금테
        Border2(new Rect(r.x + 5f, r.y + 5f, r.width - 10f, r.height - 10f), 1f, A(Accent, 0.22f));   // 안쪽 이중 프레임
        if (accentBar) Corners(r);                                                     // 코너 브래킷
    }

    // 작은 다이아(◆) — 45도 회전 사각형(장식 공용)
    public static void Diamond(Vector2 center, float size, Color c)
    {
        var m = GUI.matrix;
        GUIUtility.RotateAroundPivot(45f, center);
        Fill(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), c);
        GUI.matrix = m;
    }

    // 장식 구분선: 금색 헤어라인 + 양끝 다이아(고급 프레임 문법)
    public static void Divider(float x, float y, float w, float alpha = 0.55f)
    {
        Fill(new Rect(x + w * 0.045f, y, w * 0.91f, 1f), A(Accent, alpha * 0.6f));
        Diamond(new Vector2(x + w * 0.02f, y + 0.5f), 5f, A(Accent, alpha));
        Diamond(new Vector2(x + w * 0.98f, y + 0.5f), 5f, A(Accent, alpha));
    }

    // 금색 코너 브래킷(모서리 ㄱ자 캡 — 프레임 장식)
    public static void Corners(Rect r, float len = 16f, float t = 3f)
    {
        Color c = A(Accent, 0.95f);
        Fill(new Rect(r.x, r.y, len, t), c);              Fill(new Rect(r.x, r.y, t, len), c);              // 좌상
        Fill(new Rect(r.xMax - len, r.y, len, t), c);     Fill(new Rect(r.xMax - t, r.y, t, len), c);       // 우상
        Fill(new Rect(r.x, r.yMax - t, len, t), c);       Fill(new Rect(r.x, r.yMax - len, t, len), c);     // 좌하
        Fill(new Rect(r.xMax - len, r.yMax - t, len, t), c); Fill(new Rect(r.xMax - t, r.yMax - len, t, len), c); // 우하
    }

    // 슬롯/칸: 세로 그라데 + 베벨 + 테두리(+ 호버 시 밝게)
    public static void DrawSlot(Rect r, Color border, bool hover = false, float thickness = 2f)
    {
        Color top = hover ? Color.Lerp(SlotTop, Accent, 0.16f) : SlotTop;
        FillV(r, top, SlotBot);
        Fill(new Rect(r.x, r.y, r.width, 1f), A(Color.white, 0.05f));
        Border2(r, hover ? thickness + 0.5f : thickness, hover ? Lighten(border, 0.22f) : border);
    }

    // 아이템 희귀도 테두리(등급별 차등) — 등급이 오를수록 장식이 한 겹씩 쌓인다.
    //  Common: 옅은 링만(조용하게) / Uncommon: 링+대각 캡 / Rare: +4코너 캡+글로우
    //  Epic: +안쪽 이중 링+상단 다이아 / Legendary: +숨쉬는 펄스 글로우+하단 다이아
    public static void RarityRing(Rect r, ItemData item)
    {
        if (item == null) return;
        Color c = item.RarityColor();
        int tier = (int)item.rarity;
        Rect rr = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);

        if (tier <= 0) { Border2(rr, 1f, A(c, 0.28f)); return; }   // Common — 존재감 낮게

        Border2(rr, 1.2f, A(c, 0.78f));
        float len = Mathf.Clamp(rr.width * 0.24f, 5f, 10f);
        Color cc = A(c, 1f);
        Fill(new Rect(rr.x, rr.y, len, 2f), cc);                  Fill(new Rect(rr.x, rr.y, 2f, len), cc);                  // 좌상 캡
        Fill(new Rect(rr.xMax - len, rr.yMax - 2f, len, 2f), cc); Fill(new Rect(rr.xMax - 2f, rr.yMax - len, 2f, len), cc); // 우하 캡

        if (tier >= 2)   // Rare+: 나머지 두 코너 캡 + 은은한 글로우
        {
            Fill(new Rect(rr.xMax - len, rr.y, len, 2f), cc);     Fill(new Rect(rr.xMax - 2f, rr.y, 2f, len), cc);
            Fill(new Rect(rr.x, rr.yMax - 2f, len, 2f), cc);      Fill(new Rect(rr.x, rr.yMax - len, 2f, len), cc);
            Glow(rr, c, 4f, 0.10f);
        }
        if (tier >= 3)   // Epic+: 안쪽 이중 링 + 상단 중앙 다이아
        {
            Border2(new Rect(rr.x + 3f, rr.y + 3f, rr.width - 6f, rr.height - 6f), 1f, A(c, 0.30f));
            Diamond(new Vector2(rr.center.x, rr.y + 1f), 6f, cc);
        }
        if (tier >= 4)   // Legendary: 숨쉬는 글로우 펄스 + 하단 다이아
        {
            float k = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
            Glow(rr, c, 6f + 3f * k, 0.10f + 0.10f * k);
            Diamond(new Vector2(rr.center.x, rr.yMax - 1f), 6f, cc);
        }
    }

    // 희귀도 한글 등급명(툴팁 표기)
    public static string RarityName(ItemData.Rarity r)
    {
        switch (r)
        {
            case ItemData.Rarity.Uncommon:  return "고급";
            case ItemData.Rarity.Rare:      return "희귀";
            case ItemData.Rarity.Epic:      return "에픽";
            case ItemData.Rarity.Legendary: return "전설";
            default:                        return "일반";
        }
    }

    // 아이템 툴팁 프레임(고급): 그림자 + 먹색 그라데 + 금테 + 상단 희귀도 라인 + 희귀도 코너 캡(링과 같은 문법)
    public static void TipFrame(Rect r, Color rarity)
    {
        Shadow(r, 12f, 0.40f);
        FillV(r, PanelTop, PanelBot);
        Fill(new Rect(r.x, r.y, r.width, 1f), A(Color.white, 0.05f));
        Border2(r, 1.2f, A(Accent, 0.8f));                                       // 금테
        Fill(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, 3f), A(rarity, 0.95f));  // 상단 희귀도 라인
        float len = 10f; Color c = A(rarity, 0.95f);
        Fill(new Rect(r.x, r.y, len, 2f), c);                Fill(new Rect(r.x, r.y, 2f, len), c);                // 좌상 캡
        Fill(new Rect(r.xMax - len, r.yMax - 2f, len, 2f), c); Fill(new Rect(r.xMax - 2f, r.yMax - len, 2f, len), c); // 우하 캡
    }

    // (구) 색만 받는 버전 — 호환용(Uncommon급 기본 장식)
    public static void RarityRing(Rect r, Color rarity)
    {
        Rect rr = new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f);
        Border2(rr, 1.2f, A(rarity, 0.75f));
        float len = Mathf.Clamp(rr.width * 0.24f, 5f, 10f);
        Color c = A(rarity, 1f);
        Fill(new Rect(rr.x, rr.y, len, 2f), c);              Fill(new Rect(rr.x, rr.y, 2f, len), c);
        Fill(new Rect(rr.xMax - len, rr.yMax - 2f, len, 2f), c); Fill(new Rect(rr.xMax - 2f, rr.yMax - len, 2f, len), c);
    }

    // ── 통일 헤더: ▌◆ 태그 제목 ────(헤어라인) — 전 창(인벤/상점/제작/게시판/핸드북/도움말) 공통 문법 ──
    //  반환값 = 헤더가 차지한 높이(패널 상단 기준 콘텐츠 시작 오프셋).
    private static GUIStyle _headTitle, _headTag;
    public static float DrawHeader(Rect panel, string title, string tag = null, float pad = 20f, float headH = 46f)
    {
        if (_headTitle == null)
        {
            _headTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            _headTag = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        }
        _headTitle.fontSize = Mathf.RoundToInt(headH * 0.52f);
        _headTag.fontSize = Mathf.Max(11, Mathf.RoundToInt(headH * 0.31f));

        float y = panel.y + 6f;
        Fill(new Rect(panel.x, y + headH * 0.14f, 4f, headH * 0.72f), Accent);        // 왼쪽 오렌지 바

        var mtx = GUI.matrix;                                                          // ◆ 다이아
        float ds = Mathf.Max(7f, headH * 0.19f);
        Vector2 dc = new Vector2(panel.x + pad + ds * 0.5f, y + headH * 0.5f - 1f);
        GUIUtility.RotateAroundPivot(45f, dc);
        Fill(new Rect(dc.x - ds * 0.5f, dc.y - ds * 0.5f, ds, ds), Accent);
        GUI.matrix = mtx;

        float tx = panel.x + pad + ds + 12f;
        if (!string.IsNullOrEmpty(tag))
        {
            _headTag.normal.textColor = _headTag.hover.textColor = A(Accent, 0.95f);
            float tw = _headTag.CalcSize(new GUIContent(tag)).x;
            GUI.Label(new Rect(tx, y, tw + 4f, headH - 4f), tag, _headTag);
            tx += tw + 14f;
        }
        _headTitle.normal.textColor = _headTitle.hover.textColor = new Color(0.97f, 0.96f, 0.94f);
        GUI.Label(new Rect(tx, y - 2f, panel.xMax - pad - tx, headH), title, _headTitle);
        Divider(panel.x + pad, y + headH - 3f, panel.width - pad * 2f);   // 장식 구분선(양끝 다이아)
        return (y + headH) - panel.y;
    }
}
