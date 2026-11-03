using Chisel.Models;
using Chisel.Utils;
using Chisel.Utils.Animation;
using Engine;
using Engine.Rendering;
using Engine.Scripting.Sound;
using Engine.Sound;
using Engine.Utils;
using FPSTemplate.Entities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input.Touch;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FPSTemplate.NPC.Core;

public partial class NpcController
{
    private CModelDisplay model = null!;
    private BipedAnimator bipedAnimator = null!;
    private LayerConfig weaponLayer = null!;
    private LayerConfig flinchLayer = null!;
    private LayerConfig gestureLayer = null!;

    private float glanceTimer;
    private bool glancingAtSomeone;
    private WorldEntity glanceSubject;

    private const float GlanceIntervalMin = 5f;
    private const float GlanceIntervalMax = 10f;
    private const float GlanceDurationMin = 1f;
    private const float GlanceDurationMax = 2.5f;
    private const float GlanceDetectionRadius = 10f;
    private const float GlanceLookPriority = 0.5f;

    private Vector3 idleGazePoint;
    private float idleGazeTimer;
    private const float IdleGazeIntervalMin = 4f;
    private const float IdleGazeIntervalMax = 8f;
    private const float IdleGazeIntervalCombatMultiplier = 3f;
    private const float IdleGazeMaxYaw = 35f;
    private const float IdleGazeMaxPitch = 15f;
    private const float IdleGazeLookDistance = 5f;
    private const float IdleGazePriority = 0.1f; 

    private Quaternion eyeTwitchRotation = Quaternion.Identity;
    private float eyeTwitchTimer;
    private const float EyeTwitchIntervalMin = 0.5f;
    private const float EyeTwitchIntervalMax = 2.2f;
    private const float EyeTwitchMaxDegrees = 2f;

    private const float LookPitchClampDegrees = 60f;
    private const float AimPitchClampDegrees = 90f;

    private bool bodyTurnAssistActive;
    private const float BodyTurnAssistThresholdDegrees = 70f;      // engage once off by more than this
    private const float BodyTurnAssistDoneThresholdDegrees = 5f;   // release once back within this
    private const float BodyTurnAssistRate = 540f;

    private const float ChoreoBodyTurnThresholdDegrees = 45f;  // head gets craned past this, so turn the body
    private const float ChoreoBodyTurnRate = 180f;             // slower and more natural than combat's 540

    private List<(Vector3 target, float priority)> lookTargets = new List<(Vector3 target, float priority)>();

    public void AddLookTarget(Vector3 target, float priority)
    {
        lookTargets.Add((target, priority));
    }

    private void SpawnAnimation()
    {
        model = new CModelDisplay(Definition.ModelPath);
        var a = Definition.Animations;

        var animator = model.EnsureAnimator();

        bipedAnimator = new BipedAnimator(model,
            idleSequence: a.IdleSequence,
            walkSequence: a.WalkSequence,
            runSequence: a.RunSequence,
            aimSequence: a.AimSequence,
            runSpeed: Definition.RunSpeed);

        bipedAnimator.FootTraceIgnoredBodies = new JoltPhysicsSharp.BodyID[1] { entity.PhysicsBodyID };

        bipedAnimator.Mode = RootMotionMode.Receive;

        weaponLayer = animator.GetOrAddLayer("weapon", LayerBlendType.Replace, order: 15);
        flinchLayer = animator.GetOrAddLayer("flinch", LayerBlendType.Additive);
        gestureLayer = animator.GetOrAddLayer("gesture", LayerBlendType.Replace);

        weaponLayer.Influence = 1f;
        flinchLayer.Influence = 0f;
        gestureLayer.Influence = 0f;

        model.DrawShadow = true;
        model.OnCustomEvent += HandleCustomAnimEvent;

        idleGazePoint = PickIdleGazePoint();
    }

