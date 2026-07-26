using UnityEngine;

// IMGUI 전역 한글 폰트(자동부팅) — WebGL 등 브라우저 환경 대응 + 게임 톤 통일.
//  · 에디터/PC에선 OS 폰트(맑은고딕)로 폴백돼 한글이 보이지만, WebGL은 OS 폰트가 없어
//    빌드에 포함된 폰트가 아니면 한글이 전혀 렌더되지 않는다(타이틀 메뉴/슬롯 글자 소실 증상).
//  · Resources/Fonts/<FontName>을 로드해 기본 GUI 스킨 폰트로 지정 — 모든 OnGUI UI에 일괄 적용.
//  · 현재 갈무리14(픽셀/비트맵 계열, SIL OFL). 픽셀 폰트는 글리프 아틀라스를 Point 필터로 둬야
//    보간으로 흐려지지 않는다 → ApplyPixelCrisp()에서 처리.
public class UIFont : MonoBehaviour
{
    // 사용할 폰트 파일 이름(Resources/Fonts/ 안). 여기만 바꾸면 전 UI 서체가 교체된다.
    //  후보: "NotoSansKR"(기본) / "Pretendard"(모던 고딕) / "Galmuri11"·"Galmuri14"(픽셀)
    public const string FontName = "Galmuri14";

    private static Font font;

    // 런타임 교체(비교·테스트용). 실패하면 false.
    public static bool SetFont(string resourceName)
    {
        var f = Resources.Load<Font>("Fonts/" + resourceName);
        if (f == null) return false;
        font = f;
        return true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("UIFont");
        go.AddComponent<UIFont>();
        DontDestroyOnLoad(go);
        font = Resources.Load<Font>("Fonts/" + FontName);
        if (font == null) Debug.LogWarning("[UIFont] Resources/Fonts/" + FontName + " 를 찾지 못했습니다 — WebGL에서 한글이 안 보일 수 있음");
    }

    void OnGUI()
    {
        // GUI.skin은 전역 기본 스킨 — 폰트가 다르면 지정(사실상 최초 1회, 이후엔 비교만)
        if (font != null && GUI.skin.font != font) GUI.skin.font = font;
        ApplyPixelCrisp();
    }

    // 픽셀 폰트 선명화: 동적 폰트는 글리프를 아틀라스에 굽는데, 그 텍스처가 Bilinear면 뭉개진다.
    //  아틀라스는 글자가 늘어나면 재생성되므로 매 프레임 싸게 확인만 하고 바뀌었을 때만 적용.
    private static Texture lastAtlas;
    private static void ApplyPixelCrisp()
    {
        if (font == null || font.material == null) return;
        var tex = font.material.mainTexture;
        if (tex == null || tex == lastAtlas) return;
        lastAtlas = tex;
        tex.filterMode = FilterMode.Point;
    }
}
