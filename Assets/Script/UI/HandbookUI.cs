using System.Collections.Generic;
using UnityEngine;

// 모험 핸드북 — 나인 솔즈 메뉴 오마주(풀스크린 + 장식 프레임).
//  · 상단 중앙 탭바: [Q] 지도 | 도감 | 도움말 [E] — 선택 탭은 이중 프레임, Q/E로 전환
//  · 지도: 좌상단 구역명(밑줄)+우상단 탐사율, 지도를 크게
//  · 도감(아이템): 왼쪽 번호 매긴 목록(미발견=알 수 없음) + 오른쪽 상세(브래킷 액자+등급+설명) + 하단 달성도
//  · 도움말: 왼쪽 목록 + 오른쪽 본문([키] 금색 하이라이트)
//  G 열기/닫기, M=지도 탭 바로, ESC 닫기. 자동부팅 싱글톤(Inventory.HandbookUIOpen 입력잠금).
public class HandbookUI : MonoBehaviour
{
    public static HandbookUI Instance;
    public KeyCode toggleKey = KeyCode.G;
    public KeyCode mapKey = KeyCode.M;    // 지도 탭 바로 열기/닫기

    private bool open;
    private int tab;          // 0 지도 / 1 도감 / 2 도움말
    private int sel;          // 목록 선택 인덱스
    private Vector2 scroll;
    private GUIStyle areaTitleSt, areaSubSt, tabSt, rowSt, rowNumSt, detailNameSt, detailSubSt, bodySt, dimSt, hintSt, chipSt;
    private static Texture2D _tex;

