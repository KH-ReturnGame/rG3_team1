using UnityEngine;
using UnityEngine.Events;

// NPC 대사 데이터(상인·엔지니어 등에 부착). IInteractable이 아님 — 스테이션의 Interact()가 대화를 먼저 재생하는 데 사용.
//  · 같은 오브젝트에 ShopStation/EngineerStation과 함께 두면, F → 대화 → (끝나면) 상점/업그레이드 열림.
//  · 선택지: choices를 채우면 마지막 대사 후 선택지가 뜬다. 고른 선택지의 replyLines(후속 대사)가
//    이어진 뒤 onSelect(UnityEvent) 실행 → 그 다음에야 상점/업그레이드 등 원래 완료 콜백이 돈다.
//    비워두면 기존과 완전히 동일(대사만 하고 종료).
public class NpcDialogue : MonoBehaviour
{
    public string speakerName = "주민";
    public Sprite portrait;                 // 박스 위에 표시(없으면 '?')
    [TextArea(2, 4)] public string[] lines; // 대사 줄(한 줄씩 진행)

    [System.Serializable]
    public class Choice
    {
        public string text;                          // 선택지 버튼 문구
        [TextArea(2, 4)] public string[] replyLines; // 선택 후 이어질 대사(비우면 바로 종료)
        public UnityEvent onSelect;                  // 선택 시 실행(인스펙터에서 연결 — 퀘스트 수주, 문 열기 등)
    }

    [Header("선택지 — 마지막 대사 후 표시(비우면 없음)")]
    public Choice[] choices;

    public bool HasLines => lines != null && lines.Length > 0;

    public void Run(System.Action onDone)
    {
        if (choices == null || choices.Length == 0)
        {
            DialogueUI.Show(speakerName, portrait, lines, onDone);
            return;
        }
        string[] texts = new string[choices.Length];
        for (int i = 0; i < choices.Length; i++) texts[i] = choices[i] != null ? choices[i].text : "";
        DialogueUI.Show(speakerName, portrait, lines, texts, i => OnPick(i, onDone));
    }

    private void OnPick(int i, System.Action onDone)
    {
        Choice c = (choices != null && i >= 0 && i < choices.Length) ? choices[i] : null;
        if (c == null) { if (onDone != null) onDone(); return; }
        if (c.replyLines != null && c.replyLines.Length > 0)
            DialogueUI.Show(speakerName, portrait, c.replyLines, () => Finish(c, onDone));
        else
            Finish(c, onDone);
    }

    private void Finish(Choice c, System.Action onDone)
    {
        if (c.onSelect != null) c.onSelect.Invoke();
        if (onDone != null) onDone();
    }
}
