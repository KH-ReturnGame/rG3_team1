using System.Collections;
using UnityEngine;

// 마을(허브) 진입 컷씬.
//  · 첫 도착(길잡이 퀘스트 미완료) = 캐논 각성 시퀀스(순서 강제):
//    걸어 들어옴(Walk) → 입구서 쓰러짐 → 암전 → (여울 집) 밝아짐 → 깨어나는 모션 → 여울 대사.
//    ★ 대사는 이 컷씬이 '끝에서 직접' 띄운다(VillageGuide가 독립 재생하면 레이스로 먼저 떠버림).
//  · 재진입 = 평범하게 걸어 들어오기.
public class HubEntryCutscene : MonoBehaviour
{
    [Header("걷기")]
    public int walkDir = 1;
    public float walkInDistance = 5f;
    public float walkSpeed = 2f;
    public string walkState = "Walk";
    public float arriveThreshold = 0.15f;
    public float maxWalkTime = 8f;

    [Header("레터박스")]
    public bool useLetterbox = true;
    public float letterboxTime = 0.5f;
    public float holdAfter = 0.25f;

    [Header("첫 도착 각성 (걷기→기절→암전→깨어남→모션→대사)")]
    public float staggerDistance = 3f;      // 비틀거리며 들어오는 거리(짧게)
    public string sprawlState = "GroundSlam";   // 쓰러진 자세
    public string wakeState = "Crouch";         // 깨어나는(일어나는) 모션
    public float faintHold = 0.7f;          // 쓰러진 채 유지
    public float fadeTime = 0.8f;           // 암전/밝아짐 시간
    public float blackHold = 1.2f;          // 암전 유지(데려오는 시간 경과)
    public float wakeHold = 1.0f;           // 깨어나는 모션 유지

    private static bool playedThisSession;
    private float fadeAlpha;
    private Texture2D black;
    private bool dialogueDone;

    void Start() { StartCoroutine(Play()); }

    private IEnumerator Play()
    {
        var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (pc == null) yield break;

        var qm = QuestManager.Instance;
        var guide = qm != null ? qm.Find("guide_village") : null;
        bool firstArrival = guide != null && !qm.IsCompleted(guide) && !playedThisSession;
        playedThisSession = true;

        pc.cutsceneActive = true;
        pc.ZeroVelocity();
        if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Show(letterboxTime);
        yield return null;

        if (!firstArrival)
        {
            // 재진입: 평범하게 걸어 들어오기
            yield return WalkIn(pc, walkInDistance, walkSpeed);
            pc.CutsceneStop();
            if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
            yield return new WaitForSeconds(holdAfter);
            pc.cutsceneActive = false;
            yield break;
        }

        // ── 첫 도착 각성 시퀀스 ──
        if (qm != null) qm.AcceptAutoQuests();                    // 길잡이 퀘스트 수주(대사는 아래에서 직접)

        // 1) 비틀비틀 걸어 들어옴
        yield return WalkIn(pc, staggerDistance, walkSpeed * 0.75f);
        // 2) 입구서 쓰러짐
        pc.CutsceneStop();
        pc.PlayAnim(sprawlState);
        yield return new WaitForSeconds(faintHold);
        // 3) 암전 (감지 NPC가 여울 집으로 데려가는 시간)
        yield return Fade(0f, 1f, fadeTime);
        yield return new WaitForSeconds(blackHold);
        // 4) 여울 집에서 밝아짐(아직 누운 채)
        pc.PlayAnim(sprawlState);
        yield return Fade(1f, 0f, fadeTime);
        yield return new WaitForSeconds(0.4f);
        // 5) 깨어나는 모션
        pc.PlayAnim(wakeState);
        yield return new WaitForSeconds(wakeHold);
        // 6) 여울 대사 (끝날 때까지 대기) — 초상화는 Resources/Portraits/여울.png 자동 로드(DialogueUI 규약)
        Sprite portrait = null;
        string[] lines = {
            "[놀람]그쪽은 대체 누구신가요?? 이 주변엔 이곳말곤 마을이 없을 텐데.",
            "쓰러져 있는 걸 감지해서 데려온 거예요. ...뭘 그렇게 봐요, 고맙단 인사는 됐고. 치료도 내가 아니라 다른 사람 불러서 해준 거니까.",
            "이름은? 어디서 왔어요? ...하나도, 기억이 안 난다고요.",
            "머리를 어지간히 세게 부딪힌 모양이네. ...위에서 떨어진 것 같긴 한데, 그 낡은 후드 하나 걸치고서.",
            "여긴 지하 마을이에요. 일단 몸부터 추스르고 마을이나 둘러봐요. 엔지니어, 상인들, 게시판... 필요한 건 거기서 알아서 찾고.",
            "정신 들면 의뢰라도 받든가. ...[V]로 길 찾을 수 있으니까. 뭐, 죽지 않을 만큼은 챙기고 다녀요."
        };
        dialogueDone = false;
        DialogueUI.Show("여울", portrait, lines, () => dialogueDone = true);
        while (!dialogueDone) yield return null;

        // 7) 정리
        if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
        yield return new WaitForSeconds(0.3f);
        pc.cutsceneActive = false;
    }

    private IEnumerator WalkIn(PlayerController pc, float distance, float speed)
    {
        float startX = pc.transform.position.x;
        float targetX = startX + walkDir * distance;
        float t = 0f;
        while (t < maxWalkTime)
        {
            float dx = targetX - pc.transform.position.x;
            if (Mathf.Abs(dx) <= arriveThreshold || Mathf.Sign(dx) != walkDir) break;
            pc.CutsceneMove(walkDir, speed, walkState);
            t += Time.deltaTime;
            yield return null;
        }
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
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), black);
        GUI.color = prev;
    }
}
