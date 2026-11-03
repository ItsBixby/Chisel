using Engine;
using Microsoft.Xna.Framework;
using Rockwall;
using System.Collections.Generic;
using System.Linq;

namespace FPSTemplate.NPC.Core;
public class NpcSquad
{
    private readonly List<NpcController> members = new();

    private readonly Dictionary<string, NpcController> claimedSlots = new();

    public IReadOnlyList<NpcController> Members => members;
    public int LivingCount => members.Count(m => !m.IsDead);
    public int CombatCount => members.Count(m => !m.IsDead && m.IsInCombat);

    public WorldEntity SharedTarget { get; private set; }
    public Vector3 LastKnownTargetPosition { get; private set; }
    public bool HasActiveTarget => SharedTarget != null;

    public int CurrentVisibilityCount =>
        members.Count(m => !m.IsDead && m.BestThreat == SharedTarget && m.CanSee(m.BestThreat));

    public void Add(NpcController m)
    {
        if (!members.Contains(m)) members.Add(m);
    }

    public void Remove(NpcController m)
    {
        members.Remove(m);
        var toRemove = claimedSlots.Where(kv => kv.Value == m).Select(kv => kv.Key).ToList();
        foreach (var k in toRemove) claimedSlots.Remove(k);
    }

    public bool TryClaim(NpcController m, string slot)
    {
        if (claimedSlots.TryGetValue(slot, out var holder))
        {
            if (holder == m) return true;
            if (!holder.IsDead) return false;
        }
        claimedSlots[slot] = m;
        return true;
    }

    public void Release(NpcController m, string slot)
    {
        if (claimedSlots.TryGetValue(slot, out var holder) && holder == m)
            claimedSlots.Remove(slot);
    }

    public void ReleaseAll(NpcController m)
    {
        var toRemove = claimedSlots.Where(kv => kv.Value == m).Select(kv => kv.Key).ToList();
        foreach (var k in toRemove) claimedSlots.Remove(k);
    }

    public bool HasSlot(string slot) =>
        claimedSlots.TryGetValue(slot, out var h) && !h.IsDead;

    public NpcController GetSlotHolder(string slot) =>
        claimedSlots.TryGetValue(slot, out var h) && !h.IsDead ? h : null;

    public bool HoldsSlot(NpcController m, string slot) =>
        claimedSlots.TryGetValue(slot, out var h) && h == m;
    
    public void UpdateLastKnown(Vector3 pos) => LastKnownTargetPosition = pos;

    public void ClearTarget() => SharedTarget = null;

    public void Broadcast(NpcController source, string signal, object data = null)
    {
        foreach (var m in members)
        {
            if (m == source || m.IsDead) continue;
            m.Signal(signal, data);
        }
    }
    public (Vector3 position, float distance) GetNearestSquadMate(Vector3 to)
    {
        float minD = float.MaxValue;
        Vector3 pos = to;

        if (members.Count < 2)
            return (pos, minD);

        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].IsDead || members[i].entity == null) continue;

            var d = Vector3.DistanceSquared(members[i].entity.Position, to);

            // epsilon so we dont pick ourselves
            if (d < minD && d > 0.01f)
            {
                minD = d;
                pos = members[i].entity.Position;
            }
        }

        return (pos, minD);
    }
    public float GetAngularSpread(Vector3 around)
    {
        if (members.Count < 2) return 0f;
        float total = 0f;
        int pairs = 0;
        for (int i = 0; i < members.Count; i++)
        {
            if (members[i].IsDead) continue;
            for (int j = i + 1; j < members.Count; j++)
            {
                if (members[j].IsDead) continue;
                var da = Vector3.Normalize(members[i].entity.Position - around);
                var db = Vector3.Normalize(members[j].entity.Position - around);
                total += float.Acos(MathHelper.Clamp(Vector3.Dot(da, db), -1f, 1f));
                pairs++;
            }
        }
        return pairs == 0 ? 0f : total / pairs;
    }
    public bool CanFire(NpcController member, int maxSimultaneous = 2)
    {
        int count = 0;
        foreach (var m in members)
        {
            if (m == member || m.IsDead) continue;
            if (m.IsFiring) count++;
        }
        return count < maxSimultaneous;
    }
}
