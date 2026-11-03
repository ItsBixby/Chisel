using Engine;
using Engine.Sound;
using FPSTemplate.Entities;
using FPSTemplate.Projectiles;
using Microsoft.Xna.Framework;
using System;

namespace FPSTemplate.Weapons
{
    public class WeaponGrenade : BaseWeapon
    {
        public override float ViewPunch => 0.1f * (Random.Shared.NextSingle() * 0.1f + 0.9f);
        public override float ViewPunchOffset => (Random.Shared.NextSingle() - 0.5f);
        public override string WeaponUIAssetName => "smg";

        protected override void SetupFireModes()
        {
            RegisterFireMode(FireMode.Primary, new GrenadeFireMode(this));
        }

        private class GrenadeFireMode : BaseFireMode
        {
            private readonly AmmoContainer container;
            protected override AmmoContainer Container => container;
            public override float FireRate => 0.4f;

            public GrenadeFireMode(BaseWeapon weapon) : base(weapon)
            {
                container = new AmmoContainer(AmmoType.Grenade);
            }

            public override bool OnFire()
            {
                var projectile = new GrenadeEntity();
                projectile.Position = weapon.Position + weapon.Forward * 0.5f;
                projectile.Rotation = Quaternion.CreateFromRotationMatrix(Matrix.CreateWorld(Vector3.Zero, weapon.Forward, weapon.Up));
                projectile.Scale = Vector3.One;
                projectile.Velocity = weapon.Forward * 20;
                EntityManager.SpawnEntity(projectile);

                weapon.MuzzleFlash(weapon.Position);

                return true;
            }

            protected override void AfterFire() => Container.Reload();
        }
    }
}