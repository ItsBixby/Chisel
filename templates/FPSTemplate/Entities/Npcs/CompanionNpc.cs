using Engine;
using Engine.Compilation;
using Engine.Utils;
using FPSTemplate.Entities;
using FPSTemplate.NPC;
using FPSTemplate.NPC.Core;
using FPSTemplate.Weapons;
using Microsoft.Xna.Framework;
using Rockwall;
using System;
using System.Collections.Generic;

namespace FPSTemplate.Entities.Npcs;

[EntityDescriptor]
[EntityBounds(-0.25f, -2.00f, -0.25f, 0.25f, 0.15f, 0.25f)]
public class CompanionNpc : WorldEntity
{
    public NpcController NpcController => (NpcController)Controller;
    static readonly BoundingBox UncrouchedBounds = new BoundingBox(-new Vector3(0.25f, 2.0f, 0.25f), new Vector3(0.25f, 0.15f, 0.25f));
    public CompanionNpc()
    {
        var npc = new CompanionController();
        npc.Faction = FactionManager.Rebels;
        npc.JoinSquad(NpcSquads.PlayerSquad);
        npc.AddWeapon(new WeaponSMG());
        Controller = npc;

        AxisAlignedBox = true;
        IsSimulated = true;
        Bounds = UncrouchedBounds;
    }
}
public class CompanionController : NpcController
{
    private const float LeashRadius = 13f;
    private const float FollowStopDist = 8f;
    private const float MaxRoamCombat = 5f;

    private float alertTimer;
    private float lostTimer;
    private float burstTimer;
    private float roamCombatTimer;
    private int sweepCount;
    private bool sweepPointSet;
    private bool isRunningWhileFollowing;

    private const float StrafeIntervalMin = 1.5f;
    private const float StrafeIntervalMax = 3.0f;
    private float strafeTimer;
    private float strafeDuration;
    private Vector3 strafeTarget;
    private bool strafeTargetSet;

    protected override NpcDefinition CreateDefinition() => new NpcDefinition
    {
        MaxHP = 60,
        RunSpeed = 5.5f,
        OptimalCombatRange = 9f,
        ModelPath = "Models/Player/npc_base.ccmdl",
    };

    protected override void OnSetup()
    {
        AddBarkLine("contact", "Contact!", "NPC.Friend.Contact");
        AddBarkLine("pain", "I'm hit!", "NPC.Friend.Pain");
        AddBarkLine("dead", "I'm hit!", "NPC.Friend.Die");
        AddBarkLine("with_you", "Right behind you!", "NPC.Friend.Follow");
        AddBarkLine("suppressing", "Suppressing!", "NPC.Friend.Suppress");
        AddBarkLine("lost", "Lost visual!", "NPC.Friend.Lost");
        AddBarkLine("flanking", "Going wide!", "NPC.Friend.Flank");
        AddBarkLine("covering", "Cover me!", "NPC.Friend.Cover");

        DefineState("idle",
            enter: () =>
            {
                SetSpeed(SpeedPreset.Walk);
            },
            tick: dt =>
            {
                if (BestThreat != null && CanSee(BestThreat)) return "alert";
                if (HeardSomething) return "investigate";

                WorldEntity player = FindPlayer();
                if (player == null) return null;

                FaceMovementDirection();

                float distToPlayer = Vector3.Distance(entity.Position, player.Position);

                if (distToPlayer > FollowStopDist * 0.5f)
                {
                    Vector3 formationTarget = GetFormationPosition(player, 1.5f);
                    float distToSlot = Vector3.Distance(entity.Position, formationTarget);

                    if (distToSlot > LeashRadius * 0.5f) isRunningWhileFollowing = true;
                    if (distToSlot < FollowStopDist) isRunningWhileFollowing = false;

                    SetSpeed(isRunningWhileFollowing ? SpeedPreset.Run : SpeedPreset.Walk);

                    if (distToSlot > 0.6f)
                        MoveTo(formationTarget);
                    else
                        StopMoving();

                    if (distToPlayer > LeashRadius * 0.5f)
                        Bark("with_you");
                }
                else
                {
                    StopMoving();
                }

                return null;
            });

        DefineState("investigate",
            enter: () =>
            {
                SetSpeed(SpeedPreset.Walk);
                MoveTo(HeardPosition);
            },
            tick: dt =>
            {
                if (BestThreat != null && CanSee(BestThreat)) return "alert";
                if (IsPathCompleted()) return "idle";

                // If the player gets too far while we're investigating, abort
                WorldEntity player = FindPlayer();
                if (player != null && Vector3.Distance(entity.Position, player.Position) > LeashRadius * 1.5f)
                    return "idle";
                return null;
            });

        DefineState("alert",
            enter: () =>
            {
                alertTimer = 0f;
                SetSpeed(SpeedPreset.Run);
                Bark("contact");
                if (BestThreat != null) Follow(BestThreat);
            },
            tick: dt =>
            {
                alertTimer += dt;
                if (BestThreat == null || !CanSee(BestThreat)) return "search";
                if (alertTimer >= Definition.ReactionTime) return "combat";
                return null;
            });

        DefineState("combat",
            isCombatState: true,
            enter: () =>
            {
                lostTimer = 0f;
                burstTimer = -Random.Shared.NextSingle() * 0.6f;
                roamCombatTimer = 0f;
                strafeTargetSet = false;
                strafeTimer = 0f;
                strafeDuration = StrafeIntervalMin + Random.Shared.NextSingle() * (StrafeIntervalMax - StrafeIntervalMin);
                SetSpeed(SpeedPreset.Run);
            },
            tick: dt =>
            {
                if (BestThreat == null) return "search";

                bool hasLos = CanSee(BestThreat);
                lostTimer = hasLos ? 0f : lostTimer + dt;
                if (lostTimer >= Definition.LostTargetTimeout) return "search";

                WorldEntity player = FindPlayer();
                float distToPlayer = player != null
                    ? Vector3.Distance(entity.Position, player.Position)
                    : 0f;

                // If we've drifted too far from the player, break off and return
                if (distToPlayer > LeashRadius * 1.5f)
                    return "idle";

                if (hasLos)
                {
                    LastKnownTargetPosition = BestThreat.Position;

                    bool isSuppressing = Squad.TryClaim(this, "suppress");
                    if (isSuppressing)
                    {
                        StopMoving();
                        AddLookTarget(BestThreat.Position, 0f);
                        TickBurstFire(dt);
                        Bark("suppressing");

                        // Release suppress after a while so we don't get pinned forever
                        roamCombatTimer += dt;
                        if (roamCombatTimer >= MaxRoamCombat)
                        {
                            Squad.Release(this, "suppress");
                            roamCombatTimer = 0f;
                        }
                    }
                    else
                    {
                        TickCombatMovement(dt, player);
                        TickBurstFire(dt);
                    }
                }
                else
                {
                    Follow(BestThreat);
                }

                return null;
            },
            exit: () =>
            {
                Squad.ReleaseAll(this);
                FaceMovementDirection();
            });

        DefineState("search",
            enter: () =>
            {
                sweepCount = 0;
                sweepPointSet = false;
                SetSpeed(SpeedPreset.Walk);
                Bark("lost");
            },
            tick: dt =>
            {
                if (BestThreat != null && CanSee(BestThreat)) return "alert";

                // Don't wander too far searching
                WorldEntity player = FindPlayer();
                if (player != null && Vector3.Distance(entity.Position, player.Position) > LeashRadius)
                    return "idle";

                if (IsPathCompleted() || !sweepPointSet) AdvanceSweep();
                return null;
            });
    }

