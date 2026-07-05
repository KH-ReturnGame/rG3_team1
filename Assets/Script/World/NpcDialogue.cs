using UnityEngine;

// NPC 대사 데이터(상인·엔지니어 등에 부착). IInteractable이 아님 — 스테이션의 Interact()가 대화를 먼저 재생하는 데 사용.
//  · 같은 오브젝트에 ShopStation/EngineerStation과 함께 두면, F → 대화 → (끝나면) 상점/업그레이드 열림.
public class NpcDialogue : MonoBehaviour
{
    public string speakerName = "주민";
    public Sprite portrait;                 // 박스 위에 표시(없으면 '?')
    [TextArea(2, 4)] public string[] lines; // 대사 줄(한 줄씩 진행)

    public bool HasLines => lines != null && lines.Length > 0;
    public void Run(System.Action onDone) { DialogueUI.Show(speakerName, portrait, lines, onDone); }
}
