using UnityEngine;
using UnityEngine.SceneManagement;

// 마을(허브) 진입 시 '기억을 잃은 자'(길잡이) 퀘스트만 자동 수주.
//  ★ 각성 대사·연출(걷기→기절→암전→깨어남→여울 대사)은 HubEntryCutscene가 '순서대로' 담당한다.
//    (예전엔 여기서 대사를 독립 재생해 컷씬과 레이스가 났음 — 그 역할 제거.)
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
        string hub = GameFlow.Instance != null ? GameFlow.Instance.hubScene : "StartingArea";
        if (s.name != hub && s.name != "StartingArea") return;
        if (QuestManager.Instance != null) QuestManager.Instance.AcceptAutoQuests();   // 길잡이 퀘 수주(안전망)
    }
}
