using EngineTesting.NPC.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.NPC;
public static class NpcSquads
{
    // In an actual game, you might want to create dynamic squads created by the NPCs themselves.
    public static readonly NpcSquad GruntSquad = new();
    public static readonly NpcSquad PlayerSquad = new();
}
