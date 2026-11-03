using Engine;
using Engine.Console;
using Engine.Physics;
using Engine.Utils;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Projectiles;
public abstract class BaseProjectileController : EntityController
{
    WorldEntity owner;
    public abstract float GravityMultiplier { get; }
    public abstract float ConstantSpeed { get; }
    public abstract float InitialSpeed { get; }
    public override void OnDespawn()
    {
    }

    public override void OnSpawn()
    {
        if (entity is not BaseProjectileEntity b) 
        { 
            EntityManager.DespawnEntity(entity);
            Logger.AppendError("Projectile is WorldEntity instead of BaseProjectileEntity"); 
            return; 
        }

        PhysicsEngine.BodyInterface.SetGravityFactor(entity.PhysicsBodyID, GravityMultiplier);
        entity.Velocity = Matrix.CreateFromQuaternion(entity.Rotation).Forward * InitialSpeed;
        entity.IgnoredEntities.Add(b.Owner);
        owner = b.Owner;
    }

    public override void OnUpdate(GameTime gameTime)
    {
        if(ConstantSpeed > 0)
        {
            entity.Velocity = Matrix.CreateFromQuaternion(entity.Rotation).Forward * ConstantSpeed;
        }

        if(Collision.ShapeCastPath(entity.PhysicsBody,entity.Position+entity.Velocity*MainEngine.PreviousFrameDelta,out _, out var pos, out var norm, false, owner.PhysicsBodyID, entity.PhysicsBodyID))
        {
            OnHitCast(pos, norm);
        }
    }

    public abstract void OnHitCast(Vector3 pos, Vector3 normal);
}
public abstract class BaseProjectileEntity : WorldEntity
{
    public WorldEntity Owner;
    public BaseProjectileEntity()
    {
        AxisAlignedBox = true;
        IsSimulated = true;
        Scale = Vector3.One;
    }
}