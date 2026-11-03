using Engine;
using Engine.Compilation;
using EngineTesting.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Entities;

[EntityDescriptor()]
[RegisterEntityInputs("Explode")]
public class EnvExplosion : WorldEntity
{
    public EnvExplosion()
    {
        IsSimulated = false;
        IgnoreCollision = true;

        RegisterInputLocally("Explode", (arg,from) => { ExplosionManager.CreateExplosion(Position, 2f, 16f, 2200f, 6f); });
    }
}
