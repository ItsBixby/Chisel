using Engine;
using Engine.Compilation;
using Engine.Utils;
using EngineTesting.NPC;
using EngineTesting.NPC.Core;
using EngineTesting.Weapons;
using Microsoft.Xna.Framework;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineTesting.Entities.Npcs;

[EntityDescriptor]
[EntityBounds(-0.25f, -2.00f, -0.25f, 0.25f, 0.15f, 0.25f)]
[ExposeEntityProperty("Actor", EntityPropertyType.Bool, "This NPC will be used for acting until explicitly told not to.")]
public class GruntEnemy : WorldEntity
{
    static readonly BoundingBox UncrouchedBounds = new BoundingBox(
        -new Vector3(0.25f, 2.0f, 0.25f),
         new Vector3(0.25f, 0.15f, 0.25f));

    public NpcController NpcController => (NpcController)Controller;

    public GruntEnemy()
    {
        Controller = new GruntController();

        var npc = NpcController;
        npc.Faction = FactionManager.Grunts;
        npc.JoinSquad(NpcSquads.GruntSquad);
        npc.AddWeapon(new WeaponSMG());

        Bounds = UncrouchedBounds;
        AxisAlignedBox = true;
        IsSimulated = true;
    }
}

public class GruntController : NpcController
{
    private float alertTimer;
    private float lostTimer;
    private float burstTimer;
    private int sweepCount;
    private bool sweepPointSet;

    private const float StrafeIntervalMin = 1.8f;
    private const float StrafeIntervalMax = 3.5f;
    private float strafeTimer;
    private float strafeDuration;
    private Vector3 strafeTarget;
    private bool strafeTargetSet;

    private const float ReposIntervalMin = 4.0f;
    private const float ReposIntervalMax = 8.0f;
    private float reposTimer;
    private float reposDuration;

    private const float GrenadeMinCooldown = 3f;
    private const float GrenadeMaxCooldown = 8f;
    private const float GrenadeMaxRange = 26f;
    private const float GrenadeMinRange = 3f;

    private float grenadeTimer;
    private float steadyTimer;
    private Vector3 lastTargetPos;

    protected override NpcDefinition CreateDefinition() => new NpcDefinition
    {
        MaxHP = 48,
        RunSpeed = 5f,
        OptimalCombatRange = 10f,
        ModelPath = "Models/Player/npc_base.ccmdl",
    };

