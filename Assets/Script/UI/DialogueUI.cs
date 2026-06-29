using System;
using UnityEngine;

// 대화창(자동부팅·영구, OnGUI). 스타듀밸리/스컬풍 — 화면 하단 대사 박스 + 박스 안 좌상단 이름 + 박스 위(밖) 초상화.
//  · 타자기 연출. Space/F/Enter/좌클릭으로 진행(타이핑 중이면 즉시 완성 → 다 보이면 다음 줄/종료).
//  · 열려 있는 동안 Inventory.DialogueOpen=true → 플레이어 이동·공격·상호작용 잠금.
//  · DialogueUI.Show(이름, 초상화, 줄[], 완료콜백) — 마지막 줄까지 끝나면 onComplete 실행(상점/엔지니어 열기 등).
public class DialogueUI : MonoBehaviour
{
    public static DialogueUI Instance { get; private set; }
    public static bool IsOpen { get; private set; }

    private string speaker;
    private Sprite portrait;
    private string[] lines;
    private Action onComplete;
    private int index;
    private float shown;          // 현재 줄에서 보여준 글자 수(실수 — 타자 진행)
    private float openTime;
    private const float CharsPerSec = 48f;

    private Texture2D white;
    private GUIStyle nameStyle, textStyle, hintStyle;

    void Awake() { if (Instance != null && Instance != this) { Destroy(gameObject); return; } Instance = this; DontDestroyOnLoad(gameObject); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { if (Instance == null) new GameObject("DialogueUI").AddComponent<DialogueUI>(); }

    public static void Show(string speaker, Sprite portrait, string[] lines, Action onComplete = null)
    {
        if (Instance == null || lines == null || lines.Length == 0) { if (onComplete != null) onComplete(); return; }
        Instance.Begin(speaker, portrait, lines, onComplete);
    }

    private void Begin(string sp, Sprite por, string[] ln, Action done)
    {
        speaker = sp; portrait = por; lines = ln; onComplete = done;
        index = 0; shown = 0f; openTime = Time.unscaledTime;
        IsOpen = true; Inventory.DialogueOpen = true;
    }

    void Update()
    {
        if (!IsOpen) return;
        int len = lines[index].Length;
        if (shown < len) shown += CharsPerSec * Time.unscaledDeltaTime;

        if (Time.unscaledTime - openTime < 0.18f) return;   // 연 직후 입력 무시(여는 F가 첫 줄을 바로 넘기지 않게)
        bool advance = Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F)
            || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)
            || Input.GetMouseButtonDown(0);
        if (!advance) return;

        if (shown < len) { shown = len; return; }           // 타이핑 중 → 즉시 완성
        index++;
        if (index >= lines.Length) { Close(); return; }
        shown = 0f; openTime = Time.unscaledTime;            // 다음 줄도 살짝 입력가드(연타 방지)
    }

    private void Close()
    {
        IsOpen = false; Inventory.DialogueOpen = false;
        Action cb = onComplete; onComplete = null; lines = null;
        if (cb != null) cb();
    }

    void OnGUI()
    {
        if (!IsOpen) return;
        EnsureStyles();
        GUI.depth = -1500;

        float bw = Mathf.Min(Screen.width * 0.8f, 880f), bh = 168f;
        float bx = (Screen.width - bw) * 0.5f, by = Screen.height - bh - 42f;
        Rect box = new Rect(bx, by, bw, bh);

        Fill(new Rect(box.x + 4, box.y + 5, box.width, box.height), new Color(0, 0, 0, 0.35f));   // 그림자
        Fill(box, new Color(0.05f, 0.08f, 0.12f, 0.97f));
        Border(box, 2f, new Color(0.30f, 0.82f, 0.96f, 0.95f));

        // 초상화 — 박스 위 좌측(박스 밖)
        float ps = 124f;
        Rect por = new Rect(box.x + 18f, box.y - ps - 6f, ps, ps);
        Fill(new Rect(por.x + 3, por.y + 4, por.width, por.height), new Color(0, 0, 0, 0.35f));
        Fill(por, new Color(0.08f, 0.12f, 0.17f, 1f));
        DrawPortrait(new Rect(por.x + 4, por.y + 4, por.width - 8, por.height - 8));
        Border(por, 2f, new Color(0.30f, 0.82f, 0.96f, 0.95f));

        // 이름표 — 박스 안 좌상단
        Vector2 nsz = nameStyle.CalcSize(new GUIContent(speaker));
        Rect nameTag = new Rect(box.x + 16f, box.y + 12f, nsz.x + 28f, 30f);
        Fill(nameTag, new Color(0.30f, 0.82f, 0.96f, 0.95f));
        nameStyle.normal.textColor = new Color(0.04f, 0.10f, 0.14f);
        GUI.Label(nameTag, speaker, nameStyle);

        // 본문(타자기)
        string full = lines[index];
        int n = Mathf.Clamp(Mathf.FloorToInt(shown), 0, full.Length);
        textStyle.normal.textColor = new Color(0.90f, 0.96f, 1f);
        GUI.Label(new Rect(box.x + 24f, box.y + 52f, box.width - 48f, box.height - 64f), full.Substring(0, n), textStyle);

        // 진행 표시 ▼ (줄 다 보이면 깜빡)
        if (n >= full.Length && Mathf.Sin(Time.unscaledTime * 5f) > 0f)
        {
            hintStyle.normal.textColor = new Color(0.45f, 0.9f, 1f);
            GUI.Label(new Rect(box.xMax - 40f, box.yMax - 32f, 24f, 24f), "▼", hintStyle);
        }
    }

    private void DrawPortrait(Rect r)
    {
        if (portrait != null && portrait.texture != null)
        {
            Rect tr = portrait.rect; Texture t = portrait.texture;
            Rect tc = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);
            Color o = GUI.color; GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(r, t, tc);
            GUI.color = o;
        }
        else { hintStyle.normal.textColor = new Color(0.4f, 0.7f, 0.85f); GUI.Label(r, "?", hintStyle); }
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (textStyle != null) return;
        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, wordWrap = true, alignment = TextAnchor.UpperLeft };
        hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    private void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, white); GUI.color = o; }
    private void Border(Rect r, float t, Color c)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }
}
