using System.Collections;
using UnityEngine;

// 튜토리얼 인트로 컷씬(새 게임 SaveSystem.IntroPending일 때 1회). 캐논:
//  추락 장면은 '보여주지 않는다'. 이미 바닥에 쓰러져 있는 주인공을 비추며 시작 →
//  암전에서 서서히 밝아짐 → 독백(대사창)으로 '많이 다쳤고(딸피) 기억이 흐릿함'을 전달 → 일어남.
// TutorialScene에 빈 오브젝트로 배치.
public class IntroCutscene : MonoBehaviour
{
    [Header("타이밍")]
    public float startBlackHold = 1.0f;   // 시작 암전 유지
    public float fadeTime = 3.2f;         // 암전이 '풀리는' 시간 — 아주 천천히 밝아짐
    public float afterFadeHold = 0.8f;    // 밝아진 뒤 독백 전 여백
    public float wakeHold = 0.9f;         // 일어나는 모션 유지
    public float letterboxTime = 0.6f;    // 레터박스 '등장' 속도
    public float letterboxHideTime = 2.0f; // 레터박스 '풀리는' 속도 — 독백 후 천천히 걷힘

    [Header("애니 클립명")]
    public string sprawlState = "GroundSlam";   // 쓰러진 자세(클립)
    [Range(0f, 1f)] public float sprawlFrame = 0.62f;   // 클립의 이 지점에서 정지 = GroundSlam07 스프라이트 한 장 고정
    public string wakeState = "Crouch";         // 일어나는 모션

    [Header("독백")]
    [TextArea] public string speakerName = "???";
    [TextArea] public string[] monologue = {
        "[흔들림]……크윽.",
        "[떨림]온몸이... 부서질 것처럼 아프다. 겨우 숨만 붙어 있는 것 같아.",
        "여긴... 어디지. 위에서, 떨어진 건가.",
        "기억이 흐릿해. ...내가 누구인지조차, 떠오르질 않아."
    };

    private float fadeAlpha;
    private Texture2D black;
    private bool dialogueDone;

    void Start()
    {
        if (!SaveSystem.IntroPending) return;
        SaveSystem.IntroPending = false;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (pc == null) yield break;

        // 직렬화 갱신으로 배열/이름이 비어도 안전하게 폴백
        if (string.IsNullOrEmpty(speakerName)) speakerName = "???";
        if (monologue == null || monologue.Length == 0)
            monologue = new[] {
                "[흔들림]……크윽.",
                "[떨림]온몸이... 부서질 것처럼 아프다. 겨우 숨만 붙어 있는 것 같아.",
                "여긴... 어디지. 위에서, 떨어진 건가.",
                "기억이 흐릿해. ...내가 누구인지조차, 떠오르질 않아."
            };

        pc.cutsceneActive = true;
        pc.ZeroVelocity();
        pc.PlayAnimFrozen(sprawlState, sprawlFrame);   // 쓰러진 자세 — GroundSlam07 한 장으로 고정(애니 재생 X)
        GameManager.Instance?.SetHalves(1);       // 딸피(체력 반칸) — LowHealthFx가 붉은 비네트로 표시
        if (Letterbox.Instance != null) Letterbox.Instance.Show(letterboxTime);

        // 암전에서 시작 → 쓰러진 주인공을 비추며 서서히 밝아짐
        fadeAlpha = 1f;
        yield return new WaitForSeconds(startBlackHold);
        yield return Fade(1f, 0f, fadeTime);
        yield return new WaitForSeconds(afterFadeHold);

        // 독백(대사창) — #5: 자막이 아니라 DialogueUI로
        dialogueDone = false;
        DialogueUI.Show(speakerName, null, monologue, () => dialogueDone = true);
        while (!dialogueDone) yield return null;

        // 일어나는 모션
        pc.PlayAnim(wakeState);
        yield return new WaitForSeconds(wakeHold);

        if (Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxHideTime);   // 독백 끝 — 천천히 걷힘
        yield return new WaitForSeconds(0.3f);
        pc.cutsceneActive = false;
    }

    private IEnumerator Fade(float from, float to, float dur)
    {
        float t = 0f;
        while (t < dur) { fadeAlpha = Mathf.Lerp(from, to, t / dur); t += Time.deltaTime; yield return null; }
        fadeAlpha = to;
    }

    void OnGUI()
    {
        if (fadeAlpha <= 0.001f) return;
        if (black == null) { black = new Texture2D(1, 1); black.SetPixel(0, 0, Color.black); black.Apply(); }
        GUI.depth = -1900;
        var prev = GUI.color; GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
        GUI.color = prev;
    }
}
