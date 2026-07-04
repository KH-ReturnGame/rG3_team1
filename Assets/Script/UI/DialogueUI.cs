using System;
using UnityEngine;

// 대화창(자동부팅·영구, OnGUI). VN(비주얼노벨)풍 — 왼쪽에 큰 초상화(틀 없이 컷아웃) + 화면 하단 대사 박스(박스 안 좌상단 이름).
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

    // 초상화 자동 로드: Resources/Portraits/<화자 이름>.png 를 찾는다.
    //  → 초상화를 넣거나 바꾸려면 그 폴더에 '화자 이름과 같은 파일명'의 PNG(Sprite)만 두면 끝.
    //    (NpcDialogue의 portrait 필드로 개별 지정하면 그게 우선)
    private static readonly System.Collections.Generic.Dictionary<string, Sprite> portraitCache
        = new System.Collections.Generic.Dictionary<string, Sprite>();

    private static Sprite AutoPortrait(string speaker)
    {
        if (string.IsNullOrEmpty(speaker)) return null;
        if (portraitCache.TryGetValue(speaker, out var s)) return s;
        s = Resources.Load("Portraits/" + speaker, typeof(Sprite)) as Sprite;
        portraitCache[speaker] = s;   // 없으면 null도 캐시(매 대사마다 디스크 조회 방지)
        return s;
    }

    private void Begin(string sp, Sprite por, string[] ln, Action done)
    {
        speaker = sp; portrait = por != null ? por : AutoPortrait(sp); lines = ln; onComplete = done;
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

        float W = Screen.width, H = Screen.height;

        // ── 하단 대사 박스(넓게) ──
        float bw = Mathf.Min(W * 0.88f, 1180f), bh = 190f;
        float bx = (W - bw) * 0.5f, by = H - bh - 28f;
        Rect box = new Rect(bx, by, bw, bh);

        // 큰 초상화(왼쪽·틀 없이 컷아웃) — 박스보다 먼저 그려 박스가 앞에 오게
        DrawBigPortrait(box, W, H);

        Fill(new Rect(box.x + 4, box.y + 5, box.width, box.height), new Color(0, 0, 0, 0.35f));   // 그림자
        Fill(box, UITheme.A(UITheme.BgSolid, 0.97f));
        Border(box, 2f, UITheme.A(UITheme.Accent, 0.95f));

        // 이름표 — 박스 안 좌상단
        Vector2 nsz = nameStyle.CalcSize(new GUIContent(speaker));
        Rect nameTag = new Rect(box.x + 22f, box.y + 14f, nsz.x + 30f, 34f);
        Fill(nameTag, UITheme.A(UITheme.Accent, 0.95f));
        nameStyle.normal.textColor = new Color(0.04f, 0.10f, 0.14f);
        GUI.Label(nameTag, speaker, nameStyle);

        // 본문(타자기)
        string full = lines[index];
        int n = Mathf.Clamp(Mathf.FloorToInt(shown), 0, full.Length);
        textStyle.normal.textColor = new Color(0.90f, 0.96f, 1f);
        GUI.Label(new Rect(box.x + 28f, box.y + 60f, box.width - 56f, box.height - 74f), full.Substring(0, n), textStyle);

        // 진행 표시 ▼ (줄 다 보이면 깜빡)
        if (n >= full.Length && Mathf.Sin(Time.unscaledTime * 5f) > 0f)
        {
            hintStyle.normal.textColor = UITheme.Lighten(UITheme.Accent, 0.2f);
            GUI.Label(new Rect(box.xMax - 42f, box.yMax - 34f, 24f, 24f), "▼", hintStyle);
        }
    }

    // 왼쪽에 큰 초상화를 '틀 없이' 그린다(세로 비율 유지·바닥 정렬·컷아웃 드롭섀도).
    private void DrawBigPortrait(Rect box, float W, float H)
    {
        if (portrait == null || portrait.texture == null) return;
        Rect tr = portrait.rect; Texture t = portrait.texture;
        float aspect = tr.width / Mathf.Max(1f, tr.height);

        float maxH = H * 0.55f;              // 초상화 높이(화면 대비) — 줄이면 더 작아짐
        float maxW = W * 0.30f;              // 왼쪽 영역 폭 제한
        float ph = maxH, pw = ph * aspect;
        if (pw > maxW) { pw = maxW; ph = pw / aspect; }

        float pLeft = W * 0.05f;
        float pTop = box.yMax - ph;           // 초상화 밑 = 대사박스 밑에 맞춤
        Rect pr = new Rect(pLeft, pTop, pw, ph);
        Rect tc = new Rect(tr.x / t.width, tr.y / t.height, tr.width / t.width, tr.height / t.height);

        Color o = GUI.color;
        // 컷아웃 드롭섀도(실루엣)
        GUI.color = new Color(0f, 0f, 0f, 0.32f);
        GUI.DrawTextureWithTexCoords(new Rect(pr.x + 8f, pr.y + 10f, pr.width, pr.height), t, tc);
        // 실제 초상화
        GUI.color = Color.white;
        GUI.DrawTextureWithTexCoords(pr, t, tc);
        GUI.color = o;
    }

    private void EnsureStyles()
    {
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        if (textStyle != null) return;
        nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 21, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        textStyle = new GUIStyle(GUI.skin.label) { fontSize = 25, wordWrap = true, alignment = TextAnchor.UpperLeft };
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
