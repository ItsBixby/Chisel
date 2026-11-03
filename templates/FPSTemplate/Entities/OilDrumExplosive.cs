using Engine;
using Engine.Compilation;
using Engine.Utils;
using FPSTemplate.Conditions;
using FPSTemplate.Utilities;
using Microsoft.Xna.Framework;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPSTemplate.Entities;

[EntityDescriptor]
[EntityBounds(-0.6f, 0f, -0.6f, 0.6f, 1.6f, 0.6f)]
[EntityVisualize(typeof(ModelVisualizer))]
[VisualizerLiteral(nameof(ModelVisualizer.Model), "Models/Props/oildrum.ccmdl")]
public class OilDrumExplosive : WorldEntity
{
    public OilDrumExplosive()
    {
        IsSimulated = true;
        Bounds = new BoundingBox(new Vector3(-0.6f, 0f, -0.6f), new Vector3(0.6f, 1.6f, 0.6f));
        PhysicsShape = EntityPhysicsShapes.Cylinder;
        Controller = new OilDrumExplosiveController("Models/Props/oildrum.ccmdl");

        //This determines the impact sound
        PhysicsType = "oildrum";

        //This makes them more bouncy. Fun :D
        PhysicsRestitution = 0.35f;

        //This is kind of a "weight" in a way
        PhysicsDensityMultiplier = 1.5f;
    }

    protected class OilDrumExplosiveController : BasePhysicsProp
    {
        int health = 15;

        public OilDrumExplosiveController(string model) : base(model)
        {
        }
        public override void OnUpdate(GameTime gameTime)
        {
            base.OnUpdate(gameTime);
        }
        public override void OnTakeDamage(DamageInfo info)
        {
            base.OnTakeDamage(info);

            int oldHealth = health;
            health -= info.damage;

            if(health <= 0 && oldHealth > 0)
            {
                Explode();
            }

            entity.AddCondition(new FlameCondition());
        }
        void Explode()
        {
            ExplosionManager.CreateExplosion(entity.Position+Vector3.Up*0.01f, 2f, 16f, 2200f, 6f);

            EntityManager.DespawnEntity(entity);
        }
    }
}
