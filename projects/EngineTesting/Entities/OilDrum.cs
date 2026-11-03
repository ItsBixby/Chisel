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
[EntityBounds(-0.6f, 0f, -0.6f, 0.6f, 1.6f, 0.6f)]
[EntityVisualize(typeof(ModelVisualizer))]
[VisualizerLiteral(nameof(ModelVisualizer.Model), "Models/Props/oildrum.ccmdl")]
public class OilDrum : WorldEntity
{
    public OilDrum()
    {
        IsSimulated = true;
        Bounds = new BoundingBox(new Vector3(-0.6f,0f,-0.6f),new Vector3(0.6f,1.6f,0.6f));
        PhysicsShape = EntityPhysicsShapes.Cylinder;
        Controller = new BasePhysicsProp("Models/Props/oildrum.ccmdl");

        //This determines the impact sound
        PhysicsType = "oildrum";

        //This makes them more bouncy. Fun :D
        PhysicsRestitution = 0.35f;

        //This is kind of a "weight" in a way
        PhysicsDensityMultiplier = 1.5f;
    }
}
