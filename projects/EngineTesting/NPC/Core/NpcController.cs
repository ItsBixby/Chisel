using Chisel.Utils;
using Engine;
using Engine.Entities;
using Engine.Rendering;
using Engine.Scripting.Sound;
using Engine.Sound;
using Engine.Utils;
using FontStashSharp;
using EngineTesting.Entities;
using EngineTesting.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;
using Chisel.Models;
using Chisel.Models.Morph;
using System.IO;

namespace EngineTesting.NPC.Core;

public enum SpeedPreset { Creep, Walk, Run }

public abstract partial class NpcController : AIAgent, LivingActor, ChoreoActor
{
    public int MaxHP => Definition.MaxHP;
    [SaveValue("hp")]
    public int HP { get; set; }
    public bool IsDead { get; set; }
    [SaveValue("combat_mode")]
    public bool IsInCombat { get; protected set; }

    public NpcDefinition Definition { get; private set; }
    public NpcFaction Faction { get; set; } = FactionManager.Grunts;
    public NpcSquad Squad { get; private set; }

    public string CurrentState => currentStateName;

    private sealed class StateDefinition
    {
        public Action Enter;
        public Func<float, string> Tick;
        public Action Exit;
        public bool IsCombatState;
    }

    private readonly Dictionary<string, StateDefinition> states = new();
    private string currentStateName;
    private static Dictionary<string, string> soundPathToMorph;

    // Register a named state. The tick callback returns the name of the next
    // state to transition to, or null to stay in the current one.
    protected void DefineState(string name, Func<float, string> tick,
        Action enter = null, Action exit = null, bool isCombatState = false)
    {
        states[name] = new StateDefinition
        {
            Enter = enter,
            Tick = tick,
            Exit = exit,
            IsCombatState = isCombatState
        };
    }

    protected void GoTo(string name) => TransitionTo(name);

    public void Signal(string signal, object data = null)
    {
        if (signal.StartsWith("bark_"))
            Bark(signal.Substring(5));
        OnSignal(signal, data);
    }

    protected virtual void OnSignal(string signal, object data) { }

    private void TransitionTo(string name)
    {
        if (name == currentStateName || !states.ContainsKey(name)) return;
        states[currentStateName]?.Exit?.Invoke();
        currentStateName = name;
        IsInCombat = states[name].IsCombatState;
        states[name].Enter?.Invoke();
    }

    private void TickStateMachine(float dt)
    {
        if (currentStateName == null || !states.TryGetValue(currentStateName, out var def)) return;
        var next = def.Tick?.Invoke(dt);
        if (next != null && next != currentStateName)
            TransitionTo(next);
    }

    public float BarkCaptionDuration = 2.5f;
    public float BarkGlobalCooldown = 4f;
    public float BarkCategoryCooldown = 8f;

    private sealed class BarkLine
    {
        public string Category;
        public string Text;
        public string ScriptEntry;
        public float Weight = 1f;
    }

    private readonly List<BarkLine> barkLines = new();
    private readonly Dictionary<string, float> barkCategoryCooldowns = new();
    private float barkGlobalTimer;

    protected void AddBarkLine(string category, string text, string scriptEntry = null, float weight = 1f)
    {
        barkLines.Add(new BarkLine { Category = category, Text = text, ScriptEntry = scriptEntry, Weight = weight });
    }

    public void Bark(string category)
    {
        if (barkCategoryCooldowns.TryGetValue(category, out float cd) && cd > 0f) return;

        var candidates = barkLines.FindAll(l => l.Category == category);
        if (candidates.Count == 0) return;

        var line = BarkWeightedRandom(candidates);

        barkGlobalTimer = BarkGlobalCooldown;
        barkCategoryCooldowns[category] = Random.Shared.NextSingle() * BarkCategoryCooldown;

        if (line.ScriptEntry != null)
        {
            var snd = SoundScriptManager.PlaySound(line.ScriptEntry, entity.Position);
         
            // Ideally, you should just play sounds directly from the morph. The only reason I'm doing this weird workaround is
            // 1: I didnt finish all of the morphs
            // 2: Both good and bad guys say the same dialog, just with different effects, so i was avoiding duplicating work.
            // In your own game, toss this weird lookup and change that 'false' to 'true', and make barks play morphs directly.
            if(soundPathToMorph.TryGetValue(Path.GetFileName(snd.SoundPath), out var morph))
            {
                model.PlayMorphAnimation(morph, entity.Position, false);
            }
        }
    }

