using UnityEngine;

// 인벤토리 '실제 그리드'를 다른 UI(상점 등)에 그려주는 공용 렌더러+상호작용.
//  InventoryUI와 같은 규칙: 엔트리 클릭=집기, 발자국 고스트(초록/빨강), 놓기/스택 합치기/1:1 교체.
//  자동 배치 없음 — 플레이어가 원하는 칸에 직접 놓는다.
public static class InvGridGUI
{
    private const float pad = 5f;
    private static float cell, ox, oy;
    private static GUIStyle countStyle, nameStyle;

    private static readonly Color cOk  = new Color(0.35f, 0.85f, 0.40f);
    private static readonly Color cBad = new Color(0.90f, 0.30f, 0.28f);

    // area 안에 인벤 그리드를 맞춰 그림. held/heldCount/heldRotated = 마우스에 든 아이템(집기/놓기/[R]회전 반영).
    // 반환: 호버 중인 아이템(툴팁용, 손이 비었을 때만).
    public static ItemData Draw(Rect area, Vector2 mouse, bool click, ref ItemData held, ref int heldCount, ref int heldRot)
    {
        var inv = Inventory.Instance;
        if (inv == null) return null;
        EnsureStyles();
        int cols = inv.gridWidth, rows = inv.gridHeight;
        cell = Mathf.Min((area.width - pad * (cols + 1)) / cols, (area.height - pad * (rows + 1)) / rows);
        float gw = cols * (cell + pad) + pad, gh = rows * (cell + pad) + pad;
        ox = area.x + (area.width - gw) * 0.5f;
        oy = area.y + (area.height - gh) * 0.5f;
        Rect gridRect = new Rect(ox, oy, gw, gh);

        // 빈 칸
        for (int cy = 0; cy < rows; cy++)
            for (int cx = 0; cx < cols; cx++)
                UITheme.DrawSlot(CellRect(cx, cy), UITheme.A(UITheme.Border, 0.55f), false, 1f);

        // 엔트리(발자국)
        ItemData hover = null;
        foreach (var s in inv.slots)
        {
            if (s == null || s.IsEmpty) continue;
            Rect fr = Footprint(s.x, s.y, s.W, s.H);
            bool hv = held == null && fr.Contains(mouse);
            UITheme.FillV(fr, hv ? UITheme.Lighten(UITheme.SlotTop, 0.10f) : UITheme.Lighten(UITheme.SlotTop, 0.03f), UITheme.SlotBot);
            UITheme.Border2(fr, 1.2f, hv ? UITheme.Lighten(UITheme.Border, 0.22f) : UITheme.Border);
            UITheme.RarityRing(fr, s.item.RarityColor());
            DrawIcon(fr, s.item, s.count, s.rot);
            if (hv) hover = s.item;
        }

        // 들고 있는 아이템 발자국 고스트([R] 회전 반영)
        if (held != null && gridRect.Contains(mouse))
        {
            int hw = (heldRot & 1) == 1 ? held.GridH : held.GridW, hh = (heldRot & 1) == 1 ? held.GridW : held.GridH;
            Anchor(inv, mouse, hw, hh, out int ax, out int ay);
            bool ok = inv.CanPlace(held, ax, ay, heldRot);
            Rect gr = Footprint(ax, ay, hw, hh);
            UITheme.Fill(gr, UITheme.A(ok ? cOk : cBad, 0.30f));
            UITheme.Border2(gr, 2f, UITheme.A(ok ? cOk : cBad, 0.85f));
        }

        // 클릭: 집기 / 놓기 / 스택 / 교체
        if (click && gridRect.Contains(mouse))
        {
            HandleClick(inv, mouse, ref held, ref heldCount, ref heldRot);
            if (Event.current != null) Event.current.Use();
        }
        return hover;
    }

