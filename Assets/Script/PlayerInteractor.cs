using UnityEngine;

// 플레이어에 부착. F키로 가까운 상호작용 대상(IInteractable)을 찾아 실행.
public class PlayerInteractor : MonoBehaviour
{
    public KeyCode interactKey = KeyCode.F;
    public float interactRadius = 1.2f;

    private IInteractable nearest;
    private GUIStyle promptStyle;

    void Update()
    {
        if (InventoryUI.IsOpen) { nearest = null; return; }   // 인벤토리 열려있으면 상호작용 잠금
        nearest = FindNearest();
        if (nearest != null && Input.GetKeyDown(interactKey))
            nearest.Interact();
    }

    private IInteractable FindNearest()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactRadius);
        IInteractable best = null;
        float bestDist = Mathf.Infinity;
        foreach (var h in hits)
        {
            var it = h.GetComponent<IInteractable>();
            if (it == null) continue;
            float d = Vector2.Distance(transform.position, h.transform.position);
            if (d < bestDist) { bestDist = d; best = it; }
        }
        return best;
    }

    // 가까운 대상이 있으면 안내문 표시("F: ... 줍기")
    void OnGUI()
    {
        if (nearest == null || Camera.main == null) return;
        Vector3 sp = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.6f);
        if (sp.z < 0) return;

        if (promptStyle == null)
            promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 14 };
        promptStyle.normal.textColor = Color.white;

        GUI.Label(new Rect(sp.x - 100, Screen.height - sp.y - 24, 200, 22), nearest.Prompt, promptStyle);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
