using Chisel.Utils;
using Engine;
using Engine.Rendering;
using Engine.Sound;
using Engine.Utils;
using FontStashSharp;
using EngineTesting.Entities;
using EngineTesting.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineTesting.NPC.Core;
public partial class NpcController
{
    private const float PhysSepRadius = 1.9f;
    private const float PhysSepStrength = 30f; 

    public void MakeWay(Vector3 fromPosition, float radius, float urgency = 1f)
    {
        Vector3 away = entity.Position - fromPosition;
        away.Y = 0f;

        float dist = away.Length();
        if (dist > radius || dist < 0.001f) return;

        float strength = (1f - dist / radius) * urgency * PhysSepStrength;
        entity.Velocity += Vector3.Normalize(away) * strength * MainEngine.PreviousFrameDelta;
    }

    public static void PushNpcsInRadius(Vector3 center, float radius, float urgency = 1f)
    {
        foreach (var e in Collision.GetEntitiesInSphere(center, radius))
            if (e.Controller is NpcController npc && !npc.IsDead)
                npc.MakeWay(center, radius, urgency);
    }


    // Staggered slots in leader-local space. X = right, Z = forward
    // (negative Z = behind leader). Slot 0 is reserved for the leader's own
    // entity so members start at index 1 and map to slots 0..N-1.

    private static readonly Vector3[] FormationSlots =
    [
        new(-1.5f, 0f, -1.8f),   // slot 0 back-left
        new( 1.5f, 0f, -1.8f),   // slot 1 back-right
        new(-2.5f, 0f, -0.4f),   // slot 2 mid-left
        new( 2.5f, 0f, -0.4f),   // slot 3 mid-right
        new(-1.5f, 0f, -3.6f),   // slot 4 far back-left
        new( 1.5f, 0f, -3.6f),   // slot 5 far back-right
        new(-3.4f, 0f, -1.8f),   // slot 6 wide left
        new( 3.4f, 0f, -1.8f),   // slot 7 wide right
    ];

    /// <summary>
    /// Returns the world-space position this NPC should move toward when
    /// following <paramref name="leader"/>.
    /// </summary>
    public Vector3 GetFormationPosition(WorldEntity leader, float distanceScalar = 1f)
    {
        // Stable slot index: count living members before us in the squad list.
        int slot = 0;
        if (Squad != null)
        {
            foreach (var m in Squad.Members)
            {
                if (m.IsDead) continue;
                if (m == this) break;
                slot++;
            }
            slot = Math.Min(slot, FormationSlots.Length - 1);
        }

        // Derive the leader's facing from their rotation, projected onto XZ.
        Vector3 fwd = Vector3.Transform(-Vector3.UnitZ, leader.Rotation);
        Vector3 right = Vector3.Transform(Vector3.UnitX, leader.Rotation);

        fwd.Y = 0f; if (fwd.LengthSquared() > 0.001f) fwd.Normalize();
        right.Y = 0f; if (right.LengthSquared() > 0.001f) right.Normalize();

        var offset = FormationSlots[slot]* distanceScalar;
        var targ = leader.Position + right * offset.X + fwd * offset.Z;
        return AINodeUtils.IsPathWalkable(entity.Position,targ, out _) ? targ : leader.Position;
    }

    /// <summary>
    /// True while this NPC is actively pulling the trigger.
    /// </summary>
    public bool IsFiring { get; protected set; }
}
