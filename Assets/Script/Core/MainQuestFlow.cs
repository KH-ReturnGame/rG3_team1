using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 메인 퀘스트 체인 진행기(자동부팅·영구).
//  씬 '도착'을 기준으로 주요 퀘스트를 수주/완료해, 튜토리얼 시작부터 첫 보스 처치까지
//  트래커에 항상 다음 방향이 표시되게 한다. (완료 상태는 QuestManager.completed → 세이브로 유지)
//
//    TutorialScene 도착 → [수주] mq_awaken   "낯선 어둠 속에서"
//    StartingArea  도착 → [완료] mq_awaken   (여울의 guide_village는 VillageGuide가 기존대로 수주)
//    Metroidvania  도착 → [완료] guide_village → [수주] mq_descend "심층을 향해"
//    BossScene     도착 → [완료] mq_descend  → [수주] mq_boss "첫 번째 위협"
//    첫 보스 처치(ReportKill "boss_first")   → mq_boss 즉시 완료(autoAccept라 게시판 수령 없음)
//
//  ★보스 연동: 보스가 Enemy 상속이면 인스펙터 questKillId = "boss_first" 만 넣으면 끝.
//    아니면 보스 사망 코드에서 QuestManager.Instance.ReportKill("boss_first") 한 줄.
public class MainQuestFlow : MonoBehaviour
{
    public static MainQuestFlow Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("MainQuestFlow");
        Instance = go.AddComponent<MainQuestFlow>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }
    void Start() => OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single);   // 부팅 시 현재 씬(에디터 직행)도 반영

    private void OnScene(Scene s, LoadSceneMode m) => StartCoroutine(Process(s.name));

    // 한 프레임 대기: 같은 sceneLoaded에서 도는 SaveSystem.Apply(퀘스트 복원)가 먼저 끝난 뒤 처리(수주가 덮이지 않게)
    private IEnumerator Process(string scene)
    {
        yield return null;
        var qm = QuestManager.Instance;
        if (qm == null) yield break;

        switch (scene)
        {
            case "TutorialScene":
                qm.AcceptById("mq_awaken");
                break;
            case "StartingArea":
                qm.CompleteForce("mq_awaken");        // 불빛(마을)에 도착
                break;
            case "Metroidvania":
                qm.CompleteForce("guide_village");    // 우물로 하강 완료
                qm.AcceptById("mq_descend");
                break;
            case "BossScene":
                qm.CompleteForce("mq_descend");       // 심층부 진입
                qm.AcceptById("mq_boss");
                break;
        }
    }
}
