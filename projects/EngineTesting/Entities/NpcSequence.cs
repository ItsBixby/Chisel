using Engine;
using Engine.Compilation;
using EngineTesting.NPC.Core;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Entities;
[EntityDescriptor]
[ExposeEntityProperty("NPC Target", Rockwall.EntityPropertyType.String, "The targetname of the NPC entity.")]
[ExposeEntityPropertyTarget("Next Waypoint", "The targetname of the next NpcSequence in this specific sequence.")]
[ExposeEntityProperty("Walk Through", Rockwall.EntityPropertyType.Bool, "When true, the NPC will automatically begin walking toward the next waypoint after reaching this one.")]
[ExposeEntityPropertyEnum("Speed", "Which speed preset to use. 0 => Creep, 1 => Walk, 2 => Run.", "General", "Crawl", "Walk", "Run")]
[AutoConnectOnDuplicate("Next Waypoint")]
[RegisterEntityInputs("BeginSequence", "MoveTargetHere", "CancelSequence")]
[RegisterEntityOutputs("OnBeginSequence", "OnEndSequence", "OnThisPointReached")]
public class NpcSequence : WorldEntity
{
    public NpcSequence()
    {
        IsSimulated = false;
        AxisAlignedBox = true;
        IgnoreCollision = true;

        var control = new NpcSequenceController();
        Controller = control;

        RegisterInputLocally("BeginSequence", (args, from) =>
        {
            control.isRunning = true;

            SpeedPreset mult = ((string)ReadProperty("Speed", Rockwall.EntityPropertyType.Enum)) switch
            {
                "Crawl" => SpeedPreset.Creep,
                "Walk" => SpeedPreset.Walk,
                "Run" => SpeedPreset.Run,
                _ => SpeedPreset.Walk
            };

            foreach (var actor in control.allNpcs)
            {
                actor.IsActing = true;
                actor.SetSpeed(mult);
            }
            CallOutput("OnBeginSequence", this);
        });
        RegisterInputLocally("MoveTargetHere", (args, from) =>
        {
            foreach (var actor in control.allNpcs)
            {
                actor.Teleport(Position);
            }
        });
        RegisterInputLocally("CancelSequence", (args, from) =>
        {
            foreach (var actor in control.allNpcs)
            {
                actor.Follow(actor.entity);
                actor.IsActing = false;
            }
            control.isRunning = false;
            CallOutput("OnEndSequence", this);
        });
    }

    internal class NpcSequenceController : EntityController
    {
        internal NpcSequence next;
        internal List<NpcController> allNpcs = new List<NpcController>();
        internal bool isRunning;
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
            if (!isRunning) return;

            foreach (var actor in allNpcs)
            {
                actor.Follow(entity);
                if (!actor.IsPathCompleted()) return;
            }

            isRunning = false;
            entity.CallOutput("OnThisPointReached", entity);

            bool continuing = next != null && (bool)entity.ReadProperty("Walk Through", Rockwall.EntityPropertyType.Bool);
            if (continuing)
            {
                next.CallInput("BeginSequence", "", entity);
                return;
            }

            entity.CallOutput("OnEndSequence", entity);
            foreach (var actor in allNpcs) actor.IsActing = false;
        }
        public override void OnAllEntitiesSpawned()
        {
            var targets = EntityManager.FindEntityIndexByName((string)entity.ReadProperty("Next Waypoint", Rockwall.EntityPropertyType.String));
            if (targets != null && targets.Length > 0 && targets[0] != -1 && EntityManager.entities[targets[0]] is NpcSequence nextSeq)
                next = nextSeq;

            var npcTargets = EntityManager.FindEntityIndexByName((string)entity.ReadProperty("NPC Target", Rockwall.EntityPropertyType.String));
            if (npcTargets == null || npcTargets.Length == 0) return;

            foreach (var npcID in npcTargets)
            {
                if (npcID == -1) continue;
                if (EntityManager.entities[npcID] == null || EntityManager.entities[npcID].Controller is not NpcController npc) continue;
                allNpcs.Add(npc);
            }
        }
    }
}
