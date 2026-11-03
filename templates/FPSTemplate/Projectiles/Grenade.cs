using Engine;
using Engine.Rendering;
using FPSTemplate.Utilities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPSTemplate.Projectiles;
[EntityDescriptor()]
public class GrenadeEntity : WorldEntity
{
    public GrenadeEntity() : base()
    {
        Bounds = new BoundingBox(-Vector3.One * 0.06f, Vector3.One * 0.06f);
        Controller = new GrenadeController();
        PhysicsRestitution = 0.02f;
        AxisAlignedBox = false;
        IsSimulated = true;

        PhysicsDensityMultiplier = 10;
    }
}
public class GrenadeController : EntityController
{
    SphericalHarmonicsVisualizer model; // lol this is placeholder obv

    private bool exploded; // just in case
    private float timer = 2.5f;

    public override void OnSpawn()
    {
        model = new SphericalHarmonicsVisualizer();
    }
    public override void OnRender(GameTime gameTime)
    {
        model.DrawSphere(entity.Position);
    }
    public override void OnUpdate(GameTime gameTime)
    {
        timer -= MainEngine.PreviousFrameDelta;
        if (timer > 0f) return;

        if (exploded) return;

        ExplosionManager.CreateExplosion(entity.Position+Vector3.Up * 0.1f, 2f, 16f, 2500f);
        EntityManager.DespawnEntity(entity);

        exploded = true;
    }

    public override void OnDespawn()
    {
        model.Dispose();
    }
}