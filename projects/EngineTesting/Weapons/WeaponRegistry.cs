using Engine;
using Engine.Console;
using System;
using System.Collections.Generic;
using System.Linq;

namespace EngineTesting.Weapons;
public static class WeaponRegistry
{
    private static Dictionary<string, (Type weapon, int slot, int slotRow, IReadOnlyDictionary<AmmoType, int> pickupAmmo)> registeredWeaponTypes = new();
    private static Dictionary<Type, string> weaponBackwards = new();

    public static string GetNameForWeapon(Type type) => weaponBackwards[type];

    public static void RegisterWeapon(string name, Type weapon, int slot, int slotRow, params (AmmoType type, int amount)[] pickupAmmo)
    {
        if (registeredWeaponTypes.ContainsKey(name))
        {
            Logger.AppendError($"Cannot have more than one weapon with the name {name}!");
            return;
        }
        weaponBackwards.Add(weapon, name);
        registeredWeaponTypes.Add(name, (weapon, slot, slotRow, pickupAmmo.ToDictionary(p => p.type, p => p.amount)));
    }

    public static (Type weapon, int slot, int slotRow, IReadOnlyDictionary<AmmoType, int> pickupAmmo)? GetWeaponFromName(string name)
    {
        if (registeredWeaponTypes.TryGetValue(name, out var res)) return res;
        return null;
    }

    public static string[] GetAllWeaponNames()
    {
        return registeredWeaponTypes.Keys.ToArray();
    }

    public static void AcquireWeapon(WeaponInventory inventory, string name)
    {
        var res = GetWeaponFromName(name);
        if (!res.HasValue) return;

        foreach (var (type, amount) in res.Value.pickupAmmo)
            inventory.AddAmmo(type, amount);

        if (inventory.HasWeaponAt(res.Value.slot, res.Value.slotRow)) return;

        var weaponInstance = (BaseWeapon)Activator.CreateInstance(res.Value.weapon);
        inventory.AssignWeaponToSlot(weaponInstance, res.Value.slot, res.Value.slotRow);
    }
}