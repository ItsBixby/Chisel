using Chisel.Utils;
using Chisel.Utils.Animation;
using Engine;
using Engine.Rendering;
using Engine.Utils;
using Microsoft.Xna.Framework;
using Rockwall;
using System;

namespace FPSTemplate.Entities;
public partial class BasePlayerController
{
    protected CModelDisplay WorldModel;
    protected BipedAnimator WorldBiped;

    private float worldModelFacingYaw;
    private Vector3 thirdpersonCameraPosition;
    private float thirdpersonCamDist;

    private void SpawnWorldModel()
    {
        WorldModel = new CModelDisplay("Models/Player/npc_base.ccmdl");
        WorldBiped = new BipedAnimator(WorldModel);

        WorldBiped.MaxAimYaw = 45f;
        WorldBiped.MaxAimPitch = 30f;

        WorldBiped.Mode = RootMotionMode.Receive;
        WorldModel.DrawShadow = true;

        worldModelFacingYaw = yaw;
        thirdpersonCameraPosition = entity.Position;
    }

    private static float NormalizeAngle(float angle)
    {
        angle %= 360f;
        if (angle > 180f) angle -= 360f;
        if (angle < -180f) angle += 360f;
        return angle;
    }

    private void UpdateWorldModelAnimation(float dt)
    {
        var vel = new Vector3(entity.Velocity.X, 0f, entity.Velocity.Z);
        float spd = vel.Length();

        float targYaw = yaw;

        float headDiff = NormalizeAngle(targYaw - worldModelFacingYaw);

        if (spd > 0.1f || MathF.Abs(headDiff) > 30f)
        {
            worldModelFacingYaw += NormalizeAngle(targYaw - worldModelFacingYaw) * MathF.Min(dt * 8f, 1f);
            worldModelFacingYaw = NormalizeAngle(worldModelFacingYaw);
        }

        float moveYawDeg = 0f;
        if (spd > 0.1f && entity.IsOnGround)
        {
            var forward = Matrix.CreateRotationY(MathHelper.ToRadians(worldModelFacingYaw)).Forward;
            var right = Matrix.CreateRotationY(MathHelper.ToRadians(worldModelFacingYaw)).Right;
            var dir = vel / spd;

            moveYawDeg = MathF.Atan2(Vector3.Dot(dir, right), Vector3.Dot(dir, forward)) * (180f / MathF.PI);
        }

        WorldBiped.RunSpeed = GetSpeed();
        WorldBiped.UpdateLocomotion(entity.IsOnGround ? spd : 0f, moveYawDeg, dt);

        float aimYawDeg = NormalizeAngle(targYaw - worldModelFacingYaw);
        WorldBiped.UpdateAim(aimYawDeg, pitch, dt);

        WorldModel.Update();
    }

    private void UpdateThirdpersonCamera(GameTime gameTime, float speed)
    {
        Vector3 rayForward = -Matrix.CreateFromQuaternion(entity.Rotation).Forward;

        Vector3 targetCameraPos = entity.Position + Vector3.Up * 0.3f;
        thirdpersonCameraPosition = CMath.MoveTowards(thirdpersonCameraPosition, targetCameraPos,
            MainEngine.PreviousFrameDelta * 30 * (Vector3.Distance(thirdpersonCameraPosition, targetCameraPos) + 0.01f));

        float maxDist = 3.5f;
        Ray ray = new Ray(thirdpersonCameraPosition, rayForward);
        var result = BSPRoot.TraceRay(ray, maxDist);

        thirdpersonCamDist = Vector3.Distance(result.Point, thirdpersonCameraPosition) - 0.1f;

        ViewMatrix = Matrix.CreateTranslation(-thirdpersonCameraPosition - rayForward * thirdpersonCamDist)
                                 * Matrix.CreateFromQuaternion(Quaternion.Inverse(entity.Rotation))
                                 * Matrix.CreateRotationZ(Vector3.Dot(Vector3.Normalize(new Vector3(-rayForward.Z, 0, rayForward.X)), new Vector3(entity.Velocity.X, 0, entity.Velocity.Z)) * speed * -0.001f)
                                 * Matrix.CreateRotationZ(MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 6) * speed * 0.003f)
                                 * Matrix.CreateTranslation(MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 6) * speed * 0.03f, MathF.Sin((float)gameTime.TotalGameTime.TotalSeconds * 12) * speed * 0.03f, 0f);
        WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);
    }

    public void RenderWorldModel()
    {
        WorldModel.Transform = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(worldModelFacingYaw + 180), 0, 0)
                              * Matrix.CreateTranslation(entity.Position + entity.GetRealBounds().Min.Y * Vector3.Up);

        WorldModel.CheckForLights(entity.Position);
        WorldModel.Draw(ignoreFrustum: true);
    }
}