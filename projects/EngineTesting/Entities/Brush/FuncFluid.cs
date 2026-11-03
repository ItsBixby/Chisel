using Chisel.Particles;
using Chisel.Utils;
using Engine;
using Engine.Entities.LogicEntities;
using Engine.Physics;
using Engine.Rendering;
using Engine.Utils;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Entities.Brush;
[EntityDescriptor()]
//[ExposeEntityProperty("Density",EntityPropertyType.Float,"How dense the fluid is, water is about 1000.")]
public class FuncFluid : BrushEntity
{
    public FuncFluid()
    {
        Controller = new FuncFluidController();
        IgnoreCollision = true;
    }
    public override void OnEntityExit(WorldEntity entity)
    {
        entity.IsSubmerged = false;
    }
    public override void OnEntityEnter(WorldEntity entity)
    {
        entity.IsSubmerged = true;

        var point = CMath.ClampToBoundingBox(entity.OrientedBounds.Center, Bounds);

        ParticleManager.SpawnParticleSystem(point, $"{GameEngine.FullPath}/Particles/waterSplash.crp");
    }
    private class FuncFluidController : EntityController
    {
        public override void OnDespawn()
        {
        }

        public override void OnRender(GameTime gameTime)
        {
        }

        public override void OnSpawn()
        {
        }

        public override void OnUpdate(GameTime gameTime)
        {
            CheckEntitiesInTrigger();

            if(entity.OrientedBounds.Contains(RenderEngine.CameraPosition) == ContainmentType.Contains)
            {
                GameEngine.CameraIsUnderwater = true;
            }
        }
        public void CheckEntitiesInTrigger()
        {
            var bEnt = (BrushEntity)entity;

            if (MainEngine.IsPaused) return;

            if (bEnt.insideBrush == null) { return; }
            if (bEnt.insideBrush.Count == 0) { return; }

            var fluidBounds = GlobalMapData.ActiveMap.BrushBounds[bEnt.PrimaryBrush]; // fix later
            float surfaceY = fluidBounds.Max.Y;
            float fluidDensity = 4000f; 

            for (int i = bEnt.insideBrush.Count - 1; i >= 0; i--)
            {
                var entityInside = bEnt.insideBrush[i];
                if (!entityInside.IsSimulated) continue;

                Vector3[] corners = entityInside.OrientedBounds.GetCorners();

                Vector3 weightedSum = Vector3.Zero;
                float totalDepth = 0f;

                foreach (var c in corners)
                {
                    float depth = surfaceY - c.Y;
                    if (depth <= 0f) continue;

                    weightedSum += c * depth;
                    totalDepth += depth;
                }

                if (totalDepth <= 0f) continue;

                Vector3 centerOfBuoyancy = weightedSum / totalDepth;

                float avgDepthAcrossAllCorners = totalDepth / corners.Length;
                float objectHeight = entityInside.OrientedBounds.GetSize().Y;
                float submergedFraction = Math.Clamp(avgDepthAcrossAllCorners / objectHeight, 0f, 1f);

                float mass = entityInside.PhysicsBody.Shape.MassProperties.Mass * entityInside.PhysicsDensityMultiplier;
                float objectVolume = EstimateVolume(entityInside);
                float buoyancyForce = fluidDensity * -PhysicsEngine.GravityPerSecond * objectVolume * submergedFraction;

                entityInside.AddForceAtPosition(Vector3.Up * buoyancyForce, centerOfBuoyancy);

                var angvel = PhysicsEngine.BodyInterface.GetAngularVelocity(entityInside.PhysicsBodyID).ToXNA();

                Vector3 dragForce = -entityInside.Velocity * 2f * mass * submergedFraction;
                Vector3 angDragTorq = -angvel * 2f * mass * submergedFraction;

                entityInside.AddForce(dragForce);
                entityInside.PhysicsBody.AddTorque(angDragTorq.ToNumerics());
            }
        }
        private BoundingBox IntersectBounds(BoundingBox a, BoundingBox b)
        {
            Vector3 min = Vector3.Max(a.Min, b.Min);
            Vector3 max = Vector3.Min(a.Max, b.Max);
            max = Vector3.Max(min, max);
            return new BoundingBox(min, max);
        }
        private float GetVolume(BoundingBox box)
        {
            Vector3 size = box.Max - box.Min;
            return size.X * size.Y * size.Z;
        }
        float EstimateVolume(WorldEntity entity)
        {
            var size = entity.OrientedBounds.GetSize();
            return size.X * size.Y * size.Z * 0.5f;
        }
    }
}