    private void TickBarks(float dt)
    {
        barkGlobalTimer = MathF.Max(barkGlobalTimer - dt, 0f);
        var keys = new List<string>(barkCategoryCooldowns.Keys);
        foreach (var k in keys)
            barkCategoryCooldowns[k] = MathF.Max(barkCategoryCooldowns[k] - dt, 0f);
    }

    private static BarkLine BarkWeightedRandom(List<BarkLine> candidates)
    {
        float total = 0f;
        foreach (var c in candidates) total += c.Weight;
        float r = Random.Shared.NextSingle() * total;
        float acc = 0f;
        foreach (var c in candidates)
        {
            acc += c.Weight;
            if (r <= acc) return c;
        }
        return candidates[^1];
    }

    // Wander

    public float WanderHomeRadius = 20f;
    public float WanderRevisitCooldown = 30f;
    public float WanderTimeout = 8f;
    public float ThreatRadius = 4.5f;

    private Vector3 homePosition;
    private readonly List<(Vector3 pos, float timestamp)> visitedSpots = new();
    private float wanderTimer;

    protected void SetWanderHome(Vector3 pos) => homePosition = pos;

    protected Vector3 PickWanderPoint()
    {
        Vector3 best = entity.Position;
        float bestScore = float.MinValue;

        for (int i = 0; i < 12; i++)
        {
            float angle = Random.Shared.NextSingle() * MathF.Tau;
            float radius = MathF.Sqrt(Random.Shared.NextSingle()) * WanderHomeRadius;
            var candidate = homePosition + new Vector3(MathF.Cos(angle) * radius, 0f, MathF.Sin(angle) * radius);

            var hit = BSPRoot.TraceRay(new Ray(candidate + Vector3.Up * 2f, Vector3.Down), 10f, false);
            if (!hit.Hit) continue;
            candidate = hit.Point;

            float score = ScoreWanderCandidate(candidate);
            if (score > bestScore) { bestScore = score; best = candidate; }
        }

        wanderTimer = 0f;
        RecordVisit(best);
        return best;
    }

    protected bool ShouldRewander() => wanderTimer >= WanderTimeout || IsPathCompleted();

    protected void RecordVisit(Vector3 pos)
    {
        visitedSpots.Add((pos, (float)Environment.TickCount64 / 1000f));
        if (visitedSpots.Count > 12) visitedSpots.RemoveAt(0);
    }

    private float ScoreWanderCandidate(Vector3 pos)
    {
        float score = 0f;
        float now = (float)Environment.TickCount64 / 1000f;
        foreach (var (spot, time) in visitedSpots)
        {
            if (Vector3.Distance(pos, spot) < 2f)
            {
                float age = now - time;
                score -= MathF.Max(0f, (WanderRevisitCooldown - age) / WanderRevisitCooldown) * 3f;
            }
        }
        score += Vector3.Distance(entity.Position, pos) * 0.1f;
        score -= Vector3.Distance(homePosition, pos) * 0.05f;
        return score;
    }

    // Movement control

    protected float aiLookAngle;
    protected float aiSteerSpeed;
    protected float moveSpeedMultiplier = 1f;
    protected float movePathLengthMultiplier = 1f;

    private bool canMove = true;
    private bool canFire = true;
    private bool invincible;

    public void SetSpeed(SpeedPreset preset)
    {
        moveSpeedMultiplier = preset switch
        {
            SpeedPreset.Creep => Definition.CreepSpeed / Definition.RunSpeed,
            SpeedPreset.Walk  => Definition.WalkSpeed  / Definition.RunSpeed,
            _                 => 1f
        };
    }

    public void SetCanMove(bool enabled) => canMove = enabled;
    public void SetCanFire(bool enabled) => canFire = enabled;
    public void SetInvincible(bool value) => invincible = value;

    public void FaceMovementDirection()              => lookOverride = null;
    public void FaceBodyToward(Vector3? worldPos)    => lookOverride = worldPos;
    public void FaceBodyToward(WorldEntity target)   => lookOverride = target?.Position;

    protected abstract NpcDefinition CreateDefinition();
    protected virtual string InitialState => "idle";
    protected abstract void OnSetup();

