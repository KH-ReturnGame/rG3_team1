using UnityEngine;

// 마을 게시판 — F로 의뢰 게시판 UI를 연다. Collider2D 필요(PlayerInteractor가 감지).
public class QuestBoard : MonoBehaviour, IInteractable
{
    public string Prompt => "F: 의뢰 게시판";
    public void Interact() { if (QuestBoardUI.Instance != null) QuestBoardUI.Instance.Open(); }
}
