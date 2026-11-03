using Engine;
using Engine.Entities;
using Engine.Physics;
using Engine.Utils;
using FPSTemplate.Entities;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using Rockwall;
using System.Collections.Generic;
using System.Linq;

namespace FPSTemplate.Utilities.Bullet;
public static class Ballistics
{
    public static bool ShootBullet(WorldEntity from, Vector3 pos, Vector3 tracerOrigin,
                               Vector3 direction, int damage,
                               float maxDistance = 512f, bool disableTracer = false)
    {
        var ray = new Ray(pos, direction);
        var worldHit = BSPRoot.TraceRay(ray, maxDistance);
        var hitPoint = worldHit.Point;
        float maxDist = Vector3.Distance(pos, hitPoint) - float.Epsilon;

        var ignored = new HashSet<JoltPhysicsSharp.BodyID> { from.PhysicsBodyID };
        WorldEntity entityHit = null;
        JoltPhysicsSharp.RayCastResult enthit = default;

        // if this goes over 10 ignorable things in a row then I think that's just a miracle 
        int iters = -1;
        while (iters < 10)
        {
            iters++;

            entityHit = Collision.CastEntity(ray, maxDist, out enthit, PhysicsFilters.DefaultBroadPhaseRayCastFilterIgnoreClip, ignored.ToArray());

            if (!enthit.BodyID.IsValid) break;

            ignored.Add(enthit.BodyID);

            if (entityHit != null) break;

            var body = PhysicsEngine.GetFromBodyID(enthit.BodyID);
            if (body == null) break;

            ulong flags = body.GetUserData();
            if (flags > 256) break;

            var hitPos = ray.Position + ray.Direction * enthit.Fraction * maxDist;
            Vector3 up = BulletHitEffects.SafeUp(worldHit.Normal);

            if ((flags & (ulong)PhysicsUserData.FLESH) != 0)
            {
                // Apparently we could shoot ragdolls through themselves without this...
                // Maybe this will fix it?
                if (iters > 0) continue;

                BulletHitEffects.OnHitFlesh(hitPos, direction, up);
            }

            if ((flags & (ulong)PhysicsUserData.INTERACTABLE) != 0)
            {
                PhysicsEngine.BodyInterface.AddImpulse(enthit.BodyID, (direction * damage * 800).ToNumerics(), hitPos.ToNumerics());
            }

            if ((flags & (ulong)PhysicsUserData.RAYPASS) == 0) break;
        }

        if (enthit.BodyID.IsValid && enthit.Fraction < maxDist && IsPhysHitSolidWorld(ref enthit))
        {
            var hitPos = ray.Position + ray.Direction * enthit.Fraction * maxDist;
            PhysicsEngine.GetFromBodyID(enthit.BodyID).GetWorldSpaceSurfaceNormal(enthit.subShapeID2, hitPos.ToNumerics(), out var normNumerics);
            var normal = normNumerics.ToXNA();

            Vector3 up = BulletHitEffects.SafeUp(normal);
            BulletHitEffects.OnHitWorld(hitPos, normal, up, false);

            if (!disableTracer) GameEngine.Tracers.Spawn(tracerOrigin, hitPos);
            return true;
        }

        if (entityHit == null && worldHit.Hit)
        {
            Vector3 up = BulletHitEffects.SafeUp(worldHit.Normal);
            BulletHitEffects.OnHitWorld(hitPoint, worldHit.Normal, up,
                BSPRoot.Nodes[worldHit.Node].nodeFlag == BSPNode.SkyboxNode);

            if (!disableTracer) GameEngine.Tracers.Spawn(tracerOrigin, hitPoint);
            return true;
        }

        if (entityHit != null && entityHit.Controller != null)
        {
            var hitPos = ray.Position + ray.Direction * enthit.Fraction * maxDist;
            Vector3 up = BulletHitEffects.SafeUp(worldHit.Normal);

            DamageBuffer.Queue(entityHit, new DamageInfo
            {
                damage = damage,
                damageType = (int)GameDamageTypes.Bullet,
                from = from,
                hitLocation = hitPos,
            },
            onFirstHit: () =>
            {
                if (entityHit.Controller is AIEntityTemplate || entityHit.Controller is LivingActor)
                    BulletHitEffects.OnHitFlesh(hitPos, direction, up, entityHit.Controller is not BasePlayerController);
                else
                    BulletHitEffects.OnHitProp(hitPos, direction, entityHit.PhysicsType, entityHit.PhysicsBodyID);
            });

            if (!disableTracer) GameEngine.Tracers.Spawn(tracerOrigin, hitPos);
            return true;
        }

        return false;
    }
    private static bool IsPhysHitSolidWorld(ref JoltPhysicsSharp.RayCastResult enthit)
    {
        if (!enthit.BodyID.IsValid) return false;

        var body = PhysicsEngine.GetFromBodyID(enthit.BodyID);
        if (body == null) return false;

        ulong flags = body.GetUserData();
        if (flags > 256) return false;

        return ((flags & (ulong)PhysicsUserData.WORLD) != 0);
    }
    private static WorldEntity ResolvePhysicsFlags(Ray ray, int damage, float maxDist, Vector3 direction,
                                                    BSPHit worldHit, WorldEntity from,
                                                    WorldEntity entityHit, ref JoltPhysicsSharp.RayCastResult enthit)
    {
        if (!enthit.BodyID.IsValid || entityHit != null) return entityHit;

        var body = PhysicsEngine.GetFromBodyID(enthit.BodyID);
        if (body == null) return entityHit;

        ulong flags = body.GetUserData();
        if (flags > 256) return entityHit;

        if ((flags & (ulong)PhysicsUserData.RAYPASS) != 0)
        {
            entityHit = Collision.CastEntity(ray, maxDist, out enthit, from.PhysicsBodyID, enthit.BodyID);
        }

        if ((flags & (ulong)PhysicsUserData.FLESH) != 0)
        {
            var hitPos = ray.Position + ray.Direction * enthit.Fraction * maxDist;
            Vector3 up = BulletHitEffects.SafeUp(worldHit.Normal);
            BulletHitEffects.OnHitFlesh(hitPos, direction, up);
        }

        if ((flags & (ulong)PhysicsUserData.INTERACTABLE) != 0)
        {
            var hitPos = ray.Position + ray.Direction * enthit.Fraction * maxDist;
            PhysicsEngine.BodyInterface.AddImpulse(enthit.BodyID,
                (direction * damage * 800).ToNumerics(), hitPos.ToNumerics());
        }

        return entityHit;
    }
}
