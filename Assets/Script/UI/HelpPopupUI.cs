using System.Collections.Generic;
using UnityEngine;

// 도움말 시스템(자동부팅·영구) — 젤다 야숨식 '카드' 리워크.
//  · 카드: 화면을 어둡게 가리고 **시간을 멈춘 뒤**, 인벤토리만 한 창에 [제목+설명(위)] + [시연 GIF(아래)]를 보여준다.
//    [F]/[Space]/[ESC]/클릭으로 확인·닫기. 여러 개가 겹치면 큐로 하나씩. 컷씬(레터박스) 중엔 대기.
//  · GIF: Assets/Resources/Help/<id>/ 폴더에 프레임 PNG들(000.png, 001.png … 이름순)을 넣으면 자동 재생(기본 10fps).
//    폴더가 없으면 "시연 준비 중" 자리 표시 — 지금은 GIF 없이 텍스트만으로 동작.
//  · 스티키 배너: 각성 패링 큐 같은 '실시간 반응 유도'는 모달이면 안 되므로 상단 작은 배너로 유지(ForceHide로 닫음).
//  · 본 카드는 Seen에 기록되어 핸드북 도움말 탭에서 다시 볼 수 있다.
public class HelpPopupUI : MonoBehaviour
{
    public static HelpPopupUI Instance;

    // ── 카드(모달) ── 한 카드 = 1개 이상의 페이지(◀ n/m ▶로 넘김)
    public struct HelpPage
    {
        public string id, title, body;
        public HelpPage(string id, string title, string body) { this.id = id; this.title = title; this.body = body; }
    }
    private class Card { public HelpPage[] pages; public string[] rich; public bool force; }
    private readonly Queue<Card> queue = new Queue<Card>();
    private Card cur;
    private int page;                   // 현재 페이지 인덱스
    private float shownAt;
    private float prevTimeScale = 1f;
    private Sprite[] gifFrames;
    public float gifFps = 10f;          // GIF 재생 속도(프레임/초)
    public float confirmDelay = 0.35f;  // 이 시간 전엔 닫기 입력 무시(오입력 방지)

    // 전투 중 억제: 강제(force)가 아닌 카드는 근처 적이 교전 중이면 대기했다가 전투가 끝나면 표시
    public float combatRadius = 11f;    // 이 반경 안의 어그로 적 = 전투 중
    private float combatCheckAt;
    private bool combatBlocked;

    // ── 스티키 배너(패링 큐 등 실시간 유도 — 모달 아님) ──
    private string stickyTitle, stickyBody, stickyRich;
    private float stickyAt;

