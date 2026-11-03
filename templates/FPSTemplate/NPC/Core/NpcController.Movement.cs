using Chisel.Utils;
using Engine;
using Engine.Utils;
using Microsoft.Xna.Framework;
using Rockwall;
using System;

namespace FPSTemplate.NPC.Core;

public partial class NpcController
{
    private object followTarget;
    private bool wantsToMove;
    private bool wasGrounded;
    private Vector3? lookOverride;

    private Vector3 wishDir;

    private Vector3 lastCommittedWishDir;
    private float turnTargetMultiplier;

    private bool isRealigningLegs;
    private const float LegTurnThresholdDeg = 70f;

    private const float TurnSlowdownMinMultiplier = 0.45f;

    private bool shouldMoveThisFrame;

    private readonly record struct SteerVote(Vector3 Direction, float Weight, bool IsDanger);
    private readonly System.Collections.Generic.List<SteerVote> steerVotes = new();

    public Vector3 LastKnownTargetPosition { get; internal set; }

    public void MoveTo(Vector3 point)
    {
        followTarget = null;
        wantsToMove = true;
        if (Vector3.Distance(point, currentTarget) > 0.5f)
            FindNewPath(point);
    }

    public void Follow(WorldEntity target)
    {
        bool isNew = followTarget != target;
        followTarget = target;
        wantsToMove = true;

        Vector3 pos = target.Position;
        var hit = BSPRoot.TraceRay(new Ray(pos, Vector3.Down), 100f, false);
        if (hit.Hit) pos = hit.Point;

        if (isNew || Vector3.Distance(pos, currentTarget) > 0.8f)
            FindNewPath(pos);
    }
    public void Teleport(Vector3 point)
    {
        entity.Position = point;
        entity.Velocity = Vector3.Zero;
        followTarget = null;
        wantsToMove = false;
        currentTarget = point;
        pathCompleted = true;
    }
    public void StopMoving() => wantsToMove = false;

    public void AddInterest(Vector3 direction, float weight) =>
        steerVotes.Add(new SteerVote(direction, weight, false));

    public void AddDanger(Vector3 direction, float weight) =>
        steerVotes.Add(new SteerVote(direction, weight, true));