    public override void OnSpawn()
    {
        if(soundPathToMorph == null)
        {
            soundPathToMorph = new Dictionary<string, string>();

            // Ideally, you should just play sounds directly from the morph. The only reason I'm doing this weird workaround is
            // 1: I didnt finish all of the morphs
            // 2: Both good and bad guys say the same dialog, just with different effects, so i was avoiding duplicating work.
            // In your own game, toss this weird lookup and change that 'false' to 'true', and make barks play morphs directly.
            foreach (var file in Directory.EnumerateFiles($"{MainEngine.FullPath}/Scenes/npc_vo"))
            {
                var morph = CMorphAnimData.LoadFromFile(file);
                var path = Path.GetRelativePath(MainEngine.FullPath, file);
                soundPathToMorph.Add(Path.GetFileName(morph.AudioPath), path);
            }
        }

        entity.IgnoreRagdolls = true;
        entity.PhysicsBody.AllowSleeping = false;

        Definition = CreateDefinition();
        aiSteerSpeed = Definition.SteerSpeed;
        homePosition = entity.Position;

        HP = MaxHP;
        entity.PhysicsBody.SetFriction(0);
        entity.Bounds = Definition.Bounds;

        SpawnAnimation();
        SpawnCombat();

        OnSetup();

        if (states.ContainsKey(InitialState))
        {
            currentStateName = InitialState;
            IsInCombat = states[currentStateName].IsCombatState;
            states[currentStateName].Enter?.Invoke();
        }

        allNpcs.Add(this);
    }

    public override void OnBeforeRender(GameTime gameTime) => model.RenderShadowTexture(entity);

    public override void OnRender(GameTime gameTime)
    {
        model.Transform =
              Matrix.CreateScale(entity.Scale * Definition.Scale)
            * Matrix.CreateFromQuaternion(entity.Rotation)
            * Matrix.CreateTranslation(entity.Position
                + Vector3.UnitY * entity.GetRealBounds().Min.Y * entity.Scale * Definition.Scale);

        model.CheckForLights(entity.OrientedBounds.Center);
        model.Draw();
        DrawDebug();
    }

    public override void OnUpdate(GameTime gameTime)
    {
        if (IsDead)
        {
            if (entity.WillRenderThisFrame)
            {
                model.Update();
            }

            model.UpdatePlayingSound(entity.Position);

            entity.Velocity = Vector3.Zero;
            if (entity.IsSimulated)
            {
                entity.IgnoreCollision = true;
                entity.IsSimulated = false;
                entity.Bounds = new BoundingBox();
            }
            return;
        }

        entity.Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, aiLookAngle * CMath.Deg2Rad);

        if (GameEngine.AIDisable)
        {
            model.Update();
            model.UpdatePlayingSound(entity.Position);
            weaponAnimator?.Update();
            currentWeapon.Model?.ApplyBoneMaps();
            return;
        }

        float dt = MainEngine.PreviousFrameDelta;

        if (IsActing)
        {
            if (canMove) MoveStep();

            if (currentChoreoLookTarget != null && !currentChoreoLookTarget.IsDespawned)
            {
                AddLookTarget(currentChoreoLookTarget.Position, 100f);
            }
            else
            {
                currentChoreoLookTarget = null;

                if (!IsInCombat) TickSocialGlances(dt);
            }

            UpdateAnimation(dt);

            if (entity.WillRenderThisFrame)
            {
                model.Update();
            }
            model.UpdatePlayingSound(entity.Position);
            weaponAnimator?.Update();

            return;
        }

        // This will actually lag one frame behind, but I dont think that matters.
        if (shouldMoveThisFrame)
        {
            UpdatePerception(MoveStepIntervalSeconds);
            TickBarks(MoveStepIntervalSeconds);
            BestThreat = ChooseBestThreat();
        }

        wanderTimer += dt;
        TickStateMachine(dt);

        if (!IsInCombat)
        {
            TickSocialGlances(dt);
        }

        if (canMove) MoveStep();
        TickCombat();
        UpdateAnimation(dt);

