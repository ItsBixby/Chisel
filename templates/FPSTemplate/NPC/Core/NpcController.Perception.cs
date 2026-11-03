using Chisel.Utils;
using Engine;
using Engine.Utils;
using FPSTemplate.Entities;
using Microsoft.Xna.Framework;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FPSTemplate.NPC.Core;

public class PerceptionEntry
{
    public WorldEntity Entity { get; set; } = null!;
    public float LastSeenTime { get; set; }
    public float ThreatLevel { get; set; }
    public Vector3 LastKnownPosition { get; set; }
    public bool IsCurrentlyVisible { get; set; }

    public bool IsRecent => LastSeenTime < 3f;
}

public partial class NpcController
{
    public List<PerceptionEntry> Threats { get; } = new();
    public WorldEntity BestThreat { get; internal set; }
    public bool HeardSomething { get; internal set; }
    public Vector3 HeardPosition { get; internal set; }

    private float perceptionTimer;

    public bool CanSee(WorldEntity target)
    {
        if (target == null) return false;

        Vector3 toTarget = target.Position - entity.Position;
        float dist = toTarget.Length();

        if (dist > Definition.SightRange) return false;

        var forward = -Matrix.CreateRotationY(aiLookAngle * CMath.Deg2Rad).Forward;
        float cosHalf = MathF.Cos(MathHelper.ToRadians(Definition.SightFOVDegrees * 0.5f));
        if (Vector3.Dot(Vector3.Normalize(toTarget), Vector3.Normalize(forward)) < cosHalf)
            return false;

        var ray = new Ray(entity.Position, Vector3.Normalize(toTarget));
        return !BSPRoot.TraceRay(ray, dist).Hit;
    }

    public bool CanHear(Vector3 position, float intensityScale = 1f) =>
        Vector3.Distance(entity.Position, position) < Definition.HearRange * intensityScale;

    public void RegisterThreat(WorldEntity source, float threatBoost = 0.5f)
    {
        if (source == entity) return;

        var theirFaction = FactionManager.GetFaction(source);
        if (!Faction.IsHostileTo(theirFaction)) return;

        PerceptionEntry existing = null;
        for (int i = 0; i < Threats.Count; i++)
        {
            if (Threats[i].Entity == source)
            {
                existing = Threats[i];
                break;
            }
        }

        if (existing != null)
        {
            existing.ThreatLevel = MathF.Min(1f, existing.ThreatLevel + threatBoost * 0.5f);
            existing.LastKnownPosition = source.Position;
            existing.LastSeenTime = 0f;
            existing.IsCurrentlyVisible = true;
        }
        else
        {
            Threats.Add(new PerceptionEntry
            {
                Entity = source,
                ThreatLevel = MathF.Min(1f, threatBoost),
                LastSeenTime = 0f,
                LastKnownPosition = source.Position,
                IsCurrentlyVisible = true
            });
        }

        if (source.Controller is LivingActor actor && actor.IsDead)
        {
            Squad?.ClearTarget();
            return;
        }

        LastKnownTargetPosition = source.Position;
    }

    internal void UpdatePerception(float dt)
    {
        perceptionTimer -= dt;
        if (perceptionTimer > 0f) return;
        perceptionTimer = Definition.PerceptionTickRate;

        float tick = Definition.PerceptionTickRate;

        for (int i = Threats.Count - 1; i >= 0; i--)
        {
            var t = Threats[i];
            t.LastSeenTime += tick;
            t.IsCurrentlyVisible = CanSee(t.Entity);

            if (t.IsCurrentlyVisible)
            {
                t.LastSeenTime = 0f;
                t.LastKnownPosition = t.Entity.Position;
                t.ThreatLevel = MathF.Min(1f, t.ThreatLevel + 0.1f);
                LastKnownTargetPosition = t.LastKnownPosition;
            }

            if (t.Entity.Controller is LivingActor actor && actor.IsDead)
            {
                Threats.RemoveAt(i);
                Squad?.ClearTarget();
            }

            if (t.LastSeenTime > Definition.LostTargetTimeout + 2f)
                Threats.RemoveAt(i);
        }

        var nearby = Collision.GetEntitiesInSphere(entity.Position, Definition.SightRange);
        foreach (var e in nearby)
        {
            if (e == entity) continue;
            if (Threats.Any(t => t.Entity == e)) continue;
            if (!ShouldTrackEntity(e)) continue;

            if (CanSee(e))
                RegisterThreat(e, 0.3f);
            else if (CanHear(e.Position, GetEntityNoiseLevel(e)))
            {
                HeardSomething = true;
                HeardPosition = e.Position;
            }
        }
    }

    protected virtual bool ShouldTrackEntity(WorldEntity e)
    {
        if (e.Controller is not LivingActor actor || actor.IsDead) return false;
        if (GameEngine.AINoTarget && e.Controller is BasePlayerController) return false;

        var theirFaction = FactionManager.GetFaction(e);
        if (theirFaction != null && !Faction.IsHostileTo(theirFaction)) return false;

        return true;
    }

    protected virtual float GetEntityNoiseLevel(WorldEntity e)
    {
        float speed = new Vector2(e.Velocity.X, e.Velocity.Z).Length();
        return speed > 0.5f ? 1f : 0f;
    }
}