    private static void HandleClick(Inventory inv, Vector2 m, ref ItemData held, ref int heldCount, ref int heldRot)
    {
        if (held == null)
        {
            CellAt(inv, m, out int cx, out int cy);
            var e = inv.EntryAt(cx, cy);
            if (e != null) { held = e.item; heldCount = e.count; heldRot = e.rot; inv.slots.Remove(e); inv.RaiseChanged(); }
            return;
        }

        int hw = (heldRot & 1) == 1 ? held.GridH : held.GridW, hh = (heldRot & 1) == 1 ? held.GridW : held.GridH;
        Anchor(inv, m, hw, hh, out int ax, out int ay);
        if (inv.CanPlace(held, ax, ay, heldRot))
        {
            inv.Place(held, heldCount, ax, ay, heldRot);
            held = null; heldCount = 0; heldRot = 0;
            inv.RaiseChanged();
            return;
        }

        // 겹침 처리: 정확히 1개면 스택/교체
        var overlaps = new System.Collections.Generic.List<Inventory.Slot>();
        foreach (var s in inv.slots)
        {
            if (s == null || s.IsEmpty) continue;
            if (ax < s.x + s.W && s.x < ax + hw && ay < s.y + s.H && s.y < ay + hh)
                overlaps.Add(s);
        }
        if (overlaps.Count != 1) return;
        var t = overlaps[0];
        if (t.item == held && t.count < held.maxStack)
        {
            int put = Mathf.Min(held.maxStack - t.count, heldCount);
            t.count += put; heldCount -= put;
            if (heldCount <= 0) { held = null; heldCount = 0; heldRot = 0; }
            inv.RaiseChanged();
        }
        else if (inv.CanPlace(held, ax, ay, heldRot, t))
        {
            var ti = t.item; int tc = t.count; int tr = t.rot;
            inv.slots.Remove(t);
            inv.Place(held, heldCount, ax, ay, heldRot);
            held = ti; heldCount = tc; heldRot = tr;
            inv.RaiseChanged();
        }
    }

    // ── 좌표 ──
    private static Rect CellRect(int cx, int cy)
        => new Rect(ox + pad + cx * (cell + pad), oy + pad + cy * (cell + pad), cell, cell);

    private static Rect Footprint(int x, int y, int gw2, int gh2)
    {
        Rect a = CellRect(x, y);
        return new Rect(a.x, a.y, gw2 * (cell + pad) - pad, gh2 * (cell + pad) - pad);
    }

    private static void CellAt(Inventory inv, Vector2 m, out int cx, out int cy)
    {
        cx = Mathf.Clamp(Mathf.FloorToInt((m.x - ox - pad) / (cell + pad)), 0, inv.gridWidth - 1);
        cy = Mathf.Clamp(Mathf.FloorToInt((m.y - oy - pad) / (cell + pad)), 0, inv.gridHeight - 1);
    }

    private static void Anchor(Inventory inv, Vector2 m, int w, int h, out int ax, out int ay)
    {
        CellAt(inv, m, out int cx, out int cy);
        ax = Mathf.Clamp(cx - (w - 1) / 2, 0, Mathf.Max(0, inv.gridWidth - w));
        ay = Mathf.Clamp(cy - (h - 1) / 2, 0, Mathf.Max(0, inv.gridHeight - h));
    }

    private static void DrawIcon(Rect r, ItemData item, int count, int rot = 0)
    {
        if (item.icon != null)
        {
            rot &= 3;
            if (rot != 0)
            {
                var mtx = GUI.matrix;
                GUIUtility.RotateAroundPivot(rot * 90f, r.center);
                Rect rr = (rot & 1) == 1
                    ? new Rect(r.center.x - r.height * 0.5f + 3, r.center.y - r.width * 0.5f + 3, r.height - 6, r.width - 6)
                    : new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6);
                GUI.DrawTexture(rr, item.icon.texture, ScaleMode.ScaleToFit);
                GUI.matrix = mtx;
            }
            else GUI.DrawTexture(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), item.icon.texture, ScaleMode.ScaleToFit);
        }
        else { nameStyle.normal.textColor = item.RarityColor(); GUI.Label(new Rect(r.x + 3, r.y + 3, r.width - 6, r.height - 6), item.itemName, nameStyle); }
        if (count > 1) GUI.Label(new Rect(r.x, r.y, r.width - 3, r.height - 2), count.ToString(), countStyle);
    }

    private static void EnsureStyles()
    {
        if (countStyle != null) return;
        countStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, fontSize = 13 };
        countStyle.normal.textColor = Color.white;
        nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 10, wordWrap = true };
    }
}
