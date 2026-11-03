using Engine;
using Engine.Rendering;
using EngineTesting.Utilities;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Projectiles;
public class RocketEntity : BaseProjectileEntity
{
    public RocketEntity() : base()
    {
        // Base projectile already sets it to be an AABB and to be simulated.
        Bounds = new BoundingBox(-Vector3.One * 0.01f,Vector3.One * 0.01f);
        Controller = new RocketController();
        IgnoreCollision = true;
    }
}
public class RocketController : BaseProjectileController
{
    public override float GravityMultiplier => 0f;
    public override float ConstantSpeed => 90f;
    public override float InitialSpeed => 0f;

    SphericalHarmonicsVisualizer model; // lol this is placeholder obv

    private bool exploded; // just in case

    public override void OnSpawn()
    {
        model = new SphericalHarmonicsVisualizer();
        base.OnSpawn();
    }
    public override void OnRender(GameTime gameTime)
    {
        model.DrawSphere(entity.Position);
    }
    public override void OnHitCast(Vector3 pos, Vector3 normal)
    {
        // This gets called when it hits the world or an entity
        if (exploded) return;
        ExplosionManager.CreateExplosion(pos + normal * 0.1f, 2f, 16f, 2500f);
        EntityManager.DespawnEntity(entity);
        exploded = true;
    }
}