    // ── 도감 '발견' 기록(획득 시 AcquireFeed가 호출, SaveSystem이 저장/복원) ──
    private static readonly HashSet<string> seenItems = new HashSet<string>();
    public static void MarkItemSeen(ItemData item) { if (item != null) seenItems.Add(ItemDatabase.Key(item)); }
    public static List<string> SaveSeenItems() => new List<string>(seenItems);
    public static void LoadSeenItems(List<string> ids)
    {
        seenItems.Clear();
        if (ids != null) foreach (var s in ids) if (!string.IsNullOrEmpty(s)) seenItems.Add(s);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("HandbookUI");
        Instance = go.AddComponent<HandbookUI>();
        DontDestroyOnLoad(go);
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey)) { open = !open; if (open) OnOpen(); }
        if (Input.GetKeyDown(mapKey))   // [M] = 지도 탭 바로. 이미 지도 보는 중이면 닫기
        {
            if (open && tab == 0) open = false;
            else { open = true; tab = 0; sel = 0; scroll = Vector2.zero; OnOpen(); }
        }
        if (open)
        {
            if (Input.GetKeyDown(KeyCode.Escape)) open = false;
            if (Input.GetKeyDown(KeyCode.Q)) { tab = (tab + 2) % 3; sel = 0; scroll = Vector2.zero; }   // 이전 탭
            if (Input.GetKeyDown(KeyCode.E)) { tab = (tab + 1) % 3; sel = 0; scroll = Vector2.zero; }   // 다음 탭
        }
        Inventory.HandbookUIOpen = open;
    }

    // 열 때: 지금 갖고 있는 아이템들도 '발견'으로 기록
    private void OnOpen()
    {
        var inv = Inventory.Instance;
        if (inv != null) foreach (var s in inv.slots) if (s != null && !s.IsEmpty) MarkItemSeen(s.item);
        if (Equipment.Instance != null) foreach (var it in Equipment.Instance.Items()) MarkItemSeen(it);
    }

    void OnGUI()
    {
        if (!open) return;
        EnsureStyles();
        UIScale.Apply();
        float sw = UIScale.W, sh = UIScale.H;
        Vector2 m = Event.current.mousePosition;
        bool click = Event.current.type == EventType.MouseDown && Event.current.button == 0;

        // ── 풀스크린 배경 + 장식 프레임 ──
        Fill(new Rect(0, 0, sw, sh), UITheme.A(UITheme.BgSolid, 0.99f));
        float fm = Mathf.Min(sw, sh) * 0.035f;                                     // 프레임 여백
        Rect frame = new Rect(fm, fm + sh * 0.052f, sw - fm * 2f, sh - fm * 2f - sh * 0.075f);
        UITheme.Border2(frame, 1.2f, UITheme.A(UITheme.Accent, 0.55f));
        UITheme.Border2(new Rect(frame.x + 6f, frame.y + 6f, frame.width - 12f, frame.height - 12f), 1f, UITheme.A(UITheme.Accent, 0.15f));
        UITheme.Corners(frame, 22f, 3f);
        SideOrnaments(frame);

        // ── 상단 중앙 탭바: [Q] 지도 | 도감 | 도움말 [E] ──
        string[] tabs = { "지도", "도감", "도움말" };
        float tabW = Mathf.Min(sw * 0.115f, 168f), tabH = sh * 0.037f, tabGap = 12f;
        float totalW = tabs.Length * (tabW + tabGap) - tabGap;
        float tbx = (sw - totalW) * 0.5f, tby = fm * 0.55f;
        KeyChip(new Rect(tbx - 58f, tby + 2f, 40f, tabH - 4f), "Q");
        KeyChip(new Rect(tbx + totalW + 18f, tby + 2f, 40f, tabH - 4f), "E");
        for (int i = 0; i < tabs.Length; i++)
        {
            Rect tr = new Rect(tbx + i * (tabW + tabGap), tby, tabW, tabH);
            bool on = tab == i, hv = tr.Contains(m);
            if (on)
            {
                UITheme.Border2(tr, 1.4f, UITheme.A(UITheme.Accent, 0.95f));                                     // 이중 프레임(선택)
                UITheme.Border2(new Rect(tr.x + 3f, tr.y + 3f, tr.width - 6f, tr.height - 6f), 1f, UITheme.A(UITheme.Accent, 0.45f));
                Fill(new Rect(tr.x + 4f, tr.y + 4f, tr.width - 8f, tr.height - 8f), UITheme.A(UITheme.PanelTop, 0.85f));
            }
            else
            {
                Fill(new Rect(tr.x + tabW * 0.12f, tr.y, tabW * 0.76f, 1f), UITheme.A(UITheme.Accent, hv ? 0.8f : 0.4f));       // 위 헤어라인
                Fill(new Rect(tr.x + tabW * 0.12f, tr.yMax - 1f, tabW * 0.76f, 1f), UITheme.A(UITheme.Accent, hv ? 0.5f : 0.2f));
            }
            tabSt.normal.textColor = on ? new Color(0.97f, 0.95f, 0.88f) : (hv ? UITheme.Text : UITheme.TextDim);
            GUI.Label(tr, tabs[i], tabSt);
            if (click && hv) { tab = i; sel = 0; scroll = Vector2.zero; Event.current.Use(); }
        }

        // ── 콘텐츠 ──
        Rect content = new Rect(frame.x + 26f, frame.y + 20f, frame.width - 52f, frame.height - 40f);
        if (tab == 0) DrawMapTab(content);
        else if (tab == 1) DrawDexTab(content, m, click);
        else DrawHelpTab(content, m, click);

        // ── 하단 키 힌트(우하단) ──
        hintSt.normal.textColor = UITheme.A(UITheme.Accent, 0.75f);
        GUI.Label(new Rect(0, sh - fm - 4f, sw - fm - 6f, 24f), "[Q] · [E]  탭 전환      [M]  지도      [G] · [ESC]  닫기", hintSt);
    }

    // ── 지도 탭: 좌상단 구역명(밑줄) + 우상단 탐사율 + 지도 크게 ──
    private void DrawMapTab(Rect area)
    {
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        string main, sub;
        if (!AreaTitle.Resolve(scene, out main, out sub)) { main = scene; sub = ""; }

        areaTitleSt.normal.textColor = new Color(0.95f, 0.88f, 0.65f);
        GUI.Label(new Rect(area.x + 8f, area.y + 4f, area.width * 0.6f, 34f), main, areaTitleSt);
        Fill(new Rect(area.x + 10f, area.y + 40f, 84f, 2f), UITheme.A(UITheme.Accent, 0.8f));     // 제목 밑줄
        if (!string.IsNullOrEmpty(sub))
        {
            areaSubSt.normal.textColor = UITheme.TextDim;
            GUI.Label(new Rect(area.x + 10f, area.y + 46f, area.width * 0.6f, 20f), sub, areaSubSt);
        }

        var zones = CameraZone.All;
        var found = MapDiscovery.DiscoveredAreas();
        if (zones != null && zones.Count > 0 && found != null)
        {
            areaSubSt.normal.textColor = UITheme.A(UITheme.Gold, 0.9f);
            var prevAlign = areaSubSt.alignment; areaSubSt.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(area.x, area.y + 8f, area.width - 10f, 24f), "탐사  " + found.Count + " / " + zones.Count + " 구역", areaSubSt);
            areaSubSt.alignment = prevAlign;
        }

        Texture2D map = MapScanner.GetMap();
        Rect inner = new Rect(area.x, area.y + 74f, area.width, area.height - 96f);
        if (map == null)
        {
            var areas = MapDiscovery.DiscoveredAreas();
            GUI.Label(inner, (areas != null && areas.Count == 0)
                ? "아직 탐험한 구역이 없습니다.\n맵을 돌아다니면 지나온 구역이 지도에 채워집니다."
                : "이 구역에서는 지도를 만들 수 없습니다.", dimSt);
            return;
        }
        float ar = (float)map.width / Mathf.Max(1, map.height);
        float w = inner.width, h = w / ar;
        if (h > inner.height) { h = inner.height; w = h * ar; }
        Rect mr = new Rect(inner.x + (inner.width - w) * 0.5f, inner.y + (inner.height - h) * 0.5f, w, h);
        GUI.DrawTexture(mr, map, ScaleMode.ScaleToFit);

        dimSt.alignment = TextAnchor.MiddleLeft;
        GUI.Label(new Rect(area.x + 8f, area.yMax - 20f, area.width - 16f, 18f),
            "탐험한 구역만 표시 · 다음 포탈=금색 — 플레이어 위치는 보이지 않습니다.", dimSt);
        dimSt.alignment = TextAnchor.MiddleCenter;
    }

    // ── 도감 탭(아이템): 왼쪽 번호 목록 + 오른쪽 상세 + 하단 달성도 ──
    private void DrawDexTab(Rect area, Vector2 m, bool click)
    {
        var all = ItemDatabase.All();
        if (all == null || all.Count == 0) { GUI.Label(area, "도감 데이터가 없습니다.", dimSt); return; }
        sel = Mathf.Clamp(sel, 0, all.Count - 1);

        float listW = area.width * 0.36f;
        float rowH = 46f, rowGap = 8f;
        Rect listArea = new Rect(area.x, area.y + 4f, listW, area.height - 44f);
        float contentH = all.Count * (rowH + rowGap);
        scroll = GUI.BeginScrollView(listArea, scroll, new Rect(0, 0, listW - 18f, Mathf.Max(contentH, listArea.height)));
        for (int i = 0; i < all.Count; i++)
        {
            var it = all[i];
            bool known = seenItems.Contains(ItemDatabase.Key(it));
            Rect r = new Rect(2f, i * (rowH + rowGap), listW - 24f, rowH);
            bool on = sel == i;
            Vector2 lm = m - new Vector2(listArea.x, listArea.y) + scroll;   // 스크롤 보정 마우스
            bool hv = r.Contains(lm);

            Fill(r, UITheme.A(UITheme.PanelTop, on ? 0.85f : 0.35f));
            UITheme.Border2(r, on ? 1.4f : 1f, UITheme.A(UITheme.Accent, on ? 0.95f : (hv ? 0.55f : 0.28f)));
            if (on) UITheme.Border2(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), 1f, UITheme.A(UITheme.Accent, 0.4f));

            if (known && it.icon != null) GUI.DrawTexture(new Rect(r.x + 8f, r.y + 7f, rowH - 14f, rowH - 14f), it.icon.texture, ScaleMode.ScaleToFit);
            rowSt.normal.textColor = known ? (on ? new Color(0.97f, 0.95f, 0.88f) : UITheme.Text) : UITheme.TextDim;
            GUI.Label(new Rect(r.x + rowH + 2f, r.y, r.width - rowH - 40f, rowH), known ? it.itemName : "알 수 없음", rowSt);
            rowNumSt.normal.textColor = UITheme.A(UITheme.Accent, 0.6f);
            GUI.Label(new Rect(r.xMax - 38f, r.y + 4f, 32f, 18f), (i + 1).ToString("00"), rowNumSt);

            if (click && hv && listArea.Contains(m)) { sel = i; Event.current.Use(); }
        }
        GUI.EndScrollView();

        // 오른쪽 상세
        var cur = all[sel];
        bool curKnown = seenItems.Contains(ItemDatabase.Key(cur));
        Rect det = new Rect(area.x + listW + 26f, area.y + 4f, area.width - listW - 26f, area.height - 44f);
        Fill(new Rect(det.x - 13f, det.y, 1f, det.height), UITheme.A(UITheme.Border, 0.4f));   // 세로 구분선

        // 브래킷 액자 + 큰 아이콘
        float box = Mathf.Min(det.width * 0.4f, det.height * 0.45f);
        Rect img = new Rect(det.x + (det.width - box) * 0.5f, det.y + 6f, box, box);
        Fill(img, UITheme.A(UITheme.SlotBot, 0.9f));
        UITheme.Corners(img, 16f, 2.5f);
        if (curKnown && cur.icon != null)
            GUI.DrawTexture(new Rect(img.x + box * 0.14f, img.y + box * 0.14f, box * 0.72f, box * 0.72f), cur.icon.texture, ScaleMode.ScaleToFit);
        else { dimSt.fontSize = Mathf.RoundToInt(box * 0.4f); GUI.Label(img, "?", dimSt); dimSt.fontSize = 16; }

        float cy = img.yMax + 14f;
        if (curKnown)
        {
            Color rc = cur.RarityColor();
            detailNameSt.normal.textColor = rc;
            GUI.Label(new Rect(det.x, cy, det.width, 30f), cur.itemName, detailNameSt);
            detailSubSt.normal.textColor = UITheme.A(rc, 0.9f);
            var pa = detailSubSt.alignment; detailSubSt.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(det.x, cy + 4f, det.width, 20f), UITheme.RarityName(cur.rarity), detailSubSt);
            detailSubSt.alignment = pa;
            cy += 34f;
            UITheme.Divider(det.x, cy, det.width, 0.4f); cy += 10f;
            bodySt.normal.textColor = UITheme.Text;
            GUI.Label(new Rect(det.x, cy, det.width, det.yMax - cy), string.IsNullOrEmpty(cur.description) ? "…" : cur.description, bodySt);
        }
        else
        {
            detailNameSt.normal.textColor = UITheme.TextDim;
            GUI.Label(new Rect(det.x, cy, det.width, 30f), "알 수 없음", detailNameSt);
            cy += 34f;
            UITheme.Divider(det.x, cy, det.width, 0.3f); cy += 10f;
            GUI.Label(new Rect(det.x, cy, det.width, 60f), "아직 발견하지 못한 아이템입니다.\n탐험하며 손에 넣으면 기록됩니다.", dimSt);
        }

        // 하단 달성도
        int knownCount = 0;
        foreach (var it in all) if (seenItems.Contains(ItemDatabase.Key(it))) knownCount++;
        float frac = all.Count > 0 ? knownCount / (float)all.Count : 0f;
        Rect pr = new Rect(area.x + area.width * 0.22f, area.yMax - 22f, area.width * 0.5f, 8f);
        detailSubSt.normal.textColor = UITheme.A(UITheme.Gold, 0.9f);
        GUI.Label(new Rect(pr.x - 88f, pr.y - 8f, 80f, 24f), "달성도", detailSubSt);
        Fill(pr, UITheme.A(UITheme.SlotBot, 1f));
        Fill(new Rect(pr.x + 1f, pr.y + 1f, (pr.width - 2f) * frac, pr.height - 2f), UITheme.A(UITheme.Gold, 0.9f));
        UITheme.Border2(pr, 1f, UITheme.A(UITheme.Accent, 0.6f));
        GUI.Label(new Rect(pr.xMax + 10f, pr.y - 8f, 90f, 24f), Mathf.RoundToInt(frac * 100f) + " %", detailSubSt);
    }

    // ── 도움말 탭: 왼쪽 목록 + 오른쪽 본문([키] 금색) ──
    private void DrawHelpTab(Rect area, Vector2 m, bool click)
    {
        var seen = HelpPopupUI.Seen;
        if (seen == null || seen.Count == 0)
        { GUI.Label(area, "아직 본 도움말이 없습니다.\n탐험하며 도움말 구역을 지나면 여기에 기록됩니다.", dimSt); return; }
        sel = Mathf.Clamp(sel, 0, seen.Count - 1);

        float listW = area.width * 0.34f;
        float rowH = 44f, rowGap = 8f;
        for (int i = 0; i < seen.Count; i++)
        {
            Rect r = new Rect(area.x + 2f, area.y + 4f + i * (rowH + rowGap), listW - 12f, rowH);
            bool on = sel == i, hv = r.Contains(m);
            Fill(r, UITheme.A(UITheme.PanelTop, on ? 0.85f : 0.35f));
            UITheme.Border2(r, on ? 1.4f : 1f, UITheme.A(UITheme.Accent, on ? 0.95f : (hv ? 0.55f : 0.28f)));
            if (on) UITheme.Border2(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, r.height - 6f), 1f, UITheme.A(UITheme.Accent, 0.4f));
            rowSt.normal.textColor = on ? new Color(0.97f, 0.95f, 0.88f) : UITheme.Text;
            GUI.Label(new Rect(r.x + 14f, r.y, r.width - 20f, rowH), seen[i].title, rowSt);
            rowNumSt.normal.textColor = UITheme.A(UITheme.Accent, 0.6f);
            GUI.Label(new Rect(r.xMax - 38f, r.y + 4f, 32f, 18f), (i + 1).ToString("00"), rowNumSt);
            if (click && hv) { sel = i; Event.current.Use(); }
        }

        Rect det = new Rect(area.x + listW + 26f, area.y + 8f, area.width - listW - 26f, area.height - 16f);
        Fill(new Rect(det.x - 13f, det.y - 4f, 1f, det.height), UITheme.A(UITheme.Border, 0.4f));
        detailNameSt.normal.textColor = new Color(0.95f, 0.88f, 0.65f);
        GUI.Label(new Rect(det.x, det.y, det.width, 30f), seen[sel].title, detailNameSt);
        UITheme.Divider(det.x, det.y + 36f, det.width, 0.4f);
        string rich = HelpPopupUI.Rich(seen[sel].body);
        Rect br = new Rect(det.x, det.y + 48f, det.width, det.height - 48f);
        float bh = bodySt.CalcHeight(new GUIContent(rich), det.width - 18f);
        scroll = GUI.BeginScrollView(br, scroll, new Rect(0, 0, det.width - 20f, Mathf.Max(bh, br.height)));
        bodySt.normal.textColor = UITheme.Text;
        GUI.Label(new Rect(0, 0, det.width - 20f, bh), rich, bodySt);
        GUI.EndScrollView();
    }

    // ── 장식/부품 ──
    // 프레임 좌우 중앙의 각진 장식(나인 솔즈풍 짧은 대시 클러스터)
    private static void SideOrnaments(Rect f)
    {
        Color c = UITheme.A(UITheme.Accent, 0.5f);
        float cy = f.center.y;
        float[] lens = { 10f, 16f, 10f };
        for (int i = 0; i < 3; i++)
        {
            float oy = cy + (i - 1) * 10f;
            UITheme.Fill(new Rect(f.x + 10f, oy, lens[i], 2f), c);
            UITheme.Fill(new Rect(f.xMax - 10f - lens[i], oy, lens[i], 2f), c);
        }
        UITheme.Diamond(new Vector2(f.x + 10f + 22f, cy + 1f), 5f, c);
        UITheme.Diamond(new Vector2(f.xMax - 10f - 22f, cy + 1f), 5f, c);
    }

    // 키 힌트 칩([Q]/[E])
    private void KeyChip(Rect r, string key)
    {
        UITheme.FillV(r, UITheme.SlotTop, UITheme.SlotBot);
        UITheme.Border2(r, 1.2f, UITheme.A(UITheme.Accent, 0.7f));
        chipSt.normal.textColor = UITheme.A(UITheme.Accent, 0.95f);
        GUI.Label(r, key, chipSt);
    }

    private void EnsureStyles()
    {
        if (areaTitleSt != null) return;
        areaTitleSt = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
        areaSubSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        tabSt = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        tabSt.hover.textColor = tabSt.normal.textColor;
        rowSt = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        rowNumSt = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        detailNameSt = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold };
        detailSubSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
        bodySt = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft, richText = true };
        dimSt = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        dimSt.normal.textColor = UITheme.TextDim;
        hintSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
        chipSt = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    private static Texture2D Tex() { if (_tex == null) { _tex = new Texture2D(1, 1); _tex.SetPixel(0, 0, Color.white); _tex.Apply(); } return _tex; }
    private static void Fill(Rect r, Color c) { Color o = GUI.color; GUI.color = c; GUI.DrawTexture(r, Tex()); GUI.color = o; }
}
