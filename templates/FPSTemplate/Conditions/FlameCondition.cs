using Engine;
using Engine.Conditions;
using Engine.Scripting.Sound;
using Engine.Sound;
using Engine.Utils;
using FPSTemplate.Entities.Brush;
using Microsoft.Xna.Framework;
using System;

namespace FPSTemplate.Conditions;
public class FlameCondition : Condition<FlameCondition>, ICondition
{
    // This is the index of the condition, which needs to be unique.
    public static int ID => (int)GameConditions.Flame;
    public static string Name => "flame";

    static CVarFloat MaxFireTime = new CVarFloat("c_firetime", 3f);

    const float ParticleSpeed = 0.09f;

    float timeOnFire;
    float particleTime;
    float hurtTime;
    int fireLightID;

    SoundInstance flameLoopInstance;

    public override void OnAdded(WorldEntity parent)
    {
        timeOnFire = MaxFireTime;
        fireLightID = MainEngine.CurrentRealtimeLights.Add(new Rockwall.Light());

        SoundScriptManager.PlaySound("Flame.Ignite",parent.Position);
        flameLoopInstance = SoundScriptManager.PlaySound("Flame.Loop", parent.Position);
    }

    public override void OnRemoved(WorldEntity parent)
    {
        MainEngine.CurrentRealtimeLights.Remove(fireLightID);

        flameLoopInstance.Stop();
    }

    public override void Update(WorldEntity parent)
    {
        if (parent.IsSubmerged || parent is FuncFluid) { parent.RemoveCondition(this); return; }

        flameLoopInstance.SourcePosition = parent.Position;

        particleTime -= MainEngine.PreviousFrameDelta;
        timeOnFire -= MainEngine.PreviousFrameDelta;
        hurtTime -= MainEngine.PreviousFrameDelta;

        if (timeOnFire < 0) { parent.RemoveCondition(this); return; }

        if (particleTime <= 0)
        {
            MainEngine.CurrentRealtimeLights[fireLightID] = new Rockwall.Light
            {
                Position = parent.Position,
                Intensity = Random.Shared.NextSingle() * 0.2f + 0.5f,
                Color = Color.OrangeRed,
                Range = 7f,
            };

            Vector3 particlePoint = parent.GetRandomPointOnEntityBounds();
            ParticleManager.SpawnParticleSystem(particlePoint, $"{GameEngine.FullPath}/Particles/flame.crp");

            particleTime = ParticleSpeed;
        }

        if(hurtTime <= 0)
        {
            parent.TakeDamage(new DamageInfo { damage = 3 });
            hurtTime = 0.5f;
        }
    }
}
