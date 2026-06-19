using System.Collections;
using UnityEngine;

// 마을(허브) 진입 컷씬. 씬이 시작되면 플레이어가 입구 쪽에서 '걸어 들어오는' 연출을 1회 재생.
//  레터박스 등장 → 플레이어를 스폰 지점 뒤(입구)로 옮김 → 스폰 지점까지 걷기(걷기 애니) → 정지 → 레터박스 해제 → 조작 복귀.
//  StartingArea(마을) 씬에 빈 오브젝트로 배치(이 컴포넌트만). 걷는 방향/거리는 마을 입구에 맞게 인스펙터에서 조정.
public class HubEntryCutscene : MonoBehaviour
{
    [Header("걷기")]
    public int walkDir = 1;            // 걸어 들어오는 방향(1 = 오른쪽으로 / -1 = 왼쪽으로)
    public float walkInDistance = 5f;  // 스폰 지점에서 이만큼 안쪽으로 걸어 들어옴
    public float walkSpeed = 2f;       // 천천히 걷는 속도(이동 기본 속도보다 느리게)
    public string walkState = "Walk";  // 대시/달리기가 아닌 'Walk' 애니
    public float arriveThreshold = 0.15f;
    public float maxWalkTime = 8f;     // 안전 타임아웃(느린 걸음 고려)

    [Header("레터박스")]
    public bool useLetterbox = true;
    public float letterboxTime = 0.5f;
    public float holdAfter = 0.25f;    // 도착 후 정지 자세 유지(초)

    [Header("재생 조건")]
    public bool onlyFirstVisitPerSession = false;   // true면 세션당 1회만(이후 재진입엔 생략)
    private static bool playedThisSession;

    void Start()
    {
        if (onlyFirstVisitPerSession && playedThisSession) return;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (pc == null) yield break;
        playedThisSession = true;

        pc.cutsceneActive = true;
        pc.ZeroVelocity();
        float startX = pc.transform.position.x;
        float targetX = startX + walkDir * walkInDistance;   // 스폰 지점에서 마을 안쪽으로 걸어 들어옴(텔레포트 없음)

        if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Show(letterboxTime);
        yield return null;

        Debug.Log($"[Hub] start x={startX:F2} target={targetX:F2} dir={walkDir} spd={walkSpeed}");
        // 마을 안쪽으로 걷기
        float t = 0f; int iter = 0;
        var dbgRb = pc.GetComponent<Rigidbody2D>();
        while (t < maxWalkTime)
        {
            float dx = targetX - pc.transform.position.x;
            if (Mathf.Abs(dx) <= arriveThreshold || Mathf.Sign(dx) != walkDir) { Debug.Log($"[Hub] BREAK dx={dx:F2}"); break; }   // 도착/지나침
            pc.CutsceneMove(walkDir, walkSpeed, walkState);   // 천천히 Walk로 걸어 들어옴
            if (iter % 15 == 0) Debug.Log($"[Hub] iter={iter} x={pc.transform.position.x:F2} dx={dx:F2} velx={(dbgRb!=null?dbgRb.linearVelocity.x:-99):F2} cut={pc.cutsceneActive}");
            iter++;
            t += Time.deltaTime;
            yield return null;
        }
        Debug.Log($"[Hub] LOOP END iter={iter} x={pc.transform.position.x:F2}");

        pc.CutsceneStop();
        if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
        yield return new WaitForSeconds(holdAfter);

        pc.cutsceneActive = false;   // 조작 복귀
    }
}
