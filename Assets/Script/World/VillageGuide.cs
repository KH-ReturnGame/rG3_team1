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

        string[] lines = {
            "정신이 드나? ...겨우 살아서 도망쳐 왔군. 여긴 지저 마을 — 지하 세계의 작은 안식처일세.",
            "그 붉은 후드 망토, 너덜너덜하군. 저쪽 엔지니어에게 가면 수리해 줄 걸세. 쓸 만한 모듈도 달아주고 말이야.",
            "재료 상인·포션 상인·탐험가에게서 채비를 갖추고, 의뢰 게시판에서 일감을 받을 수 있네.",
            "준비가 되면 마을 한켠의 우물로 내려가게. ...진짜 사냥은 거기서부터일세.",
            "왼쪽 안내(퀘스트)를 따라가게. [V]를 누르면 길을 짚어줄 걸세. 부디, 살아남게."
        };
        DialogueUI.Show("길잡이", null, lines, null);
    }
}