    private GUIStyle titleSt, bodySt, tagSt, hintSt, phSt, stTitleSt, stBodySt;
    private static Texture2D _tex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HelpPopupUI");
        Instance = go.AddComponent<HelpPopupUI>();
        DontDestroyOnLoad(go);
    }

    // 지나간 도움말 기록(핸드북 다시보기 — GIF id 포함). 제목 기준 중복 제거. SaveSystem이 저장/복원.
    public class HelpEntry { public string title; public string body; public string id; }
    public static readonly List<HelpEntry> Seen = new List<HelpEntry>();
    private static void Record(string id, string t, string b)
    {
        if (string.IsNullOrEmpty(t)) return;
        foreach (var e in Seen) if (e.title == t) return;
        Seen.Add(new HelpEntry { title = t, body = b, id = id });
    }

    // GIF 프레임 로더(카드·핸드북 공용): Resources/Help/<id>/ 프레임 PNG들을 이름순 정렬해 반환(없으면 null)
    public static Sprite[] LoadGifFrames(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        var frames = Resources.LoadAll<Sprite>("Help/" + id);
        if (frames == null || frames.Length == 0) return null;
        System.Array.Sort(frames, (a, b) => a.name.Length != b.name.Length ? a.name.Length - b.name.Length : string.CompareOrdinal(a.name, b.name));
        return frames;
    }

    // ── 발동 API ──
    // 카드 등록. id = GIF 폴더 이름(Resources/Help/<id>/) — 없거나 null이면 텍스트만.
    public void Show(string id, string title, string body) => ShowPages(false, new HelpPage(id, title, body));
    // 이미 본 적 있으면(Seen — 세이브 연동) 다시 안 띄우는 1회성 카드
    public void ShowOnce(string id, string title, string body)
    {
        foreach (var e in Seen) if (e.title == title) return;
        Show(id, title, body);
    }
    // 여러 페이지 카드. force=true: 전투 중 억제를 무시하고 즉시(스크립트 연출 모멘트용)
    public void ShowPages(bool force, params HelpPage[] pages)
    {
        if (pages == null || pages.Length == 0) return;
        string first = pages[0].title;
        foreach (var c in queue) if (c.pages[0].title == first) return;   // 같은 카드 중복 큐 방지
        if (cur != null && cur.pages[0].title == first) return;
        var rich = new string[pages.Length];
        for (int i = 0; i < pages.Length; i++) { rich[i] = Rich(pages[i].body); Record(pages[i].id, pages[i].title, pages[i].body); }
        queue.Enqueue(new Card { pages = pages, rich = rich, force = force });
    }
    // (구) 호환 — 이제 전부 모달 카드
    public void ShowTimed(string t, string b, float duration) => Show(null, t, b);
    public void ShowManual(string t, string b) => Show(null, t, b);

    // 스티키 배너(기록 X, ForceHide로만 닫음)
    public void ShowSticky(string t, string b) { stickyTitle = t; stickyBody = b; stickyRich = Rich(b); stickyAt = Time.unscaledTime; }
    public void ForceHide() { stickyTitle = null; stickyBody = null; }

    public static bool CardOpen => Instance != null && Instance.cur != null;
    public static bool ManualOpen => CardOpen;   // (구 이름 호환 — MenuUI의 ESC 게이트가 사용)
    public bool IsManualOpen => cur != null;

    // [키]와 *강조*를 금색 볼드로(richText). 핸드북(Seen)엔 원문이 남는다.
    public static string Rich(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        string hex = "#" + ColorUtility.ToHtmlStringRGB(UITheme.Accent);
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\[[^\]\n]+\]", m => "<b><color=" + hex + ">" + m.Value + "</color></b>");
        s = System.Text.RegularExpressions.Regex.Replace(s, @"\*([^*\n]+)\*", m => "<b><color=" + hex + ">" + m.Groups[1].Value + "</color></b>");
        return s;
    }

    void Update()
    {
        // 다음 카드 오픈(컷씬 중엔 대기, 강제가 아니면 전투 중에도 대기)
        if (cur == null && queue.Count > 0 && !Letterbox.Covering)
        {
            var next = queue.Peek();
            if (next.force || !CombatNearby()) OpenNext();
        }

        // 페이지/닫기 입력
        if (cur != null && Time.unscaledTime - shownAt > confirmDelay)
        {
            int n = cur.pages.Length;
            if (n > 1)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) SetPage(page - 1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) SetPage(page + 1);
            }
            // 아무 데나 클릭 / F / Space / Enter = 다음 페이지, 마지막 페이지면 닫기(뒤로가기는 ←/A 키)
            bool click = Input.GetMouseButtonDown(0);
            if (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || click)
            {
                if (page < n - 1) SetPage(page + 1);   // 마지막 페이지 전이면 다음 장으로
                else CloseCard();
            }
            if (Input.GetKeyDown(KeyCode.Escape)) CloseCard();
        }
    }

    // 근처(플레이어 기준 combatRadius)에 어그로 상태 적이 있으면 전투 중 — 0.35초 간격으로만 검사
    private bool CombatNearby()
    {
        if (Time.unscaledTime - combatCheckAt < 0.35f) return combatBlocked;
        combatCheckAt = Time.unscaledTime;
        combatBlocked = false;
        var pc = PlayerController.Instance;
        if (pc == null) return false;
        foreach (var e in FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            if (e != null && e.IsAggro && Vector2.Distance(e.transform.position, pc.transform.position) <= combatRadius)
            { combatBlocked = true; break; }
        return combatBlocked;
    }

    private void SetPage(int p)
    {
        int np = Mathf.Clamp(p, 0, cur.pages.Length - 1);
        if (np == page) return;
        page = np;
        gifFrames = LoadGifFrames(cur.pages[page].id);
        AudioManager.Sfx("ui_move", 0.7f);
    }

    private void OpenNext()
    {
        cur = queue.Dequeue();
        page = 0;
        shownAt = Time.unscaledTime;
        prevTimeScale = Time.timeScale > 0.01f ? Time.timeScale : 1f;
        Time.timeScale = 0f;                 // ★야숨식 — 세상이 멈춘다
        Inventory.HelpOpen = true;
        AudioManager.Sfx("help_open");

        gifFrames = LoadGifFrames(cur.pages[0].id);   // 이름순 정렬 — 000.png, 001.png … 권장
    }

    private void CloseCard()
    {
        Time.timeScale = prevTimeScale;
        cur = null;
        gifFrames = null;
        Inventory.HelpOpen = false;
    }

    void OnGUI()
    {
        DrawSticky();
        if (cur == null) return;
        EnsureStyles();
        UIScale.Apply();
        GUI.depth = -1200;
        float sw = UIScale.W, sh = UIScale.H;
        float k = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.22f);
        float ease = 1f - (1f - k) * (1f - k);

        // 딤(화면 가리기)
        UITheme.Fill(new Rect(0, 0, sw, sh), new Color(0f, 0f, 0f, 0.72f * ease));

        // 카드(인벤토리 UI 정도 크기) — 위 텍스트 / 아래 GIF
        float w = Mathf.Min(sw * 0.46f, 700f);
        float h = Mathf.Min(sh * 0.74f, 720f);
        float x = (sw - w) * 0.5f, y = (sh - h) * 0.5f - 14f * (1f - ease);
        Rect card = new Rect(x, y, w, h);
        var prevC = GUI.color; GUI.color = new Color(1f, 1f, 1f, ease);
        UITheme.DrawPanel(card);
        int pageCount = cur.pages.Length;
        string title = cur.pages[page].title;
        string rich = cur.rich[page];
        float headH = UITheme.DrawHeader(card, title, "도움말", 20f, 44f);

        // 본문(위)
        float pad = 26f;
        float bodyW = w - pad * 2f;
        bodySt.fontSize = Mathf.RoundToInt(Mathf.Clamp(sh * 0.017f, 15f, 19f));
        float bodyH = bodySt.CalcHeight(new GUIContent(rich), bodyW);
        float bodyMaxH = h * 0.42f;
        Rect bodyR = new Rect(x + pad, y + headH + 12f, bodyW, Mathf.Min(bodyH, bodyMaxH));
        bodySt.normal.textColor = UITheme.Text;
        GUI.Label(bodyR, rich, bodySt);

        // GIF(아래) — 브래킷 액자
        float gy = y + headH + 12f + Mathf.Min(bodyH, bodyMaxH) + 14f;
        Rect gif = new Rect(x + pad, gy, w - pad * 2f, card.yMax - gy - 46f);
        UITheme.Fill(gif, UITheme.A(UITheme.SlotBot, 0.95f));
        UITheme.Corners(gif, 16f, 2.5f);
        if (gifFrames != null && gif.height > 40f)
        {
            var f = gifFrames[(int)(Time.unscaledTime * Mathf.Max(1f, gifFps)) % gifFrames.Length];
            // 비율 유지로 액자 안에 맞춤
            float ar = f.rect.width / Mathf.Max(1f, f.rect.height);
            float fw = gif.width - 16f, fh = fw / ar;
            if (fh > gif.height - 16f) { fh = gif.height - 16f; fw = fh * ar; }
            Rect fr = new Rect(gif.x + (gif.width - fw) * 0.5f, gif.y + (gif.height - fh) * 0.5f, fw, fh);
            GUI.DrawTextureWithTexCoords(fr, f.texture, new Rect(
                f.rect.x / f.texture.width, f.rect.y / f.texture.height,
                f.rect.width / f.texture.width, f.rect.height / f.texture.height));
        }
        else if (gif.height > 40f)
        {
            // GIF 준비 전 자리 표시
            UITheme.Diamond(new Vector2(gif.center.x, gif.center.y - 16f), 12f, UITheme.A(UITheme.Accent, 0.6f));
            phSt.normal.textColor = UITheme.TextDim;
            GUI.Label(new Rect(gif.x, gif.center.y - 4f, gif.width, 26f), "시연 영상 준비 중", phSt);
        }

        // 하단: 페이저(◀ n/m ▶) + 확인 힌트
        if (Time.unscaledTime - shownAt > confirmDelay)
        {
            float footY = card.yMax - 38f;
            if (pageCount > 1)
            {
                // ◀ n/m ▶ — 화살표는 클릭 가능(마우스), ←/→·A/D로도 넘김
                hintSt.normal.textColor = UITheme.Text;
                GUI.Label(new Rect(x, footY, w, 26f), (page + 1) + " / " + pageCount, hintSt);
                Rect lArrow = new Rect(x + w * 0.5f - 86f, footY - 4f, 44f, 32f);
                Rect rArrow = new Rect(x + w * 0.5f + 42f, footY - 4f, 44f, 32f);
                bool canL = page > 0, canR = page < pageCount - 1;
                hintSt.normal.textColor = canL ? UITheme.Accent : UITheme.A(UITheme.TextDim, 0.4f);
                GUI.Label(lArrow, "◀", hintSt);
                hintSt.normal.textColor = canR ? UITheme.A(UITheme.Accent, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f)) : UITheme.A(UITheme.TextDim, 0.4f);
                GUI.Label(rArrow, "▶", hintSt);
                // 화살표 클릭은 Update의 '아무 데나 클릭=다음'과 겹치므로 두지 않음(뒤로가기는 ←/A 키)
                // 마지막 페이지에서만 확인 힌트
                if (!canR)
                {
                    hintSt.normal.textColor = UITheme.A(UITheme.Accent, 0.55f);
                    GUI.Label(new Rect(x, footY - 24f, w, 22f), "[F] 확인", hintSt);
                }
            }
            else
            {
                hintSt.normal.textColor = UITheme.A(UITheme.Accent, 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 4f));
                GUI.Label(new Rect(x, footY, w, 26f), "[F] 확인", hintSt);
            }
        }
        GUI.color = prevC;
    }

    // 스티키 배너(상단 중앙 작은 패널 — 각성 패링 큐 등)
    private void DrawSticky()
    {
        if (string.IsNullOrEmpty(stickyTitle)) return;
        if (Letterbox.Covering) return;
        EnsureStyles();
        UIScale.Apply();
        float sw = UIScale.W;
        float a = Mathf.Clamp01((Time.unscaledTime - stickyAt) / 0.2f);

        float w = Mathf.Min(680f, sw - 60f);
        stBodySt.fontSize = 16;
        float bh = stBodySt.CalcHeight(new GUIContent(stickyRich), w - 44f);
        float h = 44f + bh + 16f;
        float x = (sw - w) * 0.5f, y = UIScale.H * 0.06f;
        Rect r = new Rect(x, y, w, h);
        UITheme.Shadow(r, 10f, 0.35f * a);
        UITheme.FillV(r, UITheme.A(UITheme.PanelTop, 0.96f * a), UITheme.A(UITheme.PanelBot, 0.96f * a));
        UITheme.Border2(r, 1.2f, UITheme.A(UITheme.Accent, 0.85f * a));
        UITheme.Corners(r, 10f, 2f);
        stTitleSt.normal.textColor = new Color(0.97f, 0.95f, 0.88f, a);
        GUI.Label(new Rect(x + 20f, y + 8f, w - 40f, 28f), stickyTitle, stTitleSt);
        stBodySt.normal.textColor = new Color(0.90f, 0.91f, 0.94f, a);
        GUI.Label(new Rect(x + 22f, y + 40f, w - 44f, bh + 4f), stickyRich, stBodySt);
    }

    private void EnsureStyles()
    {
        if (bodySt != null) return;
        titleSt = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        bodySt = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
        tagSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        hintSt = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        phSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        stTitleSt = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        stBodySt = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
    }

    private static Texture2D Tex() { if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); } return _tex; }
}
