using Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPSTemplate.Utilities;

public static class DamageBuffer
{
    private struct PendingDamage
    {
        public DamageInfo info;
        public Action? onFirstHit;
    }

    private static readonly Dictionary<(WorldEntity from, WorldEntity target), PendingDamage> pending = new();

    /// <param name="target">The entity receiving damage.</param>
    /// <param name="info">Damage details. If info.from is null, damage is applied immediately.</param>
    /// <param name="onFirstHit">
    /// Optional. Invoked once when the first hit of this attacker/target pair is
    /// registered this frame
    /// </param>
    public static void Queue(WorldEntity target, DamageInfo info, Action? onFirstHit = null)
    {
        if (info.from == null)
        {
            onFirstHit?.Invoke();
            target.TakeDamage(info);
            return;
        }

        var key = (info.from, target);
        if (pending.TryGetValue(key, out var existing))
        {
            existing.info.damage += info.damage;
            // Keep the first hit location and discard the new callback
            // effects have already been claimed by the first hit.
            pending[key] = existing;
        }
        else
        {
            pending[key] = new PendingDamage { info = info, onFirstHit = onFirstHit };
        }
    }

    /// <summary>
    /// Apply all buffered damage and fire pending first-hit callbacks.
    /// Call once per frame after all gameplay logic has run.
    /// </summary>
    public static void Flush()
    {
        foreach (var (key, entry) in pending)
        {
            entry.onFirstHit?.Invoke();
            key.target.TakeDamage(entry.info);
        }

        pending.Clear();
    }
}