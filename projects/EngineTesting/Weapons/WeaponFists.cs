using Engine.Sound;
using EngineTesting.Entities;
using EngineTesting.Utilities.Bullet;
using Microsoft.Xna.Framework;
using System;

namespace EngineTesting.Weapons
{
    public class WeaponFists : BaseWeapon
    {
        public override float ViewPunch => punchAmount;
        public override float ViewPunchOffset => (Random.Shared.NextSingle() + 0.5f);
        public override string WeaponUIAssetName => "smg";

        float punchAmount;
        bool punchRight;

        const string AnimDraw = "fists_draw";
        const string AnimReady = "fists_ready";
        const string AnimPunchRight = "fists_hitright";
        const string AnimPunchLeft = "fists_hitleft";

        protected override void SetupFireModes()
        {
            RegisterFireMode(FireMode.Primary, new FistsFireMode(this));
        }

        public override void RegisterAnimations()
        {
            Animator.RegisterAnimation(AnimDraw, false);
            Animator.RegisterAnimation(AnimPunchRight, false);
            Animator.RegisterAnimation(AnimPunchLeft, false);
            Animator.RegisterAnimation(AnimReady, true);
            Animator.RegisterStateOnStart(AnimReady, WeaponState.Idle);
            Animator.RegisterStateOnStart(AnimDraw, WeaponState.Drawing);
        }

        public override void OnEquipped()
        {
            Animator.Play(AnimDraw);
            Animator.Enqueue(AnimReady);
        }

        private class FistsFireMode : BaseFireMode
        {
            private readonly WeaponFists fists;
            protected override AmmoContainer Container => null;
            public override float FireRate => 0.4f;

            public FistsFireMode(WeaponFists weapon) : base(weapon)
            {
                fists = weapon;
            }

            public override bool OnFire()
            {
                fists.punchAmount = 0;
                bool hit = Ballistics.ShootBullet(weapon.Owner, weapon.Position, weapon.Position - weapon.Up * 0.04f,
                    BaseWeapon.GetSpreadDirection(weapon.Forward, weapon.Right, weapon.Up, 0.01f), 2, 3.5f, true);

                if (hit)
                    fists.punchAmount = 0.04f;

                fists.punchRight = !fists.punchRight;
                weapon.Animator.Play(fists.punchRight ? AnimPunchRight : AnimPunchLeft);
                weapon.Animator.Enqueue(AnimReady);

                return hit;
            }
        }
    }
}