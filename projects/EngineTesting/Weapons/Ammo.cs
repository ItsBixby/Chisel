using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EngineTesting.Weapons;

public enum AmmoType
{
    None = 0,
    NineMM,
    Shells,
    Grenade
}
public record AmmoProperties(int MagazineSize);

public static class AmmoTypeManager
{
    private static FrozenDictionary<AmmoType, AmmoProperties> ammoProps;

    static AmmoTypeManager()
    {
        ammoProps = new Dictionary<AmmoType, AmmoProperties>
        {
            { AmmoType.None, new AmmoProperties(0) },
            { AmmoType.NineMM, new AmmoProperties(40) },
            { AmmoType.Shells, new AmmoProperties(8) },
            { AmmoType.Grenade, new AmmoProperties(1) },
        }.ToFrozenDictionary(); 
    }

    public static AmmoProperties GetProperties(AmmoType type) => ammoProps[type];
}
public class AmmoContainer
{
    public AmmoType AmmoType { get; }
    public int MagazineSize => AmmoTypeManager.GetProperties(AmmoType).MagazineSize;
    public int CurrentAmmo { get; private set; }

    [Newtonsoft.Json.JsonIgnore]
    public WeaponInventory Inventory { get; set; }
    public AmmoContainer(AmmoType type)
    {
        AmmoType = type;
        CurrentAmmo = MagazineSize;
    }

    public bool HasAmmo(int amount = 1) => AmmoType == AmmoType.None || CurrentAmmo >= amount;

    public bool TryConsume(int amount = 1)
    {
        if (AmmoType == AmmoType.None) return true;
        if (CurrentAmmo < amount) return false;
        CurrentAmmo -= amount;
        return true;
    }

    public bool NeedsReload => AmmoType != AmmoType.None && CurrentAmmo < MagazineSize;

    public bool Reload()
    {
        if (AmmoType == AmmoType.None) return false;
        int needed = MagazineSize - CurrentAmmo;
        if (needed <= 0) return false;

        // No inventory = infinite ammo
        int taken = Inventory?.TakeAmmo(AmmoType, needed) ?? needed;
        CurrentAmmo += taken;
        return taken > 0;
    }
    public void SetCurrentAmmo(int a) => CurrentAmmo = a;   
}
