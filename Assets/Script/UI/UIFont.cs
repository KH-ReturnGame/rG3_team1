using UnityEngine;

// IMGUI 전역 한글 폰트(자동부팅) — WebGL 등 브라우저 환경 대응.
//  · 에디터/PC에선 OS 폰트(맑은고딕)로 폴백돼 한글이 보이지만, WebGL은 OS 폰트가 없어
//    빌드에 포함된 폰트가 아니면 한글이 전혀 렌더되지 않는다(타이틀 메뉴/슬롯 글자 소실 증상).
//  · Resources/Fonts/NotoSansKR.ttf 를 로드해 기본 GUI 스킨 폰트로 지정 — 모든 OnGUI UI에 일괄 적용.
public class UIFont : MonoBehaviour
{
    private static Font font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("UIFont");
        go.AddComponent<UIFont>();
        DontDestroyOnLoad(go);
        font = Resources.Load<Font>("Fonts/NotoSansKR");
        if (font == null) Debug.LogWarning("[UIFont] Resources/Fonts/NotoSansKR.ttf 를 찾지 못했습니다 — WebGL에서 한글이 안 보일 수 있음");
    }

    void OnGUI()
    {
        // GUI.skin은 전역 기본 스킨 — 폰트가 다르면 지정(사실상 최초 1회, 이후엔 비교만)
        if (font != null && GUI.skin.font != font) GUI.skin.font = font;
    }
}
