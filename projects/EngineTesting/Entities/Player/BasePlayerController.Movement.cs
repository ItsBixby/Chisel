using Chisel.Utils;
using Engine;
using Engine.Physics;
using Engine.Rendering;
using Engine.Scripting.Sound;
using Engine.Sound;
using EngineTesting.Utilities.Player;
using Microsoft.Xna.Framework;
using Rockwall;
using System;

namespace EngineTesting.Entities;

public partial class BasePlayerController
{
    void Move(GameTime gameTime, float vlen)
    {
        Matrix rotMat = Noclip
            ? Matrix.Invert(RenderEngine.ViewMatrix)
            : Matrix.CreateFromAxisAngle(Vector3.Up, yaw * CMath.Deg2Rad);

        Vector3 wishDir = Vector3.Zero;
        float speedMultiplier = 1f;

        if (!IsDead)
        {
            wishDir += rotMat.Forward * PlayerInput.Forward.GetValue();
            wishDir -= rotMat.Forward * PlayerInput.Backward.GetValue();
            wishDir += rotMat.Right * PlayerInput.Right.GetValue();
            wishDir -= rotMat.Right * PlayerInput.Left.GetValue();

            speedMultiplier = MathF.Min(wishDir.Length(), 1f);
            if (wishDir.Length() > 0.01f) wishDir.Normalize();
        }

        if (!Noclip)
        {
            ApplyVelocity(wishDir, speedMultiplier, gameTime);

            if (!IsDead)
            {
                if (PlayerInput.Jump.HasBeenPressed()) jumpQueued = true;
                if (PlayerInput.Jump.HasBeenReleased()) jumpQueued = false;

                if (jumpQueued && ((wasOnGround && entity.IsOnGround) || entity.IsSubmerged))
                {
                    if(!entity.IsSubmerged)
                    {
                        entity.Velocity.Y = 6.5f;
                        entity.IsOnGround = false;
                        entity.AllowGroundSeparation = true;

                        jumpQueued = false;

                        PlayStepSound();
                    }
                    else
                    {
                        entity.Velocity.Y += 24f * MainEngine.PreviousFrameDelta;
                        if (entity.Velocity.Y > 2) entity.Velocity.Y = 2;
                    }
                }
            }

            TryStepLogic();
            entity.IgnoreCollision = false;
            entity.IgnoreGravity = false;
        }
        else
        {
            wishDir += Vector3.Up * PlayerInput.Jump.GetValue() * 0.5f;
            wishDir += Vector3.Down * PlayerInput.Crouch.GetValue() * 0.5f;

            var targvel = wishDir * 24;
            entity.Velocity = CMath.MoveTowards(entity.Velocity, targvel, MainEngine.PreviousFrameDelta * Vector3.Distance(targvel, entity.Velocity) * 8);
            entity.IgnoreGravity = true;
            entity.IgnoreCollision = true;
        }

        // Step and previous velocity
        var moveDir = wishDir * GetSpeed() * speedMultiplier;
        var stepsize = entity.TryStepUp(moveDir);
        if (stepsize <= 0) stepsize = entity.TryStepDown();
        if (stepsize != 0) cameraHeightBlend += stepsize;
    }

    void ApplyVelocity(Vector3 wishDir, float speedMultiplier, GameTime gameTime)
    {
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        float speed = new Vector2(entity.Velocity.X, entity.Velocity.Z).Length();

        if (speed != 0 && entity.IsOnGround)
        {
            float drop = speed * GroundFriction * dt;
            entity.Velocity *= MathF.Max(speed - drop, 0) / speed;
        }

        float len = new Vector2(wishDir.X, wishDir.Z).Length();
        if (len > 1) wishDir *= 1f / len;

        float curSpeed = Vector3.Dot(wishDir, entity.Velocity);
        float addSpeed = CMath.Clamp((entity.IsOnGround || entity.IsSubmerged ? GetSpeed() : MaxAirSpeed) * speedMultiplier - curSpeed, 0, MaxAccel * dt);

        entity.Velocity += addSpeed * wishDir;
    }

    void TryStepLogic()
    {
        if (!string.IsNullOrEmpty(entity.StandingOnSurface.SurfaceType) && entity.IsOnGround)
        {
            stepTimer -= MainEngine.PreviousFrameDelta * (new Vector2(entity.Velocity.X, entity.Velocity.Z).Length() / MaxRunSpeed);
            if (stepTimer <= 0)
            {
                stepTimer = StepTimerWait;
                PlayStepSound();
            }
        }
    }

    void PlayStepSound(float overrideVolume = 0f)
    {
        string bank = entity.StandingOnSurface.SurfaceType;
        if (string.IsNullOrWhiteSpace(bank)) return;

        float gain = overrideVolume > 0f ? overrideVolume : isCrouched ? 0.12f : 0.24f;

        // Bug fix -- apparently footsteps were being played out of hearing range lol
        SoundScriptManager.PlaySound($"Footstep.{bank}", entity.Position, is3DOverride: false, overrideVolume: gain);
    }
}