using Chisel.Collision;
using Engine;
using Engine.Scripting.Sound;
using Engine.Sound;
using Engine.Utils;
using JoltPhysicsSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace EngineTesting.Utilities.Bullet;
public static class BulletHitEffects
{
    public static Texture2D BulletHoleTex;
    public static Texture2D BloodSprayTex;

    public static void OnHitWorld(Vector3 point, Vector3 normal, Vector3 up, bool isSkybox)
    {
        if (isSkybox) return;

        SoundScriptManager.PlaySound("Bullet.Impact.concrete", point);

        SpawnDecal(BulletHoleTex, point, normal, up, sizeMin: 0.5f, sizeMax: 0.7f, DecalEffect.Multiply);

        ParticleManager.SpawnParticleSystem(point + normal * 0.1f, $"{GameEngine.FullPath}/Particles/concreteImpact.crp",
            Matrix.CreateWorld(Vector3.Zero, up, normal));
        ParticleManager.SpawnParticleSystem(point + normal * 0.1f, $"{GameEngine.FullPath}/Particles/smokeImpact.crp",
            Matrix.CreateWorld(Vector3.Zero, up, normal));
    }

    public static void OnHitFlesh(Vector3 hitPos, Vector3 direction, Vector3 up, bool sound = true)
    {
        if(sound)
        {
            SoundScriptManager.PlaySound("Bullet.Impact.flesh", hitPos);
        }

        SpawnBloodDecal(hitPos, direction, up);

        ParticleManager.SpawnParticleSystem(hitPos, $"{GameEngine.FullPath}/Particles/bloodSpray.crp",
            Matrix.CreateWorld(Vector3.Zero, Vector3.Up, -direction));
        ParticleManager.SpawnParticleSystem(hitPos, $"{GameEngine.FullPath}/Particles/bloodMist.crp",
            Matrix.CreateWorld(Vector3.Zero, Vector3.Up, -direction));
    }

    public static void OnHitProp(Vector3 hitPos, Vector3 direction, string physType, BodyID id)
    {
        PhysicsSounds.QueueImpactSound(physType, id, hitPos,direction*4);

        ParticleManager.SpawnParticleSystem(hitPos, $"{GameEngine.FullPath}/Particles/smokeImpact.crp",
            Matrix.CreateWorld(Vector3.Zero, Vector3.Up, -direction));
        ParticleManager.SpawnParticleSystem(hitPos, $"{GameEngine.FullPath}/Particles/bulletSpark.crp",
            Matrix.CreateWorld(Vector3.Zero, Vector3.Up, -direction));
    }

    // Helpers
    private static void SpawnBloodDecal(Vector3 hitPos, Vector3 direction, Vector3 up)
    {
        float size = Random.Shared.NextSingle() + 0.1f;
        float angle = Random.Shared.NextSingle() * MathF.PI * 0.3f;

        var box = new OrientedBoundingBox(
            new Vector3(-0.6f * size, -0.6f * size, -2f),
            new Vector3(0.6f * size, 0.6f * size, 2f));

        Vector3 offset = direction * 2f + Vector3.Up * (Random.Shared.NextSingle() * 2f - 1f);
        box.Transform(Matrix.CreateRotationZ(angle)
                    * Matrix.CreateWorld(hitPos + offset, -direction, up));

        DecalManager.RealtimeDecals[DecalGenerator.ProjectDecal(box, BloodSprayTex)].effect = DecalEffect.Overlay;
    }

    private static void SpawnDecal(Texture2D tex, Vector3 point, Vector3 normal, Vector3 up,
                                   float sizeMin, float sizeMax, DecalEffect decalEffect)
    {
        float size = Random.Shared.NextSingle() * (sizeMax - sizeMin) + sizeMin;
        float angle = Random.Shared.NextSingle() * MathF.PI * 0.3f;

        var box = new OrientedBoundingBox(
            new Vector3(-0.1f * size, -0.1f * size, -0.01f),
            new Vector3(0.1f * size, 0.1f * size, 0.01f));

        box.Transform(Matrix.CreateRotationZ(angle)
                    * Matrix.CreateWorld(point, normal, up));

        DecalManager.RealtimeDecals[DecalGenerator.ProjectDecal(box, tex)].effect = decalEffect;
    }

    public static Vector3 SafeUp(Vector3 normal)
    {
        Vector3 up = Vector3.Up;
        if (MathF.Abs(Vector3.Dot(up, normal)) > 0.9f) up = Vector3.UnitX;
        if (MathF.Abs(Vector3.Dot(up, normal)) > 0.9f) up = Vector3.UnitZ;
        return up;
    }
}