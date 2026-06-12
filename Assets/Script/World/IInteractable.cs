// F로 상호작용할 수 있는 대상 공용 인터페이스 (줍기, 나중에 NPC/문 등도 가능)
public interface IInteractable
{
    void Interact();        // 상호작용 실행
    string Prompt { get; }  // 화면에 보여줄 안내문 (예: "F: 나무 줍기")
}
