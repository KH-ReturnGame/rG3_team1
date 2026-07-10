using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 스테이지 진입 시 지역명 표기(명일방주 엔드필드/스컬풍) — 자동부팅·영구.
//  · 씬 로드 후: 컷씬(레터박스)·씬 페이드가 끝나길 기다렸다가 상단 중앙에
//    작은 영문 서브타이틀 + 큰 지역명 + 양옆 장식선이 페이드 인 → 유지 → 페이드 아웃.
//  · 지역명은 아래 Resolve()의 표에서 수정(씬 이름 → 표기).
public class AreaTitle : MonoBehaviour
{
    public static AreaTitle Instance;

    public float fadeIn = 0.6f;
    public float hold = 2.4f;
    public float fadeOut = 0.9f;

    private string mainText, subText;
    private float alpha;
    private GUIStyle mainStyle, subStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("AreaTitle");
        Instance = go.AddComponent<AreaTitle>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }

    // 씬 이름 → (지역명, 서브타이틀). 표기를 바꾸려면 여기만 수정. (핸드북 지도 탭도 사용)
    public static bool Resolve(string scene, out string main, out string sub)
    {
        switch (scene)
        {
            case "TutorialScene": main = "무너진 통로";   sub = "COLLAPSED PASSAGE"; return true;
            case "StartingArea":  main = "지하 마을";     sub = "UNDERGROUND VILLAGE"; return true;
            case "Stage1":        main = "지하 1구역";    sub = "SUBLEVEL I";   return true;
            case "Stage2":        main = "지하 2구역";    sub = "SUBLEVEL II";  return true;
            case "Stage3":        main = "지하 3구역";    sub = "SUBLEVEL III"; return true;
            case "BossScene":     main = "심층부";        sub = "THE DEPTHS";   return true;
            case "MainMap":       main = "지하 탐사 구역"; sub = "THE UNDERGROUND"; return true;
        }
        main = sub = null; return false;
    }

    private void OnScene(Scene s, LoadSceneMode mode)
    {
        StopAllCoroutines();
        alpha = 0f;
        if (!Resolve(s.name, out mainText, out subText)) return;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        yield return null;
        // 씬 페이드인 + 컷씬(레터박스)이 끝날 때까지 대기 — 각성 연출 등과 안 겹치게
        float safety = 30f;
        yield return new WaitForSecondsRealtime(0.6f);
        while (Letterbox.Covering && safety > 0f) { safety -= Time.unscaledDeltaTime; yield return null; }
        yield return new WaitForSecondsRealtime(0.2f);

        float t = 0f;
        while (t < fadeIn) { alpha = t / fadeIn; t += Time.unscaledDeltaTime; yield return null; }
        alpha = 1f;
        yield return new WaitForSecondsRealtime(hold);
        t = 0f;
        while (t < fadeOut) { alpha = 1f - t / fadeOut; t += Time.unscaledDeltaTime; yield return null; }
        alpha = 0f;
    }

    void OnGUI()
    {
        if (alpha <= 0.01f || string.IsNullOrEmpty(mainText)) return;
        EnsureStyles();
        float sw = Screen.width, sh = Screen.height;

        mainStyle.fontSize = Mathf.RoundToInt(sh * 0.052f);
        subStyle.fontSize = Mathf.RoundToInt(sh * 0.018f);
        float cy = sh * 0.16f;

        // 서브(영문, 자간) — SetCol로 hover/active도 고정(마우스 호버에 색 안 변함)
        string spacedSub = string.Join("  ", subText.ToCharArray());
        SetCol(subStyle, new Color(0.80f, 0.62f, 0.34f, 0.85f * alpha));
        GUI.Label(new Rect(0, cy - sh * 0.032f, sw, sh * 0.03f), spacedSub, subStyle);

        // 메인(그림자 → 본문)
        Vector2 sz = mainStyle.CalcSize(new GUIContent(mainText));
        SetCol(mainStyle, new Color(0f, 0f, 0f, 0.55f * alpha));
        GUI.Label(new Rect(0 + 2f, cy + 3f, sw, sz.y), mainText, mainStyle);
        SetCol(mainStyle, new Color(0.94f, 0.92f, 0.88f, alpha));
        GUI.Label(new Rect(0, cy, sw, sz.y), mainText, mainStyle);

        // 양옆 장식선 + 다이아
        float lineY = cy + sz.y * 0.55f;
        float gap = sz.x * 0.5f + 28f;
        float lineW = Mathf.Min(sw * 0.16f, 240f);
        var lc = new Color(0.96f, 0.56f, 0.16f, 0.75f * alpha);   // 테마 오렌지
        UITheme.Fill(new Rect(sw * 0.5f - gap - lineW, lineY, lineW, 2f), lc);
        UITheme.Fill(new Rect(sw * 0.5f + gap, lineY, lineW, 2f), lc);
        UITheme.Fill(new Rect(sw * 0.5f - gap - lineW - 10f, lineY - 3f, 8f, 8f), lc);
        UITheme.Fill(new Rect(sw * 0.5f + gap + lineW + 2f, lineY - 3f, 8f, 8f), lc);
    }

    private void EnsureStyles()
    {
        if (mainStyle != null) return;
        mainStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
        subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontStyle = FontStyle.Bold };
    }

    // IMGUI 기본 스킨의 hover 색 반응 차단 — normal/hover/active를 같은 색으로
    private static void SetCol(GUIStyle st, Color c) { st.normal.textColor = c; st.hover.textColor = c; st.active.textColor = c; }
}