        if (entity.WillRenderThisFrame)
        {
            model.Update();
        }
        model.UpdatePlayingSound(entity.Position);
        weaponAnimator?.Update();
    }

    public override void OnDespawn()
    {
        currentWeapon?.ExtinguishMuzzleflash();
        currentWeapon?.OnHolstered();
        model.Dispose();
        LeaveSquad();
        allNpcs.Remove(this);
    }

    public override void OnTakeDamage(DamageInfo info)
    {
        if (IsDead || invincible) return;

        HP -= info.damage;
        bool wasDead = IsDead;
        IsDead = HP <= 0;

        OnDamaged(info);
        if (!wasDead && IsDead) OnKilled(info);
    }

    public override bool ShouldRayCollide(Ray ray)
    {
        foreach(var bone in model.BoneBounds)
        {
            if (bone.Intersects(ref ray, out _)) return true;
        }
        return false;
    }

    protected virtual void OnDamaged(DamageInfo info)
    {
        if (info.hitLocation != null)
        {
            FaceBodyToward(info.hitLocation);
        }
        if (info.from != null)
        {
            RegisterThreat(info.from, 1f);
            FaceBodyToward(info.from.Position);
        }
        TriggerFlinch();
        Bark(IsDead? "dead" : "pain");
    }

    protected virtual void OnKilled(DamageInfo info)
    {
        if (currentWeapon != null)
        {
            var weaponPickup = new WeaponPickup();
            weaponPickup.properties = new EntityProperty[]
            {
                new EntityProperty
                {
                    Name = "Weapon Code Name",
                    Value = WeaponRegistry.GetNameForWeapon(currentWeapon.GetType())
                }
            };
            // In the future you'd probably have a weapon pickup with physics of some kind
            weaponPickup.Position = entity.OrientedBounds.Center;

            EntityManager.SpawnEntity(weaponPickup);
        }

        currentWeapon?.ExtinguishMuzzleflash();
        currentWeapon?.OnHolstered();
        model.BecomeRagdoll();
        LeaveSquad();

        if (info.hitLocation != null)
        {
            Vector3 dir = Vector3.Normalize(entity.OrientedBounds.Center - (Vector3)info.hitLocation);
            if (info.from != null)
                dir = Vector3.Normalize(dir + Vector3.Normalize(
                    entity.OrientedBounds.Center - info.from.OrientedBounds.Center));

            model.ApplyImpulseAtPoint(
                CMath.ClampToBoundingBox((Vector3)info.hitLocation, entity.OrientedBounds.GetBoundingBox()),
                    dir * Math.Clamp(info.damage, 1, 40) * 100f, 16f);
        }

        entity.CallOutput("OnDeath", entity);
    }

    public void JoinSquad(NpcSquad squad)
    {
        LeaveSquad();
        Squad = squad;
        squad.Add(this);
    }

    public void LeaveSquad()
    {
        if (Squad == null) return;
        Squad.Remove(this);
        Squad = null;
    }

    private WorldEntity ChooseBestThreat() =>
        Threats.Count == 0 ? null :
        Threats
            .OrderByDescending(t => t.IsCurrentlyVisible ? t.ThreatLevel + 1f : t.ThreatLevel)
            .ThenBy(t => t.LastSeenTime)
            .Where(t => t.Entity.Controller is not LivingActor actor || !actor.IsDead)
            .FirstOrDefault()?.Entity;

    public override CustomSaveData CaptureCustomData()
    {
        CustomSaveData d = new CustomSaveData();
        d.vals = new Dictionary<string, object>();

        d.vals["dead"] = IsDead;
        d.vals["ragdollbones"] = IsDead ? MatricesToJArray(model.GetRagdollMatrices()) : null;
        d.vals["ragdollroot"] = IsDead ? JObject.FromObject(model.Transform) : null;

        return d;
    }
    JArray MatricesToJArray(Matrix[] matrices)
    {
        var jArray = new JArray();
        foreach (var matrix in matrices)
            jArray.Add(JObject.FromObject(matrix));
        return jArray;
    }
    public override void RestoreCustomData(CustomSaveData? o)
    {
        if (o == null || o.Value.vals == null) return;
        var data = o.Value.vals;

        IsDead = (bool)data["dead"];

        // Wait one frame for the model and stuff to be setup normally
        Defer(() =>
        {
            if (IsDead)
            {
                entity.IsSimulated = false;
                entity.IgnoreCollision = true;
                entity.Bounds = new BoundingBox();

                var ragdollRoot = (JObject)data["ragdollroot"];
                model.Transform = ragdollRoot.ToObject<Matrix>();

                var bonesJson = (JArray)data["ragdollbones"];
                Matrix[] bones = bonesJson.ToObject<Matrix[]>();

                currentWeapon?.ExtinguishMuzzleflash();
                currentWeapon?.OnHolstered();
                model.BecomeRagdoll();
                model.SetRagdollMatrices(bones);
                LeaveSquad();
            }
        },0f);
    }
}
