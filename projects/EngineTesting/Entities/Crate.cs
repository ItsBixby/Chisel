using Engine;
using Engine.Compilation;
using Microsoft.Xna.Framework;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Entities;

[EntityDescriptor]
[EntityBounds(-0.5f, 0f, -0.5f, 0.5f, 1f, 0.5f)]
[EntityVisualize(typeof(ModelVisualizer))]
[VisualizerLiteral(nameof(ModelVisualizer.Model), "Models/Props/crate.ccmdl")]
public class Crate : WorldEntity
{
    public Crate()
    {
        IsSimulated = true;
        Bounds = new BoundingBox(new Vector3(-0.5f, -0, -0.5f),new Vector3(0.5f, 1f, 0.5f));
        Controller = new BasePhysicsProp("Models/Props/crate.ccmdl");

        PhysicsType = "crate";

        PhysicsRestitution = 0.2f;
        PhysicsDensityMultiplier = 1.25f;
    }
}