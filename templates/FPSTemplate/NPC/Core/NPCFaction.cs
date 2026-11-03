using Engine;
using FPSTemplate.Entities;
using System.Collections.Generic;

namespace FPSTemplate.NPC.Core;

public enum FactionRelationship { Hostile, Neutral, Friendly, Ally }

public class NpcFaction
{
    public readonly string Name;
    private readonly Dictionary<NpcFaction, FactionRelationship> relations = new();

    public NpcFaction(string name) => Name = name;

    public FactionRelationship GetRelationshipTo(NpcFaction other)
    {
        if (other == null) return FactionRelationship.Neutral;
        if (other == this) return FactionRelationship.Friendly;
        return relations.TryGetValue(other, out var r) ? r : FactionRelationship.Neutral;
    }

    public void SetRelationshipTo(NpcFaction other, FactionRelationship rel)
    {
        relations[other] = rel;
        other.relations[this] = rel;
    }

    public bool IsHostileTo(NpcFaction other) => GetRelationshipTo(other) == FactionRelationship.Hostile;
    public bool IsFriendlyTo(NpcFaction other) => GetRelationshipTo(other) is FactionRelationship.Friendly or FactionRelationship.Ally;
    public bool IsAlliedWith(NpcFaction other) => GetRelationshipTo(other) == FactionRelationship.Ally;

    public override string ToString() => Name;
}

public static class FactionManager
{
    private static readonly Dictionary<string, NpcFaction> factions = new();

    public static readonly NpcFaction Player = Register("player");
    public static readonly NpcFaction Grunts = Register("grunts");
    public static readonly NpcFaction Rebels = Register("rebels");

    static FactionManager()
    {
        Grunts.SetRelationshipTo(Player, FactionRelationship.Hostile);
        Grunts.SetRelationshipTo(Rebels, FactionRelationship.Hostile);
        Rebels.SetRelationshipTo(Player, FactionRelationship.Ally);
    }

    public static NpcFaction Register(string name)
    {
        var f = new NpcFaction(name);
        factions[name] = f;
        return f;
    }

    public static NpcFaction Get(string name) =>
        factions.TryGetValue(name, out var f) ? f : null;

    public static NpcFaction GetFaction(WorldEntity e) => e.Controller switch
    {
        NpcController npc => npc.Faction,
        BasePlayerController => Player,
        _ => null
    };
}