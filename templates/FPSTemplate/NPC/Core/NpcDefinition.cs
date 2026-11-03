using Microsoft.Xna.Framework;

namespace FPSTemplate.NPC.Core;

[System.Serializable]
public class NpcAnimationConfig
{
    /// <summary>Clip sequence, played when stationary.</summary>
    public string IdleSequence = "idle";

    /// <summary>Blend1D sequence, param "move_yaw" (degrees, 0=forward/±180=back/±90=strafe).</summary>
    public string WalkSequence = "walk";

    /// <summary>Blend1D sequence, same "move_yaw" param as Walk.</summary>
    public string RunSequence = "run";

    /// <summary>Blend2D sequence for aim; params "aim_yaw"/"aim_pitch" in degrees.</summary>
    public string AimSequence = "aim";

    public string FlinchSequence = "flinch_1";
}

[System.Serializable]
public class NpcDefinition
{
    public string ModelPath = "Models/Player/npc_base.ccmdl";
    public float Scale = 1f;
    public BoundingBox Bounds = new BoundingBox(
        -new Vector3(0.4f, 2.2f, 0.4f),
         new Vector3(0.4f, 0.15f, 0.4f));

    public int MaxHP = 40;

    public float WalkSpeed = 1.75f;
    public float RunSpeed = 3.5f;
    public float CreepSpeed = 1.2f;
    public float MoveAccel = 100f;
    public float SteerSpeed = 300f;
    public float GroundFriction = 8f;

    public float SightRange = 32f;
    public float SightFOVDegrees = 150f;
    public float HearRange = 8f;
    public float PerceptionTickRate = 0.2f;

    public float AttackRange = 25f;
    public float OptimalCombatRange = 8f;
    public float ReactionTime = 0.35f;
    public float LostTargetTimeout = 8f;

    public float SearchDuration = 20f;
    public float InvestigateDuration = 5f;

    public NpcAnimationConfig Animations = new();
}