    private void TickCombatMovement(float dt, WorldEntity player)
    {
        float distToThreat = Vector3.Distance(entity.Position, BestThreat.Position);
        float optimal = Definition.OptimalCombatRange;

        float distToPlayer = player != null
            ? Vector3.Distance(entity.Position, player.Position)
            : 0f;

        if (distToThreat > optimal * 1.2f && distToPlayer < LeashRadius)
        {
            Follow(BestThreat);
            strafeTargetSet = false;
        }
        else if (distToThreat < optimal * 0.6f)
        {
            MoveTo(entity.Position + Vector3.Normalize(entity.Position - BestThreat.Position) * optimal);
            strafeTargetSet = false;
        }
        else
        {
            TickStrafe(dt);
        }

        AddLookTarget(BestThreat.Position, 0f);
    }

    private void TickStrafe(float dt)
    {
        strafeTimer += dt;
        if (strafeTimer < strafeDuration && strafeTargetSet && !IsPathCompleted())
            return;

        strafeTimer = 0f;
        strafeDuration = StrafeIntervalMin + Random.Shared.NextSingle() * (StrafeIntervalMax - StrafeIntervalMin);

        Vector3 toThreat = BestThreat.Position - entity.Position;
        toThreat.Y = 0;
        if (toThreat.LengthSquared() < 0.001f) return;
        toThreat.Normalize();

        Vector3 leftDir = new Vector3(-toThreat.Z, 0f, toThreat.X);
        Vector3 rightDir = new Vector3(toThreat.Z, 0f, -toThreat.X);
        Vector3 chosen = Random.Shared.NextSingle() > 0.5f ? leftDir : rightDir;

        float step = 3f + Random.Shared.NextSingle() * 3f;
        Vector3 candidate = entity.Position + chosen * step;
        var floorHit = BSPRoot.TraceRay(new Ray(candidate + Vector3.Up, Vector3.Down), 4f, false);
        if (!floorHit.Hit)
        {
            candidate = entity.Position + (chosen == leftDir ? rightDir : leftDir) * step;
            floorHit = BSPRoot.TraceRay(new Ray(candidate + Vector3.Up, Vector3.Down), 4f, false);
            if (!floorHit.Hit) return;
        }

        strafeTarget = floorHit.Point;
        strafeTargetSet = true;
        MoveTo(strafeTarget);
        Bark("flanking");
    }

    private void TickBurstFire(float dt)
    {
        burstTimer -= dt;

        bool wantsFire = burstTimer >= 0f;

        // Gate on squad fire discipline, at most 2 companions fire at once.
        if (wantsFire && Squad != null && !Squad.CanFire(this, 2))
            wantsFire = false;

        IsFiring = wantsFire;

        if (burstTimer < -0.8f) burstTimer = 0.25f;
        if (!wantsFire) return;

        if (BestThreat != null &&
            Vector3.Distance(entity.Position, BestThreat.Position) <= Definition.AttackRange)
        {
            FaceBodyToward(BestThreat.Position);
            FireAt(BestThreat);
        }
    }

    private void AdvanceSweep()
    {
        sweepCount++;
        sweepPointSet = true;

        float radius = 2f + sweepCount * 6f;
        float angle = Random.Shared.NextSingle() * MathF.Tau;
        Vector3 target = LastKnownTargetPosition +
                         new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);

        Vector3 dir = target - LastKnownTargetPosition;
        float len = dir.Length();
        var hit = BSPRoot.TraceRay(new Ray(LastKnownTargetPosition, dir / len), len);
        if (hit.Hit) target = hit.Point;

        MoveTo(target);
        FaceMovementDirection();

        if (sweepCount >= 3) { sweepCount = 0; sweepPointSet = false; }
    }

    private static WorldEntity FindPlayer()
    {
        foreach (var e in EntityManager.entities.GetValues())
            if (e?.Controller is BasePlayerController)
                return e;
        return null;
    }
}