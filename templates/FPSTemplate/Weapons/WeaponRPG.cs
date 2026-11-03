using Chisel.Utils.Animation;
using Engine;
using Engine.Scripting.Sound;
using Engine.Sound;
using Engine.Utils;
using FPSTemplate.Entities;
using FPSTemplate.Projectiles;
using Microsoft.Xna.Framework;
using System;

namespace FPSTemplate.Weapons
{
    public class WeaponRPG : BaseWeapon
    {
        public override float ViewPunch => 0.1f * (Random.Shared.NextSingle() * 0.1f + 0.9f);
        public override float ViewPunchOffset => (Random.Shared.NextSingle() - 0.5f);
        public override string WeaponUIAssetName => "smg";

        const string AnimDraw = "smg_draw";
        const string AnimReady = "smg_ready";
        const string AnimFire = "smg_fire";
        const string AnimReload = "smg_reload";

        protected override void SetupFireModes()
        {
            RegisterFireMode(FireMode.Primary, new RpgFireMode(this));
        }

        public override void RegisterAnimations()
        {
            WeaponModel = new CModelDisplay("Models/Weapons/v_smg.ccmdl");
            WeaponModel.DrawShadow = false;
            Model.SetSlot("weapon", WeaponModel);
            WeaponModel.AttachTo(Model, "weapon.root");

            Animator.RegisterSlot("weapon");

            WeaponModel.MapBoneToBone("weapon.root", Model);
            WeaponModel.MapBoneToBone("weapon.mag", Model);

            Animator.RegisterAnimation(AnimDraw, false);
            Animator.RegisterAnimation(AnimReady, true);
            Animator.RegisterAnimation(AnimFire, false);
            Animator.RegisterAnimation(AnimReload, false);

            Animator.RegisterStateOnStart(AnimDraw, WeaponState.Drawing);
            Animator.RegisterStateOnStart(AnimReady, WeaponState.Idle);
            Animator.RegisterStateOnStart(AnimReload, WeaponState.Reloading);
        }

        public override void OnReload()
        {
            Animator.Play(AnimReload);
            Animator.Enqueue(AnimReady);
        }
        public override void OnEquipped()
        {
            Animator.Play(AnimDraw);
            Animator.Enqueue(AnimReady);
        }
        public override void OnHolstered()
        {
            Animator.UnregisterSlot("weapon");
            Model.ClearSlot("weapon");
        }

        private class RpgFireMode : BaseFireMode
        {
            private readonly AmmoContainer container;
            protected override AmmoContainer Container => container;
            public override float FireRate => 0.4f;

            public RpgFireMode(BaseWeapon weapon) : base(weapon)
            {
                container = new AmmoContainer(AmmoType.Grenade);
            }

            public override bool OnFire()
            {
                var projectile = new RocketEntity();
                projectile.Position = weapon.Position;
                projectile.Rotation = Quaternion.CreateFromRotationMatrix(Matrix.CreateWorld(Vector3.Zero, weapon.Forward, weapon.Up));
                projectile.Scale = Vector3.One;
                projectile.Owner = weapon.Owner;
                EntityManager.SpawnEntity(projectile);

                weapon.MuzzleFlash(weapon.Position);

                SoundScriptManager.PlaySound("Weapon.RPG.FireShot", weapon.Position, is3DOverride: weapon.Owner is not Player);

                weapon.Animator.Play(AnimFire);
                return true;
            }
        }
    }
}