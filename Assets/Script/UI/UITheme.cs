using UnityEngine;

// 전 UI 공용 색 팔레트(사이버펑크: 어두운 슬레이트 + 시안 네온). 색을 바꾸려면 여기만 수정.
public static class UITheme
{
    public static readonly Color Bg        = new Color(0.05f, 0.07f, 0.11f, 0.96f);   // 패널 배경(짙은 남청)
    public static readonly Color BgSolid   = new Color(0.06f, 0.08f, 0.12f, 1f);
    public static readonly Color Panel     = new Color(0.11f, 0.15f, 0.21f, 0.98f);   // 슬롯/밝은 칸
    public static readonly Color PanelDim  = new Color(0.08f, 0.11f, 0.16f, 0.98f);   // 비활성 칸
    public static readonly Color Accent    = new Color(0.30f, 0.80f, 0.95f);          // 시안 강조(테두리·선택·제목)
    public static readonly Color AccentDim = new Color(0.18f, 0.42f, 0.52f);          // 약한 강조
    public static readonly Color Border    = new Color(0.26f, 0.42f, 0.54f);          // 일반 테두리
    public static readonly Color Text      = new Color(0.90f, 0.95f, 1f);             // 본문
    public static readonly Color TextDim   = new Color(0.55f, 0.66f, 0.76f);          // 흐린 텍스트
    public static readonly Color Gold      = new Color(1f, 0.82f, 0.35f);             // 돈/골드 숫자
    public static readonly Color Danger    = new Color(0.95f, 0.38f, 0.42f);          // 사망/경고

    // 강조색을 알파만 바꿔 쓰기
    public static Color A(Color c, float a) { c.a = a; return c; }

    private static Texture2D _white;
    public static Texture2D White()
    {
        if (_white == null) { _white = new Texture2D(1, 1); _white.SetPixel(0, 0, Color.white); _white.Apply(); }
        return _white;
    }
    public static void Fill(Rect r, Color c) { var o = GUI.color; GUI.color = c; GUI.DrawTexture(r, White()); GUI.color = o; }
    public static void Border2(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
