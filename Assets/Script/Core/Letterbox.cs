using UnityEngine;

// 영화 필름식 레터박스(위·아래 검은 막대). 컷씬용. 자동부팅 싱글톤.
// 위/아래 막대를 독립적으로 제어 가능(아래 막대를 더 키워 플레이어를 가리는 연출 등).
public class Letterbox : MonoBehaviour
{
    public static Letterbox Instance;

    public float barHeightFrac = 0.13f;   // 기본 막대 두께(화면 높이 비율)

    private float topCur, topTgt, topSpd;   // 위 막대(화면 비율 0~)
    private float botCur, botTgt, botSpd;   // 아래 막대(독립)
    private static Texture2D _black;

    public bool IsFull => topCur >= barHeightFrac - 0.005f;
    public bool IsHidden => topCur <= 0.005f && botCur <= 0.005f;

    // 컷씬 중 여부 — 막대가 '조금이라도 보이는 동안' true. HUD류 UI가 이걸 보고 숨는다(완전히 사라져야 복귀).
    public static bool Covering => Instance != null && (Instance.topCur > 0.005f || Instance.botCur > 0.005f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("Letterbox");
        Instance = go.AddComponent<Letterbox>();
        DontDestroyOnLoad(go);
    }

    // 위·아래 둘 다 기본 두께로 등장
    public void Show(float duration = 0.6f)
    {
        float s = duration > 0.01f ? barHeightFrac / duration : 100f;
        topTgt = barHeightFrac; botTgt = barHeightFrac; topSpd = s; botSpd = s;
    }

    // 둘 다 사라짐
    public void Hide(float duration = 0.6f)
    {
        float s = duration > 0.01f ? barHeightFrac / duration : 100f;
        topTgt = 0f; botTgt = 0f; topSpd = s; botSpd = s;
    }

    // 아래 막대만 별도 높이로(화면 비율). 컷씬 중반에 플레이어를 가리는 용도.
    public void SetBottom(float frac, float duration = 0.5f)
    {
        botTgt = Mathf.Clamp(frac, 0f, 0.5f);
        botSpd = duration > 0.01f ? Mathf.Max(0.01f, Mathf.Abs(botTgt - botCur)) / duration : 100f;
    }

    void Update()
    {
        topCur = Mathf.MoveTowards(topCur, topTgt, topSpd * Time.unscaledDeltaTime);
        botCur = Mathf.MoveTowards(botCur, botTgt, botSpd * Time.unscaledDeltaTime);
    }

    void OnGUI()
    {
        if (topCur <= 0.001f && botCur <= 0.001f) return;
        Color prev = GUI.color;
        GUI.color = Color.black;
        if (topCur > 0.001f) GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height * topCur), Black());                          // 위
        if (botCur > 0.001f) GUI.DrawTexture(new Rect(0, Screen.height * (1f - botCur), Screen.width, Screen.height * botCur), Black()); // 아래
        GUI.color = prev;
    }

    private static Texture2D Black()
    {
        if (_black == null) { _black = new Texture2D(1, 1); _black.SetPixel(0, 0, Color.black); _black.Apply(); }
        return _black;
    }
}
