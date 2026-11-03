using Engine;
using Engine.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Entities;

// This is a good controller for basically any generic physics prop.
// You can inherit or extend this, or just use it as is :)
public class BasePhysicsProp : EntityController
{
    protected CModelDisplay model;
    protected ConcurrentStack<Vector3> dustSpawns = new ConcurrentStack<Vector3>();
    public BasePhysicsProp(string model)
    {
        this.model = new CModelDisplay(model);
    }

    public override void OnDespawn()
    {
        model.DeleteShadow();
        model.Dispose();
    }
    public override void OnBeforeRender(GameTime gameTime)
    {
        model.RenderShadowTexture(entity);
    }
    public override void OnRender(GameTime gameTime)
    {
        model.Transform = Matrix.CreateScale(entity.Scale) *
                          Matrix.CreateFromQuaternion(entity.Rotation) *
                          Matrix.CreateTranslation(entity.Position);
        model.CheckForLights(entity.OrientedBounds.Center);
        model.Draw();
    }

    public override void OnSpawn()
    {
    }

    public override void OnUpdate(GameTime gameTime)
    {
        if(entity.WillRenderThisFrame) model.Update();

        if(!dustSpawns.IsEmpty && dustSpawns.TryPop(out var point))
        {
            ParticleManager.SpawnParticleSystem(point, $"{GameEngine.FullPath}/Particles/dustScuff.crp");
        }
    }

    // This is how we'll make the body get knocked around with damage (like shots)
    public override void OnTakeDamage(DamageInfo info)
    {
        // info.hitLocation is optional, so let's check if the caller sent that info
        if (info.hitLocation == null) return;

        // or if we dont know who hit us
        if (info.from == null) return;

        // Lets make a hit vector
        var hitvec = Vector3.Normalize(entity.Position - info.from.Position);

        entity.AddImpulseAtPosition(hitvec * 8000, info.from.Position);
    }

    public override void OnContactCreated(Vector3 point)
    {
        if(entity.Velocity.LengthSquared() > 2 && dustSpawns.Count < 4)
            dustSpawns.Push(point);
    }
}
