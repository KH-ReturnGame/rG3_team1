using System.Collections;
using UnityEngine;

// 마을(허브) 진입 연출 + 마을 탐방 스토리.
//  · 첫 도착: 마을로 걸어 들어와 주인공 '독백'(기억상실·구조·회복 결심). 여울이 떠먹여 주던 각성 컷씬은 폐지.
//    이후 마을을 돌아다니며 엔지니어·상인·제작대·게시판을 둘러보는 것이 guide_village 퀘스트 목표.
//  · 마을을 다 둘러보면(guide_village 완료) 주인공 독백으로 '우물 아래로 내려가자' — 다음 방향 제시.
//  · 재진입: 짧게 걸어 들어오기만.
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

    // 첫 도착 독백(기억상실 + 누군가 구조 + 회복/둘러보기 결심)
    [TextArea] public string[] arrivalLines = {
        "[아픔]……여긴, 어디지. 낯선 천장. 낯선 공기.",
        "위에서… 떨어진 것 같은데. 그 다음이 기억나지 않아. 내 이름조차도.",
        "누군가 쓰러진 날 여기까지 옮겨준 모양이다. …얼굴도 제대로 못 봤지만.",
        "[결심]지금은 회복이 먼저야. 몸을 추스르고, 이 마을을 둘러보자.",
        "엔지니어, 상인, 제작대, 게시판… 뭐라도 단서가 있겠지."
    };

    // 마을을 다 둘러본 뒤 독백(다음 목표 = 우물 하강)
    [TextArea] public string[] exploredLines = {
        "[한숨]몸은… 이제 좀 움직일 만하다.",
        "마을은 대충 파악했어. 더 서성여 봐야 나올 건 없겠지.",
        "[결심]답은 아래에 있어. 저 우물 — 지하 깊은 곳으로 내려가 보자."
    };

    private static bool playedThisSession;
    private bool introDone;
    private bool wasExploredAtEntry;   // 진입 시점에 이미 완료 상태였나(재진입·이어하기 → 완료 독백 생략)
    private bool exploredNarrated;

    void Start() { StartCoroutine(Play()); }

    private IEnumerator Play()
    {
        var pc = PlayerController.Instance != null ? PlayerController.Instance : FindAnyObjectByType<PlayerController>();
        if (pc == null) yield break;

        var qm = QuestManager.Instance;
        var guide = qm != null ? qm.Find("guide_village") : null;
        wasExploredAtEntry = guide != null && qm.IsCompleted(guide);
        bool firstArrival = guide != null && !qm.IsCompleted(guide) && !playedThisSession;
        playedThisSession = true;

        pc.cutsceneActive = true;
        pc.ZeroVelocity();
        if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Show(letterboxTime);
        yield return null;

        // 걸어 들어오기(공통)
        yield return WalkIn(pc, walkInDistance, firstArrival ? walkSpeed * 0.8f : walkSpeed);
        pc.CutsceneStop();

        if (firstArrival)
        {
            // 첫 도착: 길잡이 퀘스트 수주 + 주인공 독백
            if (qm != null) qm.AcceptAutoQuests();
            bool done = false;
            DialogueUI.Show("???", null, arrivalLines, () => done = true);
            while (!done) yield return null;
        }

        if (useLetterbox && Letterbox.Instance != null) Letterbox.Instance.Hide(letterboxTime);
        yield return new WaitForSeconds(holdAfter);
        pc.cutsceneActive = false;
        introDone = true;

        // (구) 도움말 다시보기 안내 카드 — 폐지(요청)
    }

    // 마을을 다 둘러본 순간(guide_village가 방금 완료) → 우물 하강 독백 1회
    void Update()
    {
        if (!introDone || exploredNarrated || wasExploredAtEntry) return;
        var qm = QuestManager.Instance;
        if (qm == null || !qm.completed.Contains("guide_village")) return;
        // UI/대사/컷씬 중엔 대기(마지막 방문이 제작대 등 UI였을 수 있음 — 닫힌 뒤에 독백)
        if (Inventory.IsUIOpen || DialogueUI.IsOpen || Letterbox.Covering) return;

        exploredNarrated = true;
        StartCoroutine(NarrateExplored());
    }

    private IEnumerator NarrateExplored()
    {
        var pc = PlayerController.Instance;
        yield return new WaitForSeconds(0.4f);
        if (pc != null) { pc.cutsceneActive = true; pc.ZeroVelocity(); }
        bool done = false;
        DialogueUI.Show("???", null, exploredLines, () => done = true);
        while (!done) yield return null;
        if (pc != null) pc.cutsceneActive = false;
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
}