    private void UpdateAnimation(float dt)
    {
        var vel = new Vector3(entity.Velocity.X, 0f, entity.Velocity.Z);
        float spd = vel.Length();

        var facing = Matrix.CreateRotationY(aiLookAngle * CMath.Deg2Rad);
        var forward = -facing.Forward;
        var right = -facing.Right;

        float moveYawDeg = 0f;
        if (spd > 0.1f)
        {
            var dir = vel / spd;
            moveYawDeg = MathF.Atan2(Vector3.Dot(dir, right), Vector3.Dot(dir, forward)) * (180f / MathF.PI);
        }

        bipedAnimator.UpdateLocomotion(entity.IsOnGround ? spd : 0f, moveYawDeg, dt);

        float aimTargetYaw = 0f, aimTargetPitch = 0f;

        Vector3? facePoint = null;
        if (IsInCombat && BestThreat != null)
        {
            facePoint = BestThreat.Position;
        }
        else if (IsActing && currentChoreoLookTarget != null && !currentChoreoLookTarget.IsDespawned && spd < 0.1f)
        {
            facePoint = currentChoreoLookTarget.Position;
        }

        float turnThreshold = IsInCombat ? BodyTurnAssistThresholdDegrees : ChoreoBodyTurnThresholdDegrees;
        float turnRate = IsInCombat ? BodyTurnAssistRate : ChoreoBodyTurnRate;

        float faceYaw = 0f, facePitch = 0f;
        if (facePoint.HasValue)
        {
            (faceYaw, facePitch) = ComputeYawPitchToPoint(
                entity.Position, facePoint.Value, forward, AimPitchClampDegrees);

            if (IsInCombat)
            {
                aimTargetYaw = faceYaw;
                aimTargetPitch = facePitch;
            }

            if (!bodyTurnAssistActive && MathF.Abs(faceYaw) > turnThreshold)
            {
                bodyTurnAssistActive = true;
            }
        }
        else
        {
            bodyTurnAssistActive = false;
        }

        if (bodyTurnAssistActive)
        {
            aiLookAngle = CMath.MoveTowardsAngle(aiLookAngle, aiLookAngle - faceYaw, dt * turnRate);

            facing = Matrix.CreateRotationY(aiLookAngle * CMath.Deg2Rad);
            forward = -facing.Forward;
            right = -facing.Right;

            (faceYaw, facePitch) = ComputeYawPitchToPoint(
                entity.Position, facePoint.Value, forward, AimPitchClampDegrees);

            if (IsInCombat)
            {
                aimTargetYaw = faceYaw;
                aimTargetPitch = facePitch;
            }

            if (MathF.Abs(faceYaw) < BodyTurnAssistDoneThresholdDegrees)
            {
                bodyTurnAssistActive = false;
            }
        }

        bipedAnimator.EnableFootIK = entity.IsOnGround;

        bipedAnimator.UpdateAim(aimTargetYaw, aimTargetPitch, dt);

        TickIdleGaze(dt, forward, right);
        TickEyeTwitch(dt);

        AddLookTarget(idleGazePoint, IdleGazePriority);

        Vector3 eyeOrigin = entity.Position - Vector3.Up * 0.15f; // these are setup like the player, where "eyes" are the current pos.

        var best = lookTargets.OrderByDescending(t => t.priority).First();
        Vector3 lookAtPoint = best.target;

        (float targetYawDeg, float targetPitchDeg) = ComputeYawPitchToPoint(
            eyeOrigin, lookAtPoint, forward, LookPitchClampDegrees);

        bipedAnimator.UpdateLook(targetYawDeg, targetPitchDeg, dt);

        Matrix entityWorldTransform =
              Matrix.CreateScale(entity.Scale * Definition.Scale)
            * Matrix.CreateFromQuaternion(entity.Rotation)
            * Matrix.CreateTranslation(entity.Position
                + Vector3.UnitY * entity.GetRealBounds().Min.Y * entity.Scale * Definition.Scale);

        bipedAnimator.UpdateFootPlacement(MainEngine.PreviousFrameDelta, entityWorldTransform, entity.GetRealBounds());

        if (flinchLayer.IsFinished && flinchLayer.Influence > 0f)
            flinchLayer.Influence = CMath.MoveTowards(flinchLayer.Influence, 0f, dt * 5f);

        lookTargets.Clear();

        if (gestureTime > 0) gestureTime -= dt;

        gestureLayer.Influence = CMath.MoveTowards(gestureLayer.Influence, gestureTime > 0 ? 1 : 0, dt * 5);

        Vector3 eyeToTarget = lookAtPoint - eyeOrigin;
        float eyeDist = MathF.Max(eyeToTarget.Length(), 0.001f);
        Vector3 eyeDir = eyeToTarget / eyeDist;
        Vector3 twitchedDir = Vector3.Normalize(Vector3.Transform(eyeDir, eyeTwitchRotation));
        model.SetEyeLookTarget(eyeOrigin + twitchedDir * eyeDist);
    }

    private static (float yaw, float pitch) ComputeYawPitchToPoint(
        Vector3 origin, Vector3 point, Vector3 forward, float pitchClampDeg, float yawClampDeg = 90f)
    {
        Vector3 toTarget = point - origin;
        float horizontalDist = MathF.Sqrt(toTarget.X * toTarget.X + toTarget.Z * toTarget.Z);

        var flatTgt = Vector3.Normalize(new Vector3(toTarget.X, 0f, toTarget.Z));
        var flatFwd = Vector3.Normalize(new Vector3(forward.X, 0f, forward.Z));

        float cross = flatFwd.X * flatTgt.Z - flatFwd.Z * flatTgt.X;
        float angleRad = MathF.Atan2(cross, Vector3.Dot(flatFwd, flatTgt));
        float yaw = Math.Clamp(angleRad * (180f / MathF.PI), -yawClampDeg, yawClampDeg);

        float pitch = Math.Clamp(
            MathF.Atan2(toTarget.Y, MathF.Max(horizontalDist, 0.001f)) * (180f / MathF.PI),
            -pitchClampDeg, pitchClampDeg);

        return (yaw, pitch);
    }

