using Engine;
using Engine.Rendering;
using Engine.Scripting.Sound;
using Engine.Sound;
using Engine.Utils;
using EngineTesting.Conditions;
using EngineTesting.Entities;
using EngineTesting.Projectiles;
using Microsoft.Xna.Framework;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Utilities;
public static class ExplosionManager
{
    private struct Explosion
    {
        public Vector3 Origin;
        public float CurRadius, MaxRadius;
        public HashSet<WorldEntity> HitEntities;
        public float Power;
        public float BaseDamage;
        public int LightID;
    }
    private static List<Explosion> activeExplosions = new List<Explosion>();
    private static readonly CVarFloat explosionExpansionRate = new CVarFloat("p_explosionexpansionrate", 24);
    /// <summary>
    /// Spawns an explosion at a point with a certain power, which will grow from the start radius to the max radius over time.
    /// </summary>
    /// <param name="point">The origin of the explosion</param>
    /// <param name="startRadius">The starting radius of the explosion</param>
    /// <param name="maxRadius">The ending radius of the explosion</param>
    /// <param name="power">How strong this explosion is</param>
    public static void CreateExplosion(Vector3 point, float startRadius, float maxRadius, float power, float baseDamage = 2.5f, string? explosionParticleFile = null)
    {
        var light = MainEngine.CurrentRealtimeLights.Add(new Light
        {
            Position = point,
            Intensity = 0.5f,
            Color = Color.OrangeRed,
            Range = 8f,
            Type = Light.LightType.Point
        });
        activeExplosions.Add(new Explosion
        {
            Origin = point,
            CurRadius = startRadius,
            MaxRadius = maxRadius,
            HitEntities = new HashSet<WorldEntity>(),
            Power = power,
            BaseDamage = baseDamage,
            LightID = light,
        });
        ParticleManager.SpawnParticleSystem(point, explosionParticleFile??$"{GameEngine.FullPath}/Particles/explosion.crp");

        SoundScriptManager.PlaySound("Explosion.Large",point);
    }

    public static void UpdateExplosions()
    {
        for (int i = activeExplosions.Count - 1; i >= 0; i--)
        {
            var explosion = activeExplosions[i];
            if (explosion.CurRadius >= explosion.MaxRadius)
            {
                MainEngine.CurrentRealtimeLights.Remove(explosion.LightID);
                activeExplosions.RemoveAt(i);
                continue;
            }

            float expansionProgress = explosion.CurRadius / explosion.MaxRadius;

            float powerMultiplier = (float)Math.Pow(1f - expansionProgress, 2);

            explosion.CurRadius += MainEngine.PreviousFrameDelta * explosionExpansionRate * ((1 - expansionProgress) + 0.5f);

            MainEngine.CurrentRealtimeLights[explosion.LightID] = MainEngine.CurrentRealtimeLights[explosion.LightID] with
            {
                Intensity = (1 - expansionProgress) * 0.8f,
            };

            var captured = Collision.GetEntitiesInSphere(explosion.Origin, explosion.CurRadius).ToArray();
            foreach(var entity in captured)
            {
                // Shouldnt be possible but... who knows.
                if (entity is null) continue;

                if (explosion.HitEntities.Contains(entity))
                    continue;

                var dir = (entity.AxisAlignedBox ? entity.Position : entity.Position + entity.BodyOffset) - explosion.Origin;
                float dist = dir.Length();

                if (dist > explosion.CurRadius) continue;

                if (entity.Controller is BaseProjectileController) continue;

                if (dist < 2.5f) entity.AddCondition(new FlameCondition());

                dir.Normalize();

                //if (BSPRoot.TraceRay(new Ray(explosion.origin, dir), dist).hit) return;

                explosion.HitEntities.Add(entity);

                //if (entity.controller is BasePhysicsProp)
                //{
                //    dir.Y *= 2f;
                //}
                //else
                //{
                //    dir.Y *= 0.18f;
                //}

                entity.TakeDamage(new DamageInfo
                {
                    from = null,
                    damage = (int)((explosion.MaxRadius - dist) * powerMultiplier * explosion.BaseDamage),
                    damageType = (int)GameDamageTypes.Explosive,
                    hitLocation = explosion.Origin,
                });

                Vector3 impulse = dir * explosion.Power * (explosion.MaxRadius - dist) * powerMultiplier;

                entity.AddImpulseAtPosition(
                    impulse,
                    explosion.Origin
                );
            }
            activeExplosions[i] = explosion;
        }
    }
    public static void DebugRender()
    {
        for (int i = activeExplosions.Count - 1; i >= 0; i--)
        {
            var explosion = activeExplosions[i];

            RenderEngine.DrawBox(new BoundingBox(explosion.Origin-Vector3.One*explosion.CurRadius*0.5f, explosion.Origin + Vector3.One * explosion.CurRadius * 0.5f));
        }
    }
    public static void ClearExplosions()
    {
        activeExplosions.Clear();
    }
}