    private void MoveStep()
    {
        shouldMoveThisFrame = ShouldTakeMoveStep();
        if (!shouldMoveThisFrame)
        {
            if (!wantsToMove || pathCompleted)
            {
                ApplyFriction();
                steerVotes.Clear();

                movePathLengthMultiplier = 0;
                lastCommittedWishDir = Vector3.Zero;

                return;
            }

            goto moveBody;
        }

        if (followTarget is WorldEntity follow)
        {
            Vector3 pos = follow.Position;
            var fhit = BSPRoot.TraceRay(new Ray(pos, Vector3.Down), 100f, false);
            if (fhit.Hit) pos = fhit.Point;
            if (Vector3.Distance(pos, currentTarget) > 0.8f) FindNewPath(pos);
        }

        if (!wasGrounded && entity.IsOnGround && wantsToMove)
        {
            Vector3 dest = followTarget is WorldEntity fe ? fe.Position : currentTarget;
            FindNewPath(dest);
        }
        wasGrounded = entity.IsOnGround;

        if (!wantsToMove || pathCompleted)
        {
            ApplyFriction();
            steerVotes.Clear();

            movePathLengthMultiplier = 0;
            lastCommittedWishDir = Vector3.Zero;

            return;
        }

        Vector3 immediateTarget = GetMoveTarget();
        Vector3 rawDir = immediateTarget - entity.Position;
        rawDir.Y = 0;

        float lookAheadDist = Definition.RunSpeed * moveSpeedMultiplier * 0.55f;
        Vector3 lookAheadPt = GetLookAheadTarget(lookAheadDist);
        Vector3 steerDir = lookAheadPt - entity.Position;
        steerDir.Y = 0;

        Vector3 newWishDir = ResolveSteerVotes(
            !lastMoveAvoided && steerDir.Length() > 0.3f ? steerDir : rawDir);

        turnTargetMultiplier = ComputeTurnTargetMultiplier(lastCommittedWishDir, newWishDir);
        if (newWishDir.LengthSquared() > 0.01f)
        {
            lastCommittedWishDir = newWishDir;
        }
        wishDir = newWishDir;

        steerVotes.Clear();

    moveBody:

        bool hasDir = wishDir.Length() > 0.5f;

        float targetMult = hasDir ? turnTargetMultiplier : 0f;
        float rampRate = targetMult > movePathLengthMultiplier ? 3f : 4f;
        movePathLengthMultiplier = CMath.MoveTowards(
            movePathLengthMultiplier, targetMult,
            MainEngine.PreviousFrameDelta * rampRate);

        UpdateLookAngle(wishDir, hasDir);
        ApplyVelocity(hasDir ? wishDir : Vector3.Zero);

        float step = entity.TryStepUp(wishDir);
        if (step <= 0f) entity.TryStepDown();
    }
    private static float ComputeTurnTargetMultiplier(Vector3 previousDir, Vector3 newDir)
    {
        float newLenSq = newDir.LengthSquared();
        if (newLenSq < 0.01f)
        {
            return 1f;
        }

        float prevLenSq = previousDir.LengthSquared();
        if (prevLenSq < 0.01f)
        {
            return TurnSlowdownMinMultiplier;
        }

        Vector3 prevNorm = previousDir / MathF.Sqrt(prevLenSq);
        Vector3 newNorm = newDir / MathF.Sqrt(newLenSq);

        float alignment = (Vector3.Dot(prevNorm, newNorm) + 1f) * 0.5f;
        return MathHelper.Lerp(TurnSlowdownMinMultiplier, 1f, alignment);
    }
    private Vector3 ResolveSteerVotes(Vector3 pathDir)
    {
        if (steerVotes.Count == 0) return pathDir;

        Vector3 result = pathDir;
        foreach (var vote in steerVotes)
        {
            if (vote.IsDanger)
                result -= vote.Direction * vote.Weight;
            else
                result += vote.Direction * vote.Weight;
        }

        float len = result.Length();
        return len > 0.01f ? result / len : pathDir;
    }

    private void UpdateLookAngle(Vector3 moveDir, bool hasDir)
    {
        if (lookOverride.HasValue)
        {
            Vector3 d = lookOverride.Value - entity.Position;
            d.Y = 0f;
            if (d.LengthSquared() > 0.01f)
            {
                d.Normalize();
                float ta = MathHelper.ToDegrees(MathF.Atan2(d.X, d.Z));
                aiLookAngle = CMath.MoveTowardsAngle(aiLookAngle, ta,
                                  MainEngine.PreviousFrameDelta * aiSteerSpeed * 2f);
            }
        }
        else if (hasDir)
        {
            float targetAngle = MathHelper.ToDegrees(MathF.Atan2(moveDir.X, moveDir.Z));
            aiLookAngle = CMath.MoveTowardsAngle(aiLookAngle, targetAngle,
                              MainEngine.PreviousFrameDelta * aiSteerSpeed);
        }
    }

    private void ApplyFriction()
    {
        float speed = new Vector2(entity.Velocity.X, entity.Velocity.Z).Length();
        if (speed > 0f && entity.IsOnGround)
        {
            float drop = speed * Definition.GroundFriction * MainEngine.PreviousFrameDelta;
            entity.Velocity *= MathF.Max(speed - drop, 0f) / speed;
        }
    }

    private void ApplyVelocity(Vector3 wishDir)
    {
        ApplyFriction();

        float len = new Vector2(wishDir.X, wishDir.Z).Length();
        if (len > 1f) wishDir *= 1f / len;

        float topSpeed = Definition.RunSpeed * moveSpeedMultiplier * movePathLengthMultiplier;
        float curSpeed = Vector3.Dot(wishDir, entity.Velocity);
        float addSpeed = CMath.Clamp(
            (entity.IsOnGround ? topSpeed : 1f) - curSpeed,
            0f, Definition.MoveAccel * MainEngine.PreviousFrameDelta);

        entity.Velocity += addSpeed * wishDir;
    }
}