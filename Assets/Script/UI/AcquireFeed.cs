using System.Collections.Generic;
using UnityEngine;

// 아이템 획득 알림. 무언가를 주우면 화면 우측에 "[아이콘] +N 이름"이 잠깐 떴다 사라진다.
// ItemPickup.Interact 등에서 AcquireFeed.Notify(item, count) 호출. 자동부팅 싱글톤.
public class AcquireFeed : MonoBehaviour
{
    public static AcquireFeed Instance;

    private class Entry { public ItemData item; public int count; public float t0; }
    private readonly List<Entry> entries = new List<Entry>();
    private const float Life = 2.6f;
    private GUIStyle style;
    private static Texture2D _bg;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AcquireFeed");
        Instance = go.AddComponent<AcquireFeed>();
        DontDestroyOnLoad(go);
    }

    public static void Notify(ItemData item, int count)
    {
        if (Instance == null || item == null || count <= 0) return;
        // 최근 같은 아이템이면 합쳐서 갱신(코인 연속 줍기 등)
        for (int i = Instance.entries.Count - 1; i >= 0; i--)
        {
            var e = Instance.entries[i];
            if (e.item == item && Time.unscaledTime - e.t0 < 1.3f) { e.count += count; e.t0 = Time.unscaledTime; return; }
        }
        Instance.entries.Add(new Entry { item = item, count = count, t0 = Time.unscaledTime });
        if (Instance.entries.Count > 6) Instance.entries.RemoveAt(0);
    }

    void Update()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
            if (Time.unscaledTime - entries[i].t0 > Life) entries.RemoveAt(i);
    }

    void OnGUI()
    {
        if (entries.Count == 0) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        UIScale.Apply();   // 해상도 독립 스케일

        float w = 220f, h = 32f, gap = 4f;
        float x = UIScale.W - w - 18f;
        float baseY = UIScale.H * 0.60f;

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            float age = Time.unscaledTime - e.t0;
            float a = age < Life - 0.6f ? 1f : Mathf.Clamp01((Life - age) / 0.6f);   // 끝에서 페이드아웃
            float slide = (1f - Mathf.Clamp01(age / 0.25f)) * 18f;                    // 등장 시 살짝 우→좌
            float y = baseY - (entries.Count - 1 - i) * (h + gap);

            Color prev = GUI.color;
            GUI.color = new Color(0.06f, 0.07f, 0.09f, 0.85f * a);
            GUI.DrawTexture(new Rect(x + slide, y, w, h), Bg());
            GUI.color = UITheme.A(UITheme.Accent, a);
            GUI.DrawTexture(new Rect(x + slide, y, 3f, h), Bg());                     // 좌측 강조 바

            GUI.color = new Color(1f, 1f, 1f, a);
            if (e.item.icon != null) GUI.DrawTexture(new Rect(x + slide + 8f, y + 4f, 24f, 24f), e.item.icon.texture);

            Color rc = e.item.RarityColor();
            style.normal.textColor = new Color(rc.r, rc.g, rc.b, a);
            GUI.Label(new Rect(x + slide + 40f, y, w - 46f, h), "+" + e.count + "  " + e.item.itemName, style);
            GUI.color = prev;
        }
    }

    private static Texture2D Bg() { if (_bg == null) { _bg = new Texture2D(1, 1); _bg.SetPixel(0, 0, Color.white); _bg.Apply(); } return _bg; }
}
