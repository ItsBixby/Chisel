using Engine;
using Engine.Physics;
using FPSTemplate.Projectiles;
using FPSTemplate.Weapons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace FPSTemplate.NPC.Core;

public partial class NpcController
{
    private readonly List<BaseWeapon> weapons = new();
    private BaseWeapon currentWeapon;
    private WeaponAnimator weaponAnimator;

    public BaseWeapon CurrentWeapon => currentWeapon;
    public IReadOnlyList<BaseWeapon> Inventory => weapons;

    public void AddWeapon(BaseWeapon weapon)
    {
        weapons.Add(weapon);
        if (currentWeapon == null && weaponAnimator != null)
            EquipWeapon(weapon);
    }

    public void EquipWeapon(int index)
    {
        if ((uint)index < (uint)weapons.Count)
            EquipWeapon(weapons[index]);
    }

    public void EquipWeapon(BaseWeapon weapon)
    {
        currentWeapon?.OnHolstered();
        currentWeapon = weapon;
        currentWeapon.Model = model;
        currentWeapon.Owner = entity;
        currentWeapon.HeldByNPC = true;
        currentWeapon.SetAnimator(weaponAnimator!);
        currentWeapon.OnEquipped();
    }

    public void FireAt(WorldEntity target)
    {
        if (!canFire || currentWeapon == null) return;
        var lookMat = Matrix.Invert(Matrix.CreateLookAt(entity.Position, target.OrientedBounds.Center, Vector3.Up));
        currentWeapon.UpdateSpecial(lookMat.Forward, lookMat.Right, lookMat.Up, entity.Position);
        currentWeapon.GetFireMode(FireMode.Primary)?.TryFireImmediate();
    }

    public void FireAt(Vector3 worldPos)
    {
        if (!canFire || currentWeapon == null) return;
        var lookMat = Matrix.Invert(Matrix.CreateLookAt(entity.Position, worldPos, Vector3.Up));
        currentWeapon.UpdateSpecial(lookMat.Forward, lookMat.Right, lookMat.Up, entity.Position);
        currentWeapon.GetFireMode(FireMode.Primary)?.TryFireImmediate();
    }

    public bool ThrowGrenade(Vector3 targetPos)
    {
        Vector3 origin = entity.Position;
        Vector3 delta = targetPos - origin;

        float horizontalDist = new Vector2(delta.X, delta.Z).Length();
        float flightTime = MathF.Max(0.8f, horizontalDist / 50f);

        Vector3 horizontal = new Vector3(delta.X, 0f, delta.Z) / flightTime;
        float vy = (delta.Y + 0.5f * PhysicsEngine.GravityPerSecond * flightTime * flightTime) / flightTime;
        Vector3 launchVel = horizontal + Vector3.Up * vy;

        if (launchVel.Length() > 120f) return false;

        EntityManager.SpawnEntity(new GrenadeEntity
        {
            Position = origin,
            Velocity = launchVel,
            Rotation = Quaternion.CreateFromRotationMatrix(
                           Matrix.CreateWorld(Vector3.Zero, Vector3.Normalize(launchVel), Vector3.Up)),
            Scale = Vector3.One,
        });
        return true;
    }

    private void SpawnCombat()
    {
        weaponAnimator = new WeaponAnimator(model,"weapon");
        if (currentWeapon == null && weaponAnimator != null)
            EquipWeapon(0);
    }

    private void TickCombat()
    {
        currentWeapon?.Update();

        var primary = currentWeapon?.GetFireMode(FireMode.Primary);
        if (primary != null && primary.CurrentAmmo == 0)
        {
            currentWeapon.ReloadFireMode(FireMode.Primary);
        }
    }
}