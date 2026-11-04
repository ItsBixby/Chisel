using Chisel.Utils;
using Engine;
using Engine.Compilation;
using Engine.Console;
using Engine.Entities;
using Engine.Entities.PhysicsEntities;
using Engine.Input;
using Engine.Physics;
using Engine.Physics.Constraints;
using Engine.Rendering;
using Engine.SaveSystem;
using Engine.Scripting.Sound;
using Engine.UI;
using Engine.Utils;
using EngineTesting.Utilities;
using EngineTesting.Utilities.Player;
using EngineTesting.Weapons;
using Microsoft.Xna.Framework;
using MonoGameGum.GueDeriving;
using Newtonsoft.Json.Linq;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineTesting.Entities;

public partial class BasePlayerController : CameraController, LivingActor
{
    public static readonly BoundingBox UncrouchedBounds = new BoundingBox(-new Vector3(0.25f, 2.0f, 0.25f), new Vector3(0.25f, 0.15f, 0.25f));
    public static readonly BoundingBox CrouchedBounds = new BoundingBox(-new Vector3(0.25f, 1.1f, 0.25f), new Vector3(0.25f, 0.15f, 0.25f));

    private const float HoldMaxStretch = 2.0f;
    const float StepTimerWait = 0.39f;

    public static CVarFloat MaxSprintSpeed = new CVarFloat("pl_sprintspeed", 8f);
    public static CVarFloat MaxRunSpeed = new CVarFloat("pl_runspeed", 6f);
    public static CVarFloat MaxCrouchSpeed = new CVarFloat("pl_crouchspeed", 3.5f);
    public static CVarFloat MaxAirSpeed = new CVarFloat("pl_airspeed", 1f);
    public static CVarFloat MaxAccel = new CVarFloat("pl_acceleration", 200f);
    public static CVarFloat MaxSafeFall = new CVarFloat("pl_maxfallspeed", 13f);
    public static CVarFloat FallDamageScale = new CVarFloat("pl_falldamagescale", 0.4f);
    public static CVarFloat GroundFriction = new CVarFloat("pl_friction", 10f);
    public static CVarFloat ViewmodelReactSpeed = new CVarFloat("vm_reactspeed", 16f);
    public static CVarFloat ViewmodelSwayScale = new CVarFloat("vm_swayscale", 0.35f);
    public static CVarFloat FovWorld = new CVarFloat("fov", 85f);
    public static CVarFloat FovViewmodel = new CVarFloat("vm_fov", 70f);
    public static CVarBool DrawViewmodel = new CVarBool("vm_render", true);
    public static CVarBool DrawHud = new CVarBool("hud_render", true);
    public static CVarBool Noclip = new CVarBool("noclip", false);
    public static CVarBool GodMode = new CVarBool("god", false);
    public static CVarBool Thirdperson = new CVarBool("thirdperson", false);
    public static CommandBinding GiveAll = new CommandBinding("give_all", (args) =>
    {
        foreach (var plr in EntityManager.entities.GetValues().ToArray().Where(e => e is Player))
        {
            if (plr.Controller is not BasePlayerController controller) continue;

            foreach (var name in WeaponRegistry.GetAllWeaponNames())
            {
                WeaponRegistry.AcquireWeapon(controller.WeaponInventory, name);
            }
        }
    });
    public static CommandBinding Give = new CommandBinding("give", (args) =>
    {
        if (args.Length == 0) return;

        foreach (var plr in EntityManager.entities.GetValues().ToArray().Where(e => e is Player))
        {
            if (plr.Controller is not BasePlayerController controller) continue;
            WeaponRegistry.AcquireWeapon(controller.WeaponInventory, args[0]);
        }
    });

    public static float MouseSensitivity = 1;

    [ExposeValue("max_hp")]
    public int MaxHP { get; } = 100;

    [ExposeValue("hp")]
    [SaveValue("hp")]
    public int HP { get; set; } = 0;

    [ExposeValue("is_dead")]
    [SaveValue("is_dead")]
    public bool IsDead { get; set; } = false;

