using System.Collections.Generic;
using UnityEngine;

// 카메라 구역. 맵의 각 방/구역을 BoxCollider2D(트리거)로 덮어두면,
// 플레이어가 그 구역에 들어왔을 때 CameraFollow가 이 구역을 카메라 경계로 사용한다.
// 비일자 맵(여러 방이 복도로 연결)에서 방마다 카메라 범위/줌을 따로 줄 수 있음.
//
// 사용법:
//   1) 빈 GameObject 생성 → CameraZone 컴포넌트 추가(BoxCollider2D 자동 생성·트리거화)
//   2) 콜라이더 size/위치를 그 방(+딸린 복도 입구까지 살짝)을 덮도록 조절. 씬뷰에 하늘색 박스로 보임
//   3) 방마다 하나씩. 인접 구역은 경계에서 살짝 겹치게 두면 전환이 매끄럽다
//   ※ 트리거가 아니라 위치 포함(Contains)으로 판정하므로 레이어/태그·물리 충돌과 무관(플레이어를 밀지 않음)
[RequireComponent(typeof(BoxCollider2D))]
public class CameraZone : MonoBehaviour
{
    // 씬에 존재하는 모든 활성 구역(CameraFollow가 참조)
    public static readonly List<CameraZone> All = new List<CameraZone>();

    [Tooltip("겹치는 구역이 있을 때 우선순위(클수록 우선). 같은 자리에 작은 특수 구역(보스방 등)을 둘 때 사용")]
    public int priority = 0;

    [Tooltip("이 구역의 줌 배율(0=전역 zoomMul 사용). 전투방을 더 줌인(예: 0.7)하는 등 방별 연출용")]
    public float zoomMul = 0f;

    private BoxCollider2D box;

    void Awake() { box = GetComponent<BoxCollider2D>(); if (box != null) box.isTrigger = true; }
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() { All.Remove(this); }

    // 컴포넌트 추가 시 기본값: 트리거 + 적당한 방 크기
    void Reset()
    {
        var c = GetComponent<BoxCollider2D>();
        if (c != null) { c.isTrigger = true; c.size = new Vector2(24f, 13f); }
    }

    public Bounds Area
    {
        get
        {
            if (box == null) box = GetComponent<BoxCollider2D>();
            return box != null ? box.bounds : new Bounds(transform.position, new Vector3(24f, 13f, 0f));
        }
    }

    public bool Contains(Vector2 p)
    {
        Bounds b = Area;
        return p.x >= b.min.x && p.x <= b.max.x && p.y >= b.min.y && p.y <= b.max.y;
    }

    void OnDrawGizmos()
    {
        Bounds b = Area;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireCube(b.center, b.size);
    }
}