    protected override void OnSetup()
    {
        AddBarkLine("contact", "Contact!", "NPC.Enemy.Contact");
        AddBarkLine("pain", "Argh!", "NPC.Enemy.Pain");
        AddBarkLine("dead", "Argh!", "NPC.Enemy.Die");
        AddBarkLine("suppressing", "Suppressing!", "NPC.Enemy.Suppress");
        AddBarkLine("grenade", "Frag out!", "NPC.Enemy.Frag");
        AddBarkLine("lost", "Where'd he go?", "NPC.Enemy.Lost");
        AddBarkLine("flanking", "Flanking!", "NPC.Enemy.Flank");

        DefineState("idle",
            enter: () =>
            {
                StopMoving();
                SetSpeed(SpeedPreset.Walk);
            },
            tick: dt =>
            {
                if (BestThreat != null && CanSee(BestThreat)) return "alert";
                if (HeardSomething) return "investigate";
                if (ShouldRewander()) MoveTo(PickWanderPoint());
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
                burstTimer = -Random.Shared.NextSingle() * 0.8f;
                reposDuration = ReposIntervalMin + Random.Shared.NextSingle() * (ReposIntervalMax - ReposIntervalMin);
                reposTimer = 0f;
                grenadeTimer = GrenadeMinCooldown + Random.Shared.NextSingle() * (GrenadeMaxCooldown - GrenadeMinCooldown);
                lastTargetPos = BestThreat?.Position ?? entity.Position;
                steadyTimer = 0f;
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

                bool isSuppressor = Squad.TryClaim(this, "suppress");
                bool isAdvancer = !isSuppressor && Squad.TryClaim(this, "advance");

                if (hasLos)
                {
                    LastKnownTargetPosition = BestThreat.Position;
                    TickGrenadeSteadiness(dt);

                    grenadeTimer -= dt;

                    // Only the suppressor or someone with the grenade slot throws grenades
                    // so multiple grunts don't lob simultaneously.
                    bool hasGrenadeSlot = Squad.TryClaim(this, "grenade");
                    if (hasGrenadeSlot && grenadeTimer <= 0f && TryThrowGrenade())
                    {
                        grenadeTimer = GrenadeMinCooldown + Random.Shared.NextSingle() * (GrenadeMaxCooldown - GrenadeMinCooldown);
                        Squad.Release(this, "grenade");
                    }

                    reposTimer += dt;

                    if (isSuppressor)
                    {
                        // Suppressor holds position and fires constantly
                        float targDist = Vector3.DistanceSquared(BestThreat.Position, entity.Position);

                        if(targDist < 30)
                        {
                            StopMoving();
                        }
                        else
                        {
                            var pos = targDist * 0.5f * Vector3.Normalize(BestThreat.Position-entity.Position) + entity.Position;
                            MoveTo(pos);
                        }

                        FaceBodyToward(BestThreat.Position);
                        AddLookTarget(BestThreat.Position, 0f);
                        TickBurstFire(dt);
                    }
                    else if (isAdvancer && reposTimer < reposDuration)
                    {
                        // Advancer pushes covered by the suppressor
                        Follow(BestThreat);
                        AddLookTarget(BestThreat.Position, 0f);
                        TickBurstFire(dt);

                        reposDuration = ReposIntervalMin + Random.Shared.NextSingle() * (ReposIntervalMax - ReposIntervalMin);
                    }
                    else
                    {
                        TickCombatMovement(dt);
                        TickBurstFire(dt);
                    }
                }
                else
                {
                    // Navigate toward the last known position to try to regain LOS.
                    MoveTo(LastKnownTargetPosition);
                    AddLookTarget(LastKnownTargetPosition, 0.5f);
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
                Squad.ReleaseAll(this);

                sweepCount = 0;
                sweepPointSet = false;
                SetSpeed(SpeedPreset.Walk);
                Bark("lost");
            },
            tick: dt =>
            {
                if (BestThreat != null && CanSee(BestThreat)) return "alert";
                if (IsPathCompleted() || !sweepPointSet) AdvanceSweep();
                return null;
            });


        if (entity.ReadProperty("Actor", EntityPropertyType.Bool) is true)
            SetPermaActing(true);
    }

    private void TickCombatMovement(float dt)
    {
        float dist = Vector3.Distance(entity.Position, BestThreat.Position);
        float optimal = Definition.OptimalCombatRange;

        if (dist > optimal * 1.2f)
        {
            Follow(BestThreat);
            strafeTargetSet = false;
        }
        else if (dist < optimal * 0.6f)
        {
            Vector3 backTarget = entity.Position + Vector3.Normalize(entity.Position - BestThreat.Position) * optimal;
            MoveTo(backTarget);
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
        bool timerExpired = strafeTimer >= strafeDuration;
        bool needsNewTarget = !strafeTargetSet || (strafeTargetSet && IsPathCompleted());

        if (!timerExpired && !needsNewTarget)
            return;

        strafeTimer = 0f;
        strafeDuration = StrafeIntervalMin + Random.Shared.NextSingle() * (StrafeIntervalMax - StrafeIntervalMin);

        if (BestThreat == null) return;

        Vector3 toThreat = BestThreat.Position - entity.Position;
        toThreat.Y = 0;
        if (toThreat.LengthSquared() < 0.001f) return;
        toThreat.Normalize();

        Vector3 leftDir = new Vector3(-toThreat.Z, 0f, toThreat.X);
        Vector3 rightDir = new Vector3(toThreat.Z, 0f, -toThreat.X);

        Vector3 chosenDir = (strafeTargetSet && Random.Shared.NextSingle() > 0.4f)
            ? (Vector3.Dot(entity.Position - BestThreat.Position, leftDir) > 0 ? rightDir : leftDir)
            : (Random.Shared.NextSingle() > 0.5f ? leftDir : rightDir);

        float stepDist = 3f + Random.Shared.NextSingle() * 3f;
        Vector3 candidate = entity.Position + chosenDir * stepDist;

        var floorHit = BSPRoot.TraceRay(new Ray(candidate + Vector3.Up, Vector3.Down), 4f, false);
        if (!floorHit.Hit)
        {
            candidate = entity.Position + (chosenDir == leftDir ? rightDir : leftDir) * stepDist;
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

    private void TickGrenadeSteadiness(float dt)
    {
        if (BestThreat == null) return;
        float moved = Vector3.Distance(BestThreat.Position, lastTargetPos);
        if (moved < 1.0f)
            steadyTimer += dt;
        else
        {
            steadyTimer = 0f;
            lastTargetPos = BestThreat.Position;
        }
    }

    private bool TryThrowGrenade()
    {
        if (BestThreat == null) return false;
        float dist = Vector3.Distance(entity.Position, BestThreat.Position);
        if (dist < GrenadeMinRange || dist > GrenadeMaxRange) return false;

        // Estimate flight time based on horizontal distance, then lead the target.
        Vector2 horizDelta = new Vector2(
            BestThreat.Position.X - entity.Position.X,
            BestThreat.Position.Z - entity.Position.Z);
        float flightTime = MathF.Max(0.8f, horizDelta.Length() / 50f);

        Vector3 targetVel = BestThreat.Velocity;
        targetVel.Y = 0f;
        Vector3 predictedPos = BestThreat.Position + targetVel * flightTime;

        bool thrown = ThrowGrenade(predictedPos);
        if (thrown) Bark("grenade");
        return thrown;
    }

    private Vector3 PickCoverPoint()
    {
        Vector3 threatPos = LastKnownTargetPosition;
        Vector3 awayDir = entity.Position - threatPos;
        awayDir.Y = 0;
        if (awayDir.LengthSquared() > 0.001f) awayDir.Normalize();
        else awayDir = Vector3.Forward;

        Vector3 best = entity.Position;
        float bestScore = float.MinValue;

        for (int i = 0; i < 16; i++)
        {
            float spreadAngle = MathHelper.ToRadians(Random.Shared.NextSingle() * 120f - 60f);
            float radius = 3f + Random.Shared.NextSingle() * 7f;

            Vector3 dir = Vector3.Transform(awayDir, Matrix.CreateRotationY(spreadAngle));
            Vector3 candidate = entity.Position + dir * radius;

            var floorHit = BSPRoot.TraceRay(new Ray(candidate + Vector3.Up * 2f, Vector3.Down), 6f, false);
            if (!floorHit.Hit) continue;
            candidate = floorHit.Point;

            // Strong bonus if a wall breaks LOS between this spot and the threat
            bool losBlocked = BSPRoot.TraceRay(
                new Ray(candidate + Vector3.Up * 1.0f,
                        Vector3.Normalize(threatPos - candidate)), 40f).Hit;

            float score = 0f;
            score += losBlocked ? 4f : 0f;
            score -= Vector3.Distance(entity.Position, candidate) * 0.3f;   // prefer nearby
            score += Vector3.Distance(threatPos, candidate) * 0.05f;        // slight bonus for range from threat

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
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
}