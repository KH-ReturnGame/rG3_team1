using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// 마을(허브) 진입 시 1회: '길잡이' 퀘스트를 자동 수주하고, 다친 주인공이 막 도착한 설정의 인트로 대화를 재생.
//  · 대화는 NPC 구성·게임 분위기·목적(우물로 내려가 사냥)을 알려준다.
//  · 걸어 들어오는 컷씬이 끝나고 조작이 가능해진 뒤에 시작. 이미 길잡이를 완료했으면 아무것도 안 함.
public class VillageGuide : MonoBehaviour
{
    public static VillageGuide Instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("VillageGuide");
        Instance = go.AddComponent<VillageGuide>();
        DontDestroyOnLoad(go);
    }

    void OnEnable() { SceneManager.sceneLoaded += OnScene; }
    void OnDisable() { SceneManager.sceneLoaded -= OnScene; }

    private void OnScene(Scene s, LoadSceneMode mode)
    {
        StopAllCoroutines();   // 씬 전환 시 진행 중이던 길잡이 코루틴 중단(다음 씬으로 대사 새어나가지 않게)
        string hub = GameFlow.Instance != null ? GameFlow.Instance.hubScene : "StartingArea";
        if (s.name != hub && s.name != "StartingArea") return;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        var qm = QuestManager.Instance;
        if (qm == null) yield break;
        var guide = qm.Find("guide_village");
        if (guide == null || qm.IsCompleted(guide)) yield break;   // 이미 완료 → 아무것도 안 함

        bool fresh = !qm.accepted.Contains(guide);
        qm.AcceptAutoQuests();                                     // 길잡이 수주(트래커 표시)
        if (!fresh) yield break;                                   // 재진입(이미 수주중) → 대화 재생 안 함

        // 걸어 들어오는 컷씬이 끝나 조작 가능해질 때까지 대기
        float t = 0f;
        while (t < 8f)
        {
            var pc = PlayerController.Instance;
            if (pc != null && !pc.cutsceneActive && !DialogueUI.IsOpen) break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        yield return new WaitForSecondsRealtime(0.5f);

        var portrait = Resources.Load<Sprite>("Portraits/portrait_yeul");
        string[] lines = {
            "그쪽은 대체 누구신가요?? 이 주변엔 이곳말곤 마을이 없을 텐데.",
            "쓰러져 있는 걸 감지해서 데려온 거예요. ...뭘 그렇게 봐요, 고맙단 인사는 됐고. 치료도 내가 아니라 다른 사람 불러서 해준 거니까.",
            "이름은? 어디서 왔어요? ...하나도, 기억이 안 난다고요.",
            "머리를 어지간히 세게 부딪힌 모양이네. ...위에서 떨어진 것 같긴 한데, 그 낡은 후드 하나 걸치고서.",
            "여긴 지하 마을이에요. 일단 몸부터 추스르고 마을이나 둘러봐요. 엔지니어, 상인들, 게시판... 필요한 건 거기서 알아서 찾고.",
            "정신 들면 의뢰라도 받든가. ...[V]로 길 찾을 수 있으니까. 뭐, 죽지 않을 만큼은 챙기고 다녀요."
        };
        DialogueUI.Show("여울", portrait, lines, null);
    }
}