    protected CModelDisplay Viewmodel;
    protected Matrix ViewmodelMatrix;

    protected TextRuntime HpNumberText;
    protected ContainerRuntime AmmoCountParent;
    protected TextRuntime AmmoCurrentText;
    protected TextRuntime AmmoReserveText;

    protected bool isFiring; // because we use triggers, and they dont have a "hasbeenpressed", we do this manually
    protected bool isAltFiring; 
    protected BaseWeapon CurrentWeapon => WeaponInventory.GetCurrentWeapon();
    protected WeaponInventory WeaponInventory;

    private int flashlightLightIndex = -1;
    private Light flashlightLight;

    private bool jumpQueued;
    private bool viewPunchSign;
    private bool wasOnGround;
    private bool holdingObject;
    private WorldEntity heldObject;
    private FixedConstraint heldObjectConstraint;
    private PhysicsAnchorEntity heldObjectAnchor;
    private (int s, int r) lastHeldWeapon;

    [SaveValue("fLight")] private bool flashlightEnabled;
    [SaveValue("crouch")] private bool isCrouched;
    [SaveValue("sprint")] private bool isSprinting;
    [SaveValue("pitch")] private float pitch;
    [SaveValue("yaw")] private float yaw;
    [SaveValue("vpitch")] private float viewmodelPitch;
    [SaveValue("vyaw")] private float viewmodelYaw;
    [SaveValue("vreel")] private float viewmodelReelin;

    private float totalAnimTime;
    private float viewPunchIntensity;
    private float viewPunchSinOffset;
    private float cameraHeightBlend;
    private float stepTimer;

    private float dieWaitTime;

    private Vector2 gamepadLookAcceleration;
    private Vector3 previousVelocity;

    private ProceduralBounceFollower cameraBounce;

    private float GetSpeed() => (isCrouched || entity.IsSubmerged ? MaxCrouchSpeed : isSprinting ? MaxSprintSpeed : MaxRunSpeed);

    public override void OnDespawn()
    {
        heldObjectConstraint?.Detach();
        heldObject = null;

        EntityManager.DespawnEntity(heldObjectAnchor);
        GumUtils.RemoveScreen("PlayerHUD");
        Viewmodel.Dispose();
        WorldModel?.Dispose();
    }

    public override void OnSpawn()
    {
        viewmodelPitch = pitch;
        viewmodelYaw = yaw;
        HP = MaxHP;
        IsDead = false;

        // Singleplayer, only one player at a time.
        var otherIndex = EntityManager.entities.FindIndex(e => e is Player);
        while (otherIndex != -1)
        {
            var other = EntityManager.entities[otherIndex];
            if (other != this.entity) EntityManager.DespawnEntity(other);
            otherIndex = EntityManager.entities.FindIndex(e => e is Player && !e.IsDespawned);
        }

        entity.PhysicsBody.AllowSleeping = false;

        Viewmodel = new CModelDisplay("Models/Player/v_arms.ccmdl");
        Viewmodel.DrawShadow = false;

        SpawnWorldModel();

        cameraBounce = new ProceduralBounceFollower(2.5f, 0.6f, 0f, entity.Position.Y);

        entity.PhysicsBody.SetFriction(0);
        entity.Bounds = UncrouchedBounds;
        base.OnSpawn();

        flashlightLight = new Light
        {
            Position = entity.Position,
            Type = Light.LightType.SpotLight,
            Angle = 40f,
            InnerAngle = 30f,
            Color = Color.White,
            Intensity = 0.8f,
            Range = 20f,
            Rotation = RenderEngine.CameraForward
        };

        var hudScreen = GumUtils.ShowScreen("PlayerHUD");

        WeaponInventory = new WeaponInventory(entity, hudScreen);
        WeaponInventory.OnWeaponChanged += WeaponSwitched;

        WeaponRegistry.AcquireWeapon(WeaponInventory, "w_fists");
        if ((bool)(entity.ReadProperty("Spawn with Full Loadout", EntityPropertyType.Bool) ?? false))
        {
            // Weird
            Defer(() => MainEngine.Instance.Console.Execute("give_all"), 0);
        }

        HpNumberText = hudScreen.GetGraphicalUiElementByName("HPtext") as TextRuntime;
        AmmoCurrentText = hudScreen.GetGraphicalUiElementByName("Ammotext") as TextRuntime;
        AmmoReserveText = hudScreen.GetGraphicalUiElementByName("AmmoReservetext") as TextRuntime;
        AmmoCountParent = hudScreen.GetGraphicalUiElementByName("AmmoView") as ContainerRuntime;

        heldObjectAnchor = new PhysicsAnchorEntity();
        EntityManager.SpawnEntity(heldObjectAnchor);
        heldObjectConstraint = new FixedConstraint();
        heldObjectConstraint.DisableAutoPointDetection = true;
    }