    private void TickIdleGaze(float dt, Vector3 forward, Vector3 right)
    {
        idleGazeTimer -= dt;
        if (idleGazeTimer > 0f) return;

        idleGazePoint = PickIdleGazePoint(forward, right);

        float baseInterval = IdleGazeIntervalMin + Random.Shared.NextSingle() * (IdleGazeIntervalMax - IdleGazeIntervalMin);
        idleGazeTimer = IsInCombat ? baseInterval * IdleGazeIntervalCombatMultiplier : baseInterval;
    }

    private Vector3 PickIdleGazePoint() => PickIdleGazePoint(Vector3.Forward, Vector3.Right);

    private Vector3 PickIdleGazePoint(Vector3 forward, Vector3 right)
    {
        float yawDeg = (Random.Shared.NextSingle() * 2f - 1f) * IdleGazeMaxYaw;
        float pitchDeg = (Random.Shared.NextSingle() * 2f - 1f) * IdleGazeMaxPitch;

        Vector3 headPoint = entity.Position - Vector3.Up * 0.05f;
        return headPoint + YawPitchToWorldDir(yawDeg, pitchDeg, forward, right) * IdleGazeLookDistance;
    }

    private void TickEyeTwitch(float dt)
    {
        eyeTwitchTimer -= dt;
        if (eyeTwitchTimer > 0f) return;

        float twitchAngle = MathHelper.ToRadians(EyeTwitchMaxDegrees) * Random.Shared.NextSingle();
        Vector3 randomAxis = new Vector3(
            Random.Shared.NextSingle() * 2f - 1f,
            Random.Shared.NextSingle() * 2f - 1f,
            Random.Shared.NextSingle() * 2f - 1f);
        randomAxis = randomAxis.LengthSquared() < 0.0001f ? Vector3.Up : Vector3.Normalize(randomAxis);

        eyeTwitchRotation = Quaternion.CreateFromAxisAngle(randomAxis, twitchAngle);
        eyeTwitchTimer = EyeTwitchIntervalMin + Random.Shared.NextSingle() * (EyeTwitchIntervalMax - EyeTwitchIntervalMin);
    }

    private static Vector3 YawPitchToWorldDir(float yawDeg, float pitchDeg, Vector3 forward, Vector3 right)
    {
        Matrix yawRot = Matrix.CreateFromAxisAngle(Vector3.Up, yawDeg * CMath.Deg2Rad);
        Vector3 yawedForward = Vector3.TransformNormal(forward, yawRot);
        Vector3 yawedRight = Vector3.TransformNormal(right, yawRot);

        Matrix pitchRot = Matrix.CreateFromAxisAngle(yawedRight, -pitchDeg * CMath.Deg2Rad);
        return Vector3.Normalize(Vector3.TransformNormal(yawedForward, pitchRot));
    }

    public void TriggerFlinch()
    {
        if (Random.Shared.NextSingle() > 0.3f)
        {
            var flinchSeq = model.Model.FindSequence(Definition.Animations.FlinchSequence);
            if (flinchSeq == null) return;

            flinchLayer.Influence = 1f;
            flinchLayer.ForceReplay(flinchSeq, loop: false);
        }
    }

    private void HandleCustomAnimEvent(string slot, string options)
    {
        if (options == "footstep") PlayStepSound();
    }

    private void PlayStepSound(float overrideVolume = 0f)
    {
        string bank = entity.StandingOnSurface.SurfaceType;
        if (string.IsNullOrWhiteSpace(bank)) return;

        SoundScriptManager.PlaySound($"Footstep.{bank}", entity.Position, overrideVolume: overrideVolume > 0 ? overrideVolume : 0.24f);
    }

    private void TickSocialGlances(float dt)
    {
        glanceTimer -= dt;
        if (glanceTimer <= 0f)
        {
            glancingAtSomeone = !glancingAtSomeone;

            if (glancingAtSomeone)
            {
                glanceSubject = PickGlanceSubject();
                if (glanceSubject == null)
                {
                    glancingAtSomeone = false;
                }
            }

            glanceTimer = glancingAtSomeone
                ? GlanceDurationMin + Random.Shared.NextSingle() * (GlanceDurationMax - GlanceDurationMin)
                : GlanceIntervalMin + Random.Shared.NextSingle() * (GlanceIntervalMax - GlanceIntervalMin);
        }

        if (glancingAtSomeone && glanceSubject != null && !glanceSubject.IsDespawned)
        {
            AddLookTarget(glanceSubject.Position, GlanceLookPriority);
        }
    }

    private WorldEntity PickGlanceSubject()
    {
        var nearby = Collision.GetEntitiesInSphere(entity.Position, GlanceDetectionRadius);

        WorldEntity best = null;
        float bestScore = float.MinValue;

        foreach (var other in nearby)
        {
            if (other == entity)
            {
                continue;
            }

            if (other.Controller is not LivingActor actor || actor.IsDead)
            {
                continue;
            }

            bool isSquadmate = Squad != null && other.Controller is NpcController otherNpc && otherNpc.Squad == Squad;
            float score = (isSquadmate ? 2f : 1f) + Random.Shared.NextSingle();

            if (score > bestScore)
            {
                bestScore = score;
                best = other;
            }
        }

        return best;
    }
}