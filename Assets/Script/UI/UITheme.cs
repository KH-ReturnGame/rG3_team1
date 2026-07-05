using UnityEngine;
using System.Collections.Generic;

// 전 UI 공용 테마(사이버펑크: 어두운 건메탈 남청 + 시안 네온(주) + 앰버(부, 따뜻한 대비)).
//  · 색은 여기만 바꾸면 전 UI 반영. 그리기 헬퍼(그라데이션/소프트 그림자/패널/슬롯/글로우)로 평면 단색 탈피.
public static class UITheme
{
    // ── 팔레트 (건메탈 그레이 프레임 + 오렌지 강조 — 메이플식 참고) ──
    public static readonly Color Bg        = new Color(0.10f, 0.10f, 0.115f, 0.96f);  // (구) 단색 배경
    public static readonly Color BgSolid   = new Color(0.09f, 0.09f, 0.105f, 1f);
    public static readonly Color Panel     = new Color(0.17f, 0.175f, 0.195f, 0.98f); // (구) 단색 패널 — 하위호환
    public static readonly Color PanelDim  = new Color(0.12f, 0.125f, 0.14f, 0.98f);
    public static readonly Color PanelTop  = new Color(0.28f, 0.29f, 0.32f, 1f);      // 패널 그라데 상단(밝은 금속)
    public static readonly Color PanelBot  = new Color(0.155f, 0.16f, 0.18f, 1f);     // 패널 그라데 하단(어두운 금속)
    public static readonly Color SlotTop   = new Color(0.155f, 0.16f, 0.175f, 1f);    // 슬롯 그라데 상단(어두운 칸)
    public static readonly Color SlotBot   = new Color(0.085f, 0.088f, 0.10f, 1f);    // 슬롯 그라데 하단
    public static readonly Color Accent    = new Color(0.96f, 0.56f, 0.16f);          // 오렌지(주 강조: 선택·강조바·글로우)
    public static readonly Color AccentDim = new Color(0.46f, 0.27f, 0.09f);
    public static readonly Color Warm      = new Color(1.00f, 0.74f, 0.28f);          // 골드/앰버(부 강조: 핫키·장비 배지)
    public static readonly Color WarmDim   = new Color(0.52f, 0.37f, 0.15f);
    public static readonly Color Border    = new Color(0.42f, 0.43f, 0.47f);          // 금속 테두리(밝은 그레이)
    public static readonly Color Text      = new Color(0.90f, 0.95f, 1f);
    public static readonly Color TextDim   = new Color(0.55f, 0.66f, 0.76f);
    public static readonly Color Gold      = new Color(1f, 0.82f, 0.35f);
    public static readonly Color Good      = new Color(0.45f, 0.90f, 0.55f);          // 성공/긍정
    public static readonly Color Danger    = new Color(0.95f, 0.38f, 0.42f);

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

    // 창 패널: 그림자 + 세로 그라데 + 상단 하이라이트/강조바 + 테두리
    public static void DrawPanel(Rect r, bool accentBar = true)
    {
        Shadow(r, 18f, 0.40f);
        FillV(r, PanelTop, PanelBot);
        Fill(new Rect(r.x, r.y, r.width, 1f), A(Color.white, 0.06f));   // 상단 베벨 하이라이트
        Border2(r, 2f, Border);
        if (accentBar) Fill(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, 3f), Accent);   // 상단 시안 강조바
    }

    // 슬롯/칸: 세로 그라데 + 베벨 + 테두리(+ 호버 시 밝게)
    public static void DrawSlot(Rect r, Color border, bool hover = false, float thickness = 2f)
    {
        Color top = hover ? Color.Lerp(SlotTop, Accent, 0.16f) : SlotTop;
        FillV(r, top, SlotBot);
        Fill(new Rect(r.x, r.y, r.width, 1f), A(Color.white, 0.05f));
        Border2(r, hover ? thickness + 0.5f : thickness, hover ? Lighten(border, 0.22f) : border);
    }

    // 아이템 희귀도 링(슬롯 안쪽 얇은 테두리)
    public static void RarityRing(Rect r, Color rarity)
    {
        Border2(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, r.height - 4f), 1.5f, A(rarity, 0.85f));
    }
}
