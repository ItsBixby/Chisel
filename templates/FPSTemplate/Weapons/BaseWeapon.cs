using Engine;
using Chisel.Models;
using Engine.Utils;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Rockwall;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPSTemplate.Weapons
{
    public enum FireMode
    {
        Primary,
        Secondary
    }

    public interface WeaponFireMode
    {
        float FireRate { get; }
        bool HasAmmo { get; }
        bool NeedsReload { get; }
        int CurrentAmmo { get; }
        int MagazineSize { get; }
        AmmoType AmmoType { get; }
        AmmoContainer AmmoContainer { get; }
        bool OnFire();
        bool OnInputPressed();
        void OnInputReleased();
        bool TryFireImmediate();
        void Reload();
        bool Update();
    }

    public abstract class BaseFireMode : WeaponFireMode
    {
        public abstract float FireRate { get; }
        protected abstract AmmoContainer Container { get; }
        protected virtual int AmmoPerShot => 1;

        protected BaseWeapon weapon;
        protected float currentRefire;
        protected bool held;

        protected BaseFireMode(BaseWeapon weapon)
        {
            this.weapon = weapon;
        }

        public bool HasAmmo => Container == null || Container.HasAmmo(AmmoPerShot);
        public bool NeedsReload => Container?.NeedsReload ?? false;
        public int CurrentAmmo => Container?.CurrentAmmo ?? 0;
        public int MagazineSize => Container?.MagazineSize ?? 0;
        public AmmoType AmmoType => Container?.AmmoType ?? AmmoType.None;
        public AmmoContainer AmmoContainer => Container;
        protected bool CanFire => currentRefire <= 0 && HasAmmo && weapon.State == BaseWeapon.WeaponState.Idle;

        public virtual bool OnInputPressed()
        {
            held = true;
            return CanFire && Fire();
        }

        public virtual void OnInputReleased() => held = false;

        public bool TryFireImmediate()
        {
            if (!CanFire) return false;
            return Fire();
        }

        public bool Update()
        {
            currentRefire -= MainEngine.PreviousFrameDelta;
            if (held && CanFire) return Fire();
            return false;
        }

        private bool Fire()
        {
            if (Container != null && !Container.TryConsume(AmmoPerShot)) return false;
            currentRefire = FireRate;
            bool hit = OnFire();
            AfterFire();
            return hit;
        }

        protected virtual void AfterFire() { }

        public void Reload() => Container?.Reload();

        public abstract bool OnFire();
    }

    [JsonConverter(typeof(BaseWeaponConverter))]
    public abstract class BaseWeapon
    {
        public BaseWeapon()
        {
            SetupFireModes();
        }

        public string Type => GetType().AssemblyQualifiedName;

        public abstract float ViewPunch { get; }
        public abstract float ViewPunchOffset { get; }
        public abstract string WeaponUIAssetName { get; }

        public enum WeaponState { Idle, Drawing, Holstering, Firing, Reloading, Custom }

        [JsonIgnore]
        public WeaponState State { get; private set; } = WeaponState.Idle;
        public event Action<WeaponState, WeaponState> OnStateChanged;
        [JsonIgnore]
        public WeaponAnimator Animator { get; private set; }

        [JsonIgnore]
        public WorldEntity Owner;
        [JsonIgnore]
        public CModelDisplay Model;
        [JsonIgnore]
        public CModelDisplay WeaponModel { get; protected set; }
        public bool HeldByNPC;

        private WeaponInventory inventory;
        [JsonIgnore]
        public WeaponInventory Inventory
        {
            get => inventory;
            set
            {
                inventory = value;
                OnInventoryChanged(value);
            }
        }

        private readonly Dictionary<FireMode, WeaponFireMode> fireModes = new();

        // Specifically just for json
        [JsonIgnore]
        public Dictionary<FireMode, int> AmmoSnapshot
        {
            get => fireModes
                .Where(kv => kv.Value.AmmoContainer != null)
                .ToDictionary(kv => kv.Key, kv => kv.Value.CurrentAmmo);
            set
            {
                if (value == null) return;
                foreach (var (slot, ammo) in value)
                    if (fireModes.TryGetValue(slot, out var mode) && mode.AmmoContainer != null)
                        mode.AmmoContainer.SetCurrentAmmo(ammo);
            }
        }

        [JsonIgnore]
        public Vector3 Forward { get; private set; }
        [JsonIgnore]
        public Vector3 Right { get; private set; }
        [JsonIgnore]
        public Vector3 Up { get; private set; }
        [JsonIgnore]
        public Vector3 Position { get; private set; }

        private int muzzleflashLightIndex = -1;
        private Light muzzleflashLight;
        private float muzzleflashTime;
        private const float muzzleflashTimeMax = 0.05f;

        public void SetAnimator(WeaponAnimator animator)
        {
            if (Animator != null)
                Animator.OnAnimationFinished -= HandleAnimationFinished;

            Animator = animator;

            if (Animator != null)
            {
                Animator.OnStateRequested += HandleStateRequested;
                Animator.OnAnimationFinished += HandleAnimationFinished;
                RegisterAnimations();
            }
        }
        private void HandleStateRequested(WeaponState state)
            => TransitionTo(state);
        public virtual void RegisterAnimations() { }
        protected virtual void HandleAnimationFinished(string animName) { }
        protected void TransitionTo(WeaponState newState)
        {
            var old = State;
            State = newState;
            OnStateChanged?.Invoke(old, newState);
        }

        protected abstract void SetupFireModes();
        protected virtual void OnInventoryChanged(WeaponInventory inv)
        {
            foreach (var mode in fireModes.Values)
                if (mode.AmmoContainer != null) mode.AmmoContainer.Inventory = inv;
        }

        protected void RegisterFireMode(FireMode slot, WeaponFireMode mode)
            => fireModes[slot] = mode;

        public WeaponFireMode GetFireMode(FireMode slot)
            => fireModes.GetValueOrDefault(slot);

        public virtual void OnReload() { }

        public void ReloadFireMode(FireMode slot)
        {
            if (State == WeaponState.Reloading) return;
            if (!fireModes.TryGetValue(slot, out var mode)) return;
            if (!mode.NeedsReload) return;

            OnReload();
            mode.Reload();
        }

        public bool Update()
        {
            muzzleflashTime -= MainEngine.PreviousFrameDelta;
            if (muzzleflashTime < 0) ExtinguishMuzzleflash();

            bool anyHit = false;
            foreach (var mode in fireModes.Values)
                if (mode.Update()) anyHit = true;

            return anyHit;
        }

        public virtual void UpdateSpecial(Vector3 forward, Vector3 right, Vector3 up, Vector3 position)
        {
            Forward = forward;
            Right = right;
            Up = up;
            Position = position;
        }

        public bool FireModePressed(FireMode slot, Vector3 forward, Vector3 right, Vector3 up, Vector3 position)
        {
            if (!fireModes.TryGetValue(slot, out var mode)) return false;

            UpdateSpecial(forward, right, up, position);
            return mode.OnInputPressed();
        }

        public void FireModeReleased(FireMode slot)
            => fireModes.GetValueOrDefault(slot)?.OnInputReleased();

        public virtual void OnEquipped() { }
        public virtual void OnHolstered() { }

        public void MuzzleFlash(Vector3 pos)
        {
            muzzleflashTime = muzzleflashTimeMax;
            ExtinguishMuzzleflash();

            muzzleflashLight = new Light
            {
                Position = pos,
                Range = 15,
                Color = Color.LightYellow,
                Type = Light.LightType.Point,
                Intensity = 0.2f,
            };
            muzzleflashLightIndex = MainEngine.CurrentRealtimeLights.Add(muzzleflashLight);
        }

        public void ExtinguishMuzzleflash()
        {
            if (muzzleflashLightIndex != -1)
            {
                MainEngine.CurrentRealtimeLights.Remove(muzzleflashLightIndex);
                muzzleflashLightIndex = -1;
            }
        }

        public static Vector3 GetSpreadDirection(Vector3 forward, Vector3 right, Vector3 up, float maxSpread)
        {
            return (Matrix.CreateFromAxisAngle(forward, (Random.Shared.NextSingle() * 2 - 1) * maxSpread) *
                    Matrix.CreateFromAxisAngle(right, (Random.Shared.NextSingle() * 2 - 1) * maxSpread) *
                    Matrix.CreateFromAxisAngle(up, (Random.Shared.NextSingle() * 2 - 1) * maxSpread) *
                    Matrix.Invert(Matrix.CreateLookAt(Vector3.Zero, forward, up))).Forward;
        }
    }

    public class BaseWeaponConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(BaseWeapon).IsAssignableFrom(objectType);
        public override bool CanWrite => false;

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, Newtonsoft.Json.JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var typeName = obj["Type"]?.Value<string>()
                ?? throw new JsonException("Missing Type discriminator on BaseWeapon");
            var type = Type.GetType(typeName)
                ?? throw new JsonException($"Unknown weapon type: {typeName}");
            var weapon = (BaseWeapon)Activator.CreateInstance(type);
            serializer.Populate(obj.CreateReader(), weapon);
            return weapon;
        }

        public override void WriteJson(JsonWriter writer, object value, Newtonsoft.Json.JsonSerializer serializer)
            => throw new NotImplementedException();
    }
}
