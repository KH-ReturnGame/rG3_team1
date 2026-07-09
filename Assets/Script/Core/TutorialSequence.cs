using System.Collections;
using UnityEngine;

// 튜토리얼 연출 시퀀스(TutorialScene 전용 — 씬에 배치).
//  1) 인트로(암전+독백) 중에 왼쪽에서 근접몹 1마리가 천천히 다가옴(공격 봉인)
//  2) 가까워지면 경고 독백([놀람]) → 주인공이 아픔을 참고 '발도' → 공격 봉인 해제, 전투 개시
//     (체력이 반 칸이라, 적의 공격 직전 CombatTutorial의 '각성 예지+패링 레슨'이 자연히 발동)
//  3) 배틀 아레나(B) 클리어 → 보상 상자(onClearActivate) 등장 → 포션 줍기/등록/사용 안내
public class TutorialSequence : MonoBehaviour
{
    [Header("접근 몬스터")]
    public GameObject meleePrefab;         // Enemy_Melee 프리팹
    public float spawnOffsetX = -12f;      // 플레이어 기준 왼쪽 스폰 거리
    public float approachSpeed = 1.1f;     // 천천히 걸어오는 속도
    public float lineGap = 1.6f;           // 첫 독백 끝 ~ 경고 대사 사이 간격(적이 다가오는 그림을 보는 시간)
    public float wakeWait = 1.3f;          // 인트로 '일어나기' 모션을 기다리는 시간(검은 바는 유지)

    [Header("아레나 클리어 안내(보상 상자 달린 아레나)")]
    public BattleArena arena;

    [TextArea] public string[] alarmLines = {
        "[놀람]……뭐지. 이쪽으로, 온다.",
        "[떨림]싸울 수밖에 없어. ……부서질 것 같은 몸이지만."
    };

    private Enemy approacher;

    IEnumerator Start()
    {
        // 플레이어 준비 대기
        while (PlayerController.Instance == null) yield return null;
        var pc = PlayerController.Instance;

        // 인트로 독백(대사창)이 열릴 때까지 대기 → 독백 '중'에 스폰(다가오는 그림)
        float guard = 12f;
        while (!DialogueUI.IsOpen && guard > 0f) { guard -= Time.deltaTime; yield return null; }

        // 접근몹 스폰: 멀리서 천천히, 공격은 봉인
        if (meleePrefab != null)
        {
            var go = Instantiate(meleePrefab, pc.transform.position + new Vector3(spawnOffsetX, 0.5f, 0f), Quaternion.identity);
            approacher = go.GetComponentInChildren<Enemy>();
            if (approacher != null)
            {
                approacher.randomizeStats = false;
                approacher.moveSpeed = approachSpeed;
                approacher.detectRange = 40f;     // 스폰 즉시 플레이어에게 걸어옴
                approacher.detectHeight = 8f;
                approacher.firstAttackDelay = 0f;
                approacher.attackDamage = Mathf.Min(approacher.attackDamage, 0.5f);   // 튜토 공격력 상한(런타임 스폰이라 직접)
                approacher.ArmAttack(999f);        // 발도 전엔 절대 공격 안 함
            }
        }

        // 인트로 독백 종료 대기
        while (DialogueUI.IsOpen) yield return null;

        // ★검은 바(레터박스)·컷씬을 끊지 않고 유지: 인트로 꼬리(일어나기)가 바를 내리고 조작을 풀려 해도
        //   매 프레임 다시 잡는다 — 일어나기 + 적이 걸어오는 '간격'까지 한 호흡으로
        float hold = wakeWait + lineGap;
        float t = 0f;
        while (t < hold)
        {
            pc.cutsceneActive = true;
            if (Letterbox.Instance != null) Letterbox.Instance.Show(0.15f);
            t += Time.deltaTime;
            yield return null;
        }
        pc.ZeroVelocity();

        // 경고 독백(검은 바 유지 상태에서) → "싸울 수밖에 없어"까지
        bool done = false;
        DialogueUI.Show("???", null, alarmLines, () => done = true);
        while (!done)
        {
            pc.cutsceneActive = true;
            if (Letterbox.Instance != null) Letterbox.Instance.Show(0.15f);
            yield return null;
        }

        // 대사가 끝난 지금에서야 검은 바가 걷히며 발도 → 전투 개시
        if (Letterbox.Instance != null) Letterbox.Instance.Hide(1.2f);
        pc.CutsceneDrawSword();
        yield return new WaitForSeconds(0.4f);
        pc.cutsceneActive = false;
        if (approacher != null) approacher.ArmAttack(1.4f);   // 공격 개시(살짝 여유 — 자세 잡을 시간)

        // ── 아레나 클리어 감시 → 포션 서바이벌 키트 안내 ──
        if (arena != null)
        {
            while (arena != null && !arena.IsCleared) yield return null;
            yield return new WaitForSeconds(0.9f);
            if (HelpPopupUI.Instance != null)
                HelpPopupUI.Instance.ShowTimed("전리품 — 회복 포션",
                    "상자에서 회복 포션이 나왔습니다!\n[F]로 줍고 → 배낭[B]에서 우클릭 → [1번 슬롯에 등록] → 전투 중 [1]로 바로 마실 수 있습니다.", 10f);
        }
    }
}