    public void GetWeapon(string name)
    {
        WeaponRegistry.AcquireWeapon(WeaponInventory, name);
        var wData = WeaponRegistry.GetWeaponFromName(name);

        if (wData.HasValue)
            WeaponInventory.SetSlots((wData.Value.slot, wData.Value.slotRow));
    }

    public override void OnUpdate(GameTime gameTime)
    {
        if (entity.PhysicsBody == null) return;

        if (PlayerInput.QuickSave.HasBeenPressed() && !IsDead) SaveManager.SaveGame(SaveManager.SaveType.Quick);
        if (PlayerInput.QuickLoad.HasBeenPressed()) { SaveManager.LoadGame("quick"); return; }

        if (!MainEngine.IsPaused) UpdateCamera(gameTime);

        entity.DeferRenderTillLast = !Thirdperson;

        UpdateHUD();

        // Viewmodel lag / sway
        float vmReelTarg = pitch / 90f + entity.Velocity.Y * 0.08f;
        viewmodelReelin = CMath.MoveTowards(viewmodelReelin, vmReelTarg, MathF.Abs(vmReelTarg - viewmodelReelin) * MainEngine.PreviousFrameDelta * ViewmodelReactSpeed * 0.25f);
        viewmodelPitch = CMath.MoveTowards(viewmodelPitch, pitch, MathF.Abs(pitch - viewmodelPitch) * MainEngine.PreviousFrameDelta * ViewmodelReactSpeed);
        viewmodelYaw = CMath.MoveTowards(viewmodelYaw, yaw, MathF.Abs(yaw - viewmodelYaw) * MainEngine.PreviousFrameDelta * ViewmodelReactSpeed);

        // View punch decay
        viewPunchSinOffset += MainEngine.PreviousFrameDelta * 2;
        viewPunchSinOffset %= MathF.Tau;
        viewPunchIntensity = CMath.MoveTowards(viewPunchIntensity, 0f, MainEngine.PreviousFrameDelta * viewPunchIntensity * 4);

        // Camera height smoothing + bounce
        cameraHeightBlend = CMath.MoveTowards(cameraHeightBlend, 0f, MainEngine.PreviousFrameDelta * MathF.Abs(cameraHeightBlend) * 10);
        float cameraBounceOffset = entity.Position.Y - cameraBounce.Update(MainEngine.PreviousFrameDelta, entity.Position.Y);
        cameraBounceOffset -= cameraHeightBlend;

        totalAnimTime += MainEngine.PreviousFrameDelta;

        float vlen = new Vector2(entity.Velocity.X, entity.Velocity.Z).Length();
        var horizMove = vlen / MaxRunSpeed;

        if (Thirdperson)
        {
            UpdateThirdpersonCamera(gameTime, horizMove);
        }
        else
        {
            ViewMatrix = Matrix.CreateTranslation(-entity.Position)
                       * Matrix.CreateTranslation(0, cameraHeightBlend, 0)
                       * Matrix.CreateFromQuaternion(Quaternion.Inverse(entity.Rotation));
            WorldMatrix = Matrix.CreateWorld(Vector3.Zero, Vector3.Forward, Vector3.Up);
        }


        var deltaYaw = (viewmodelYaw - yaw) * ViewmodelSwayScale;
        var deltaPitch = (viewmodelPitch - pitch) * ViewmodelSwayScale;

        var viewmodelRotation = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(deltaYaw), MathHelper.ToRadians(deltaPitch), 0)
                               * Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(yaw), MathHelper.ToRadians(pitch), 0);
        var flashlightRotation = Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(-deltaYaw) * 0.25f, MathHelper.ToRadians(-deltaPitch) * 0.25f, 0)
                               * Matrix.CreateFromYawPitchRoll(MathHelper.ToRadians(yaw), MathHelper.ToRadians(pitch), 0);


        ViewmodelMatrix = Matrix.CreateWorld(Vector3.Zero, -Vector3.Forward, Vector3.Up)
                        * viewmodelRotation
                        * Matrix.CreateTranslation(viewmodelRotation.Up * -0.90f)
                        * Matrix.CreateTranslation(viewmodelRotation.Up * (viewmodelReelin * -0.03f))
                        * Matrix.CreateTranslation(viewmodelRotation.Forward * (viewmodelReelin * -0.05f - 0.06f))
                        * Matrix.CreateTranslation(viewmodelRotation.Right * MathF.Sin(totalAnimTime * 4) * horizMove * 0.024f)
                        * Matrix.CreateTranslation(viewmodelRotation.Up * MathF.Sin(totalAnimTime * 8) * horizMove * 0.01f)
                        * Matrix.CreateTranslation(0, -cameraHeightBlend - cameraBounceOffset * 0.007f, 0)
                        * Matrix.CreateTranslation(entity.Position);

        Move(gameTime, vlen);

        RequestCameraControl(0);

        if (IsDead)
        {
            dieWaitTime -= MainEngine.PreviousFrameDelta;

            if (dieWaitTime < 0 && PlayerInput.AnyPressed) SaveManager.LoadGame();

            if (Thirdperson)
            {
                WorldModel.Update();
            }

            return;
        }

        // Fall damage
        if (entity.IsOnGround && !wasOnGround && !entity.IsSubmerged)
        {
            float impact = MathF.Max(-previousVelocity.Y, 0);
            if (impact > MaxSafeFall)
            {
                float excess = impact - MaxSafeFall;
                float damage = excess * excess * FallDamageScale;

                viewPunchIntensity = damage * 0.01f;
                viewPunchSinOffset = MathF.PI + Random.Shared.NextSingle() * 0.1f;
                viewPunchSign = Random.Shared.NextSingle() > 0.5f;

                SoundScriptManager.PlaySound("Physics.FallDamage", entity.Position, is3DOverride: false);

                entity.TakeDamage(new DamageInfo { damage = (int)damage, damageType = -1, from = entity });
            }
        }
        wasOnGround = entity.IsOnGround;

        TryInteract();
        UpdateAnimations();

        if(Thirdperson)
        {
            UpdateWorldModelAnimation(MainEngine.PreviousFrameDelta);
        }

        isSprinting = PlayerInput.Sprint.IsPressed();

        // Crouch
        if (PlayerInput.Crouch.IsPressed() && !isCrouched)
        {
            entity.Bounds = CrouchedBounds;
            isCrouched = true;
            if (entity.IsOnGround)
            {
                entity.Position.Y += (UncrouchedBounds.Min.Y - CrouchedBounds.Min.Y) * entity.Scale.Y;
                cameraHeightBlend += (UncrouchedBounds.Min.Y - CrouchedBounds.Min.Y) * entity.Scale.Y;
            }
        }
        if (!PlayerInput.Crouch.IsPressed() && isCrouched)
        {
            entity.Bounds = UncrouchedBounds;
            isCrouched = false;
            if (entity.IsOnGround)
                entity.Position.Y -= (UncrouchedBounds.Min.Y - CrouchedBounds.Min.Y) * (entity.Scale.Y + 0.025f);

            if (Collision.CheckBounds(entity))
            {
                entity.Bounds = CrouchedBounds;
                isCrouched = true;
                if (entity.IsOnGround)
                    entity.Position.Y += (UncrouchedBounds.Min.Y - CrouchedBounds.Min.Y) * (entity.Scale.Y + 0.025f);
            }
            else if (entity.IsOnGround)
            {
                cameraHeightBlend -= (UncrouchedBounds.Min.Y - CrouchedBounds.Min.Y) * entity.Scale.Y;
            }
        }

        // Weapons
        UpdateWeapons(flashlightRotation);

        // Flashlight
        if (flashlightEnabled && flashlightLightIndex == -1)
        {
            flashlightLightIndex = MainEngine.CurrentRealtimeLights.Add(flashlightLight);
        }

        if (PlayerInput.Flashlight.HasBeenPressed())
        {
            flashlightEnabled = !flashlightEnabled;
            if (flashlightEnabled) flashlightLightIndex = MainEngine.CurrentRealtimeLights.Add(flashlightLight);
            else MainEngine.CurrentRealtimeLights.Remove(flashlightLightIndex);
        }
        if (flashlightEnabled)
        {
            flashlightLight.Position = entity.Position + Vector3.Up * -cameraHeightBlend - RenderEngine.CameraRight*0.1f - Vector3.Up*0.1f;
            flashlightLight.Rotation = flashlightRotation.Forward;
            MainEngine.CurrentRealtimeLights[flashlightLightIndex] = flashlightLight;
        }

        int PissIndex = 0;

        //Piss Mechanic
        if (PlayerInput.Piss.HasBeenPressed())
        {
            PissIndex = ParticleManager.SpawnParticleSystem(entity.Position, $"{GameEngine.FullPath}/Particles/Piss.crp");
            Logger.AppendInfo("This nigga's pissing!!!" + PissIndex);


        }
        if (PlayerInput.Piss.HasBeenReleased())
        {
            //ParticleManager.GetSpawner(PissIndex).system.
        }

            previousVelocity = entity.Velocity;
    }

    private void UpdateWeapons(Matrix flashlightRotation)
    {
        if (CurrentWeapon == null) return;

        if (holdingObject) return;

        if (CurrentWeapon.Update())
            SetViewPunch(CurrentWeapon.ViewPunch, CurrentWeapon.ViewPunchOffset);

        CurrentWeapon.UpdateSpecial(RenderEngine.CameraForward, RenderEngine.CameraRight, RenderEngine.CameraUp, entity.Position);

        if (PlayerInput.Fire.IsPressed() && !isFiring)
        {
            isFiring = true;
            // If we hit something, punch view
            if (CurrentWeapon.FireModePressed(FireMode.Primary, RenderEngine.CameraForward, RenderEngine.CameraRight, RenderEngine.CameraUp, entity.Position))
            {
                SetViewPunch(CurrentWeapon.ViewPunch, CurrentWeapon.ViewPunchOffset);
            }
        }
        if(!PlayerInput.Fire.IsPressed() && isFiring)
        {
            isFiring = false;
            CurrentWeapon.FireModeReleased(FireMode.Primary);
        }
        if (PlayerInput.AltFire.IsPressed() && !isAltFiring)
        {
            isAltFiring = true;
            // Same for alt.
            if (CurrentWeapon.FireModePressed(FireMode.Secondary, RenderEngine.CameraForward, RenderEngine.CameraRight, RenderEngine.CameraUp, entity.Position))
            {
                SetViewPunch(CurrentWeapon.ViewPunch, CurrentWeapon.ViewPunchOffset);
            }
        }
        if (!PlayerInput.AltFire.IsPressed() && isAltFiring)
        {
            isAltFiring = false;
            CurrentWeapon.FireModeReleased(FireMode.Secondary);
        }

        if (PlayerInput.Reload.IsPressed())
        {
            CurrentWeapon.ReloadFireMode(FireMode.Primary);
            CurrentWeapon.ReloadFireMode(FireMode.Secondary);
        }

        if (!PlayerInput.Fire.IsPressed() && !PlayerInput.AltFire.IsPressed())
        {
            if (PlayerInput.Slot0.HasBeenPressed()) WeaponInventory.SelectSlot(0);
            if (PlayerInput.Slot1.HasBeenPressed()) WeaponInventory.SelectSlot(1);
            if (PlayerInput.Slot2.HasBeenPressed()) WeaponInventory.SelectSlot(2);
            if (PlayerInput.Slot3.HasBeenPressed()) WeaponInventory.SelectSlot(3);
            if (PlayerInput.Slot4.HasBeenPressed()) WeaponInventory.SelectSlot(4);
            if (PlayerInput.Slot5.HasBeenPressed()) WeaponInventory.SelectSlot(5);
            if (PlayerInput.Slot6.HasBeenPressed()) WeaponInventory.SelectSlot(6);
            if (PlayerInput.Slot7.HasBeenPressed()) WeaponInventory.SelectSlot(7);
            if (PlayerInput.Slot8.HasBeenPressed()) WeaponInventory.SelectSlot(8);

            if (!MainEngine.IsPaused)
            {
                if (BetterMouse.GetScrollDir() > 0 || PlayerInput.SlotNext.HasBeenPressed()) WeaponInventory.ScrollUp();
                if (BetterMouse.GetScrollDir() < 0 || PlayerInput.SlotPrev.HasBeenPressed()) WeaponInventory.ScrollDown();
            }
        }
    }

    private void SetViewPunch(float intensity, float sinOffset)
    {
        viewPunchIntensity = intensity;
        viewPunchSinOffset = sinOffset;
        viewPunchSign = Random.Shared.NextSingle() > 0.5f;
    }

    private void UpdateCamera(GameTime gameTime)
    {
        Vector2 mouseMove = BetterMouse.GetMovement();
        yaw -= mouseMove.X * MouseSensitivity * 0.1f;
        pitch -= mouseMove.Y * MouseSensitivity * 0.1f;

        var lookDirGamepad = new Vector2(PlayerInput.GamepadLookX.GetValue(), PlayerInput.GamepadLookY.GetValue());
        gamepadLookAcceleration = CMath.MoveTowards(gamepadLookAcceleration, lookDirGamepad,
            Vector2.Distance(lookDirGamepad, gamepadLookAcceleration) * MainEngine.PreviousFrameDelta * 25);

        yaw -= gamepadLookAcceleration.X * MainEngine.PreviousFrameDelta * 180;
        pitch += gamepadLookAcceleration.Y * MainEngine.PreviousFrameDelta * 180;

        pitch = MathF.Max(MathF.Min(pitch, 89), -89);
        entity.Rotation = (Quaternion.CreateFromYawPitchRoll(
            yaw * CMath.Deg2Rad + MathF.Sin(viewPunchSinOffset * (viewPunchSign ? 1 : -1)) * viewPunchIntensity,
            pitch * CMath.Deg2Rad + MathF.Cos(viewPunchSinOffset * (viewPunchSign ? 1 : -1)) * viewPunchIntensity,
            0));
    }

    private void UpdateHUD()
    {
        GumUtils.GetScreen("PlayerHUD").Visible = DrawHud;
        HpNumberText.Text = int.Max(HP, 0).ToString();
        WeaponInventory.UpdateUI();

        if (CurrentWeapon == null) { AmmoCountParent.Visible = false; return; }

        var primary = CurrentWeapon.GetFireMode(FireMode.Primary);
        if (primary == null || primary.AmmoType == AmmoType.None || IsDead) { AmmoCountParent.Visible = false; return; }

        AmmoCountParent.Visible = true;
        AmmoCurrentText.Text = primary.CurrentAmmo.ToString();
        AmmoReserveText.Text = WeaponInventory.GetAmmo(primary.AmmoType).ToString();
    }

    public override void OnTakeDamage(DamageInfo info)
    {
        if (GodMode) return;

        HP -= info.damage;
        if (HP <= 0 && !IsDead)
        {
            IsDead = true;

            dieWaitTime = 1f;

            entity.Bounds = new BoundingBox(new Vector3(CrouchedBounds.Min.X, CrouchedBounds.Min.Y * 0.5f, CrouchedBounds.Min.Z), CrouchedBounds.Max);
            CurrentWeapon?.OnHolstered();
            Viewmodel.Visible = false;

            if(Thirdperson)
            {
                WorldModel.BecomeRagdoll();
            }
        }
    }

    public override void OnBeforeRender(GameTime gameTime)
    {
        if(Thirdperson)
        {
            WorldModel.RenderShadowTexture(entity);
        }
    }
    public override void OnRender(GameTime gameTime)
    {
        if(!Thirdperson)
        {
            RenderViewmodel();
        }
        else
        {
            RenderWorldModel();
        }
        FOV = FovWorld;
    }

    public void RenderViewmodel()
    {
        FOV = FovWorld;

        if (!DrawViewmodel) return;
        if (CurrentWeapon == null) return;

        FOV = FovViewmodel;

        RequestCameraControl(0);
        CameraControl.Evaluate();

        Viewmodel.Transform = ViewmodelMatrix;
        Viewmodel.CheckForLights(entity.Position);

        Matrix zSquish = Matrix.CreateScale(1f, 1f, 0.1f);

        Viewmodel.Draw(ViewMatrix, ProjectionMatrix * zSquish, ignoreFrustum: true);

        FOV = FovWorld;

        RequestCameraControl(0);
        CameraControl.Evaluate();
    }

    private void UpdateAnimations()
    {
        Viewmodel?.Update();
        CurrentWeapon?.Animator.Update();
    }

    private void WeaponSwitched(BaseWeapon oldWeapon, BaseWeapon newWeapon)
    {
        if (oldWeapon != null) { oldWeapon.SetAnimator(null); oldWeapon.Model = null; }

        var animator = new WeaponAnimator(Viewmodel, "main");
        newWeapon.Model = Viewmodel;
        newWeapon.SetAnimator(animator);
    }

    void TryInteract()
    {
        Vector3 rayForward = entity.WorldTransformMatrix.Forward;
        Vector3 rayRight = entity.WorldTransformMatrix.Right;
        Vector3 objectForward = Matrix.CreateFromYawPitchRoll(
            yaw * CMath.Deg2Rad,
            float.Clamp(pitch, -60, 60) * CMath.Deg2Rad,
            0).Forward;
        Ray ray = new Ray(entity.Position, rayForward);

        var result = BSPRoot.TraceRay(ray, 4f);
        var found = Collision.CastEntity(ray, Vector3.Distance(result.Point, ray.Position), out _, entity.PhysicsBodyID);

        var target = entity.Position + objectForward * 3f;

        if (PlayerInput.Fire.HasBeenPressed())
        {
            if (holdingObject)
            {
                PhysicsEngine.BodyInterface.AddTorque(heldObject.PhysicsBodyID, rayRight.ToNumerics() * 6000);
                BreakHold(rayForward * 7f + entity.Velocity);
                return;
            }
        }

        if (PlayerInput.Use.HasBeenPressed())
        {
            if (holdingObject)
            {
                BreakHold();
                return;
            }

            if (found == null)
            {
                SoundScriptManager.PlaySound("General.Deny", entity.Position);
                return;
            }

            SoundScriptManager.PlaySound("General.Accept", entity.Position);

            if (found.Controller is BasePhysicsProp)
            {
                lastHeldWeapon = WeaponInventory.GetSlots();
                WeaponInventory.SelectSlot(-1);
                heldObject = found;
                holdingObject = true;
                heldObject.IgnoredEntities.Add(entity);
                heldObjectConstraint.Attach(heldObject, heldObjectAnchor);
                return;
            }
            
            found.CallInput("Use", "", entity);
        }

        if (holdingObject)
        {
            float distSq = Vector3.DistanceSquared(entity.Position, heldObject.Position);
            if (distSq > 5 * 5)
            {
                BreakHold();
                return;
            }

            //float stretchSq = Vector3.DistanceSquared(heldObjectAnchor.Position, heldObject.Position);
            //if (stretchSq > HoldMaxStretch * HoldMaxStretch)
            //{
            //    BreakHold();
            //    return;
            //}

            heldObjectAnchor.Velocity = (target - heldObjectAnchor.Position) * 15;

            // This is where the anchor WOULD move, were it to be a clear path.
            var finalAnchorPos = CMath.MoveTowards(heldObjectAnchor.Position, target, MainEngine.PreviousFrameDelta * Vector3.DistanceSquared(target, heldObjectAnchor.Position) * 25);
            const float ShapeExpansion = 1.25f;

            if (Collision.ShapeCastPath(
                    heldObject.PhysicsBody,
                    finalAnchorPos,
                    ShapeExpansion,
                    out _,
                    out var colPoint,
                    out var colNormal,
                    false,
                    heldObject.PhysicsBodyID,
                    heldObjectAnchor.PhysicsBodyID,
                    entity.PhysicsBodyID))
            {
                heldObjectAnchor.Position = colPoint + colNormal * 0.1f;
            }
            else
            {
                heldObjectAnchor.Position = finalAnchorPos;
            }
        }
        else
        {
            heldObjectAnchor.Position = target;
        }
    }
    private void BreakHold(Vector3? throwImpulse = null)
    {
        PhysicsEngine.BodyInterface.ActivateBody(heldObject.PhysicsBodyID);
        heldObjectConstraint.Detach();

        if (throwImpulse.HasValue)
        {
            heldObjectAnchor.Velocity += (throwImpulse.Value);
            heldObject.Velocity += (throwImpulse.Value);
        }

        heldObject.IgnoredEntities.Remove(entity);
        heldObject = null;
        holdingObject = false;
        WeaponInventory.SetSlots(lastHeldWeapon);
    }

    public override CustomSaveData CaptureCustomData()
    {
        return new CustomSaveData
        {
            vals = new Dictionary<string, object>
            {
                { "weapons", WeaponInventory.Capture() },
                { "holdingObject", holdingObject },
                { "heldObjectSaveID", heldObject?.SaveID.ToString()??"" },
                { "lastSlot", lastHeldWeapon.s },
                { "lastRow", lastHeldWeapon.r },
            }
        };
    }

    public override void RestoreCustomData(CustomSaveData? o)
    {
        if (!o.HasValue) return;
        WeaponInventory.Restore(o.Value.vals["weapons"] as JToken ?? JToken.FromObject(o.Value.vals["weapons"]));

        Defer(() =>
        {
            holdingObject = (bool)o.Value.vals["holdingObject"];
            if (holdingObject)
            {
                Guid saveID = Guid.Parse((string)o.Value.vals["heldObjectSaveID"]);
                lastHeldWeapon = (Convert.ToInt32(o.Value.vals["lastSlot"]), Convert.ToInt32(o.Value.vals["lastRow"]));

                var found = EntityManager.entities.FindIndex(e => e != null && e.SaveID == saveID);

                if (found != -1)
                {
                    heldObject = EntityManager.entities[found];
                    heldObjectConstraint.Attach(heldObject, heldObjectAnchor);
                }
                else
                {
                    holdingObject = false;

                    WeaponInventory.SetSlots(lastHeldWeapon);
                }
            }
        }, 0);
    }
}

[EntityDescriptor()]
[EntityBounds(-0.25f, -2.00f, -0.25f, 0.25f, 0.15f, 0.25f)]
[ExposeEntityProperty("Spawn with Full Loadout", EntityPropertyType.Bool, "False meaning spawns with no weapons, and true spawns with all weapons.")]
public class Player : WorldEntity
{
    public Player()
    {
        Controller = new BasePlayerController();
        Bounds = BasePlayerController.UncrouchedBounds;
        AxisAlignedBox = true;
        IsSimulated = true;
        IgnoreRagdolls = true; //TODO: step
        PhysicsDensityMultiplier = 2.4f;

        CollisionID = -1;
    }
}