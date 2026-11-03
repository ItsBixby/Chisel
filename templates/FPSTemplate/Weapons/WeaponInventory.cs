using Engine;
using Engine.Console;
using Gum.Forms.Controls;
using Gum.Forms.DefaultFromFileVisuals;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using MonoGameGum.GueDeriving;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FPSTemplate.Weapons;
public class WeaponInventory
{
    public Action<BaseWeapon, BaseWeapon> OnWeaponChanged;

    private Dictionary<(int c, int r), BaseWeapon> currentWeapons;
    private Dictionary<AmmoType, int> ammoPools;
    private int[] numPerColumn;
    private int slots;

    private WorldEntity owner;

    private int activeSlot, activeRow;

    private GraphicalUiElement gumRoot;
    private ContainerRuntime weaponsUIContainer;
    private DefaultFromFileStackPanelRuntime weaponsUIColumnStack;
    private List<ColoredRectangleRuntime> allBackground;
    private List<SpriteRuntime> allSprites;

    private BaseWeapon previousWeapon;
    private ColoredRectangleRuntime curFlashing;
    private float flashTime;

    private float fadeTime = -1;

    public WeaponInventory(WorldEntity owner, GraphicalUiElement gumRoot, int slots = 9)
    {
        currentWeapons = new();
        ammoPools = new();
        numPerColumn = new int[slots];
        allBackground = new List<ColoredRectangleRuntime>();
        allSprites = new List<SpriteRuntime>();
        this.slots = slots;
        this.owner = owner;
        this.gumRoot = gumRoot;

        weaponsUIContainer = (ContainerRuntime)gumRoot.GetGraphicalUiElementByName("Weapons");
        weaponsUIColumnStack = (DefaultFromFileStackPanelRuntime)weaponsUIContainer.GetChildByName("ColumnStack");

        for (int i = 0; i < slots; i++)
        {
            var container = new ContainerRuntime();
            container.Width = (1f / slots) * 100;
            container.Height = 75;
            container.HeightUnits = Gum.DataTypes.DimensionUnitType.PercentageOfOtherDimension;
            container.WidthUnits = Gum.DataTypes.DimensionUnitType.PercentageOfParent;
            container.Visible = true;
            container.Name = $"S{i}";
            weaponsUIColumnStack.AddChild(container);
            ConstructRowItem(GetSlotItem(i), 0);
        }
    }

    public (int, int) GetSlots()
    {
        return (activeSlot, activeRow);
    }
    public void SetSlots((int, int) s)
    {
        (activeSlot, activeRow) = s;
    }

    void ConstructRowItem(ContainerRuntime parent, int row)
    {
        var container = new ContainerRuntime();
        container.YUnits = Gum.Converters.GeneralUnitType.Percentage;
        container.Y = 100 * row;
        container.Width = 100;
        container.Height = 100;
        container.HeightUnits = Gum.DataTypes.DimensionUnitType.PercentageOfOtherDimension;
        container.WidthUnits = Gum.DataTypes.DimensionUnitType.PercentageOfParent;
        container.Visible = true;
        container.Name = $"R{row}";

        var coloredBG = new ColoredRectangleRuntime();
        coloredBG.WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent;
        coloredBG.Width = -10;
        coloredBG.HeightUnits = Gum.DataTypes.DimensionUnitType.PercentageOfOtherDimension;
        coloredBG.Height = 75;
        coloredBG.Color = new Microsoft.Xna.Framework.Color(0, 0, 0, 128);

        allBackground.Add(coloredBG);

        var texture = new SpriteRuntime();
        texture.WidthUnits = Gum.DataTypes.DimensionUnitType.RelativeToParent;
        texture.HeightUnits = Gum.DataTypes.DimensionUnitType.PercentageOfParent;
        texture.Width = -10;
        texture.Height = 75;
        texture.TextureAddress = Gum.Managers.TextureAddress.EntireTexture;
        texture.Visible = false;
        texture.Name = "sprite";
        texture.Visible = false;

        allSprites.Add(texture);

        parent.AddChild(container);
        container.AddChild(coloredBG);
        container.AddChild(texture);
    }

    ContainerRuntime GetSlotItem(int slot)
    {
        return (ContainerRuntime)weaponsUIColumnStack.GetChildByName($"S{slot}");
    }
    ContainerRuntime GetRowItem(ContainerRuntime parent, int row)
    {
        return (ContainerRuntime)parent.GetChildByName($"R{row}");
    }

    void SetWeaponTexture(string name, int slot, int row)
    {
        var item = GetRowItem(GetSlotItem(slot), row);

        var spr = (SpriteRuntime)item.GetChildByName("sprite");
        spr.Texture = AssetManager.LoadAsset<Texture2D>($"Textures/ui/weapons/{name}", $"{name}_hud");

        spr.Visible = true;
    }

    /// <summary>
    /// Adds a weapon to a slot.
    /// </summary>
    /// <param name="weapon"></param>
    /// <param name="slot">The column to put the weapon under</param>
    /// <param name="row">The row in the column to put the weapon under</param>
    public void AssignWeaponToSlot(BaseWeapon weapon, int slot, int row)
    {
        if (slot < 0 || slot >= slots) return;

        if (row >= numPerColumn[slot])
        {
            for (int i = 1; i <= row; i++)
            {
                ConstructRowItem(GetSlotItem(slot), i);
            }
            numPerColumn[slot] = row + 1;
        }

        if (currentWeapons.ContainsKey((slot, row)))
        {
            Logger.AppendWarn("Cannot assign to an already used slot!");
            return;
        }
        weapon.Owner = owner;
        weapon.Inventory = this;
        SetWeaponTexture(weapon.WeaponUIAssetName, slot, row);
        currentWeapons.Add((slot, row), weapon);
    }

    /// <summary>
    /// Will select a slot, or if already selected, scroll down the rows
    /// </summary>
    /// <param name="slot"></param>
    public void SelectSlot(int slot)
    {
        SelectBestSlot(slot);

        if (activeSlot == -1) return;

        fadeTime = 2.5f;

        if (curFlashing != null)
        {
            curFlashing.Red = 0;
            curFlashing.Green = 0;
            curFlashing.Blue = 0;
        }
        curFlashing = GetRowItem(GetSlotItem(activeSlot), activeRow).GetChildByType(typeof(ColoredRectangleRuntime)) as ColoredRectangleRuntime;
        flashTime = 1;
    }
    void SelectBestSlot(int slot)
    {
        if (slot == -1 && activeSlot != slot)
        {
            activeSlot = slot;
            activeRow = 0;
            return;
        }

        if (activeSlot != slot)
        {
            activeSlot = slot;
            while (numPerColumn[activeSlot] == 0 && activeSlot > 0)
                activeSlot--;
            activeRow = 0;
            return;
        }

        if (fadeTime < -0.5f)
        {
            return;
        }
        IncrementRow(slot);
    }
    void IncrementRow(int slot)
    {
        if (activeSlot == -1) return;

        var oldRow = activeRow;
        activeRow++;

        if (activeRow >= numPerColumn[activeSlot])
        {
            activeRow = 0;
            return;
        }

        //This row is empty, skip ahead
        while (GetCurrentWeapon() == null && activeRow <= numPerColumn[slot])
        {
            activeRow++;
        }

        if (GetCurrentWeapon() == null) activeRow = oldRow;
    }
    void DecrementRow(int slot)
    {
        if (activeSlot == -1) return;

        var oldRow = activeRow;
        activeRow--;

        if (activeRow < 0)
        {
            activeRow = numPerColumn[activeSlot];
        }

        while (GetCurrentWeapon() == null && activeRow >= 0)
        {
            activeRow--;
        }

        if (GetCurrentWeapon() == null) activeRow = oldRow;
    }
    public void ScrollUp()
    {
        if (activeSlot == -1) return;

        int slot = activeSlot;

        if (fadeTime > -0.5f)
        {
            if (activeRow >= numPerColumn[slot] - 1)
            {
                activeSlot++;
                activeRow = 0;
                while (GetCurrentWeapon() == null && activeSlot < slots) activeSlot++;

                if (GetCurrentWeapon() == null) activeSlot = 0;
                while (GetCurrentWeapon() == null && activeSlot < slots) activeSlot++;
            }
            else
            {
                IncrementRow(slot);
            }
        }

        fadeTime = 2.5f;

        if (curFlashing != null)
        {
            curFlashing.Red = 0;
            curFlashing.Green = 0;
            curFlashing.Blue = 0;
        }
        curFlashing = GetRowItem(GetSlotItem(activeSlot), activeRow).GetChildByType(typeof(ColoredRectangleRuntime)) as ColoredRectangleRuntime;
        flashTime = 1;
    }
    public void ScrollDown()
    {
        if (activeSlot == -1) return;

        if (fadeTime > -0.5f)
        {
            if (activeRow <= 0)
            {
                activeSlot--;
                if (activeSlot < 0)
                    activeSlot = slots - 1;

                activeRow = numPerColumn[activeSlot] - 1;
                while (activeRow >= 0 && GetCurrentWeapon() == null)
                {
                    activeRow--;
                }

                while (activeRow < 0 && activeSlot > 0)
                {
                    activeSlot--;
                    activeRow = numPerColumn[activeSlot] - 1;
                    while (activeRow >= 0 && GetCurrentWeapon() == null)
                    {
                        activeRow--;
                    }
                }

                if (activeRow < 0)
                {
                    activeSlot = slots - 1;
                    activeRow = numPerColumn[activeSlot] - 1;
                    while (activeRow >= 0 && GetCurrentWeapon() == null)
                    {
                        activeRow--;
                    }
                }
            }
            else
            {
                DecrementRow(activeSlot);
            }
        }

        fadeTime = 2.5f;
        if (curFlashing != null)
        {
            curFlashing.Red = 0;
            curFlashing.Green = 0;
            curFlashing.Blue = 0;
        }
        curFlashing = GetRowItem(GetSlotItem(activeSlot), activeRow).GetChildByType(typeof(ColoredRectangleRuntime)) as ColoredRectangleRuntime;
        flashTime = 1;
    }
    public void UpdateUI()
    {
        if (fadeTime < -1)
        {
            weaponsUIContainer.Visible = false;
            return;
        }
        fadeTime -= MainEngine.PreviousFrameDelta;
        weaponsUIContainer.Visible = true;

        for (int i = 0; i < allBackground.Count; i++)
        {
            allBackground[i].Alpha = (int)((1 - float.Max(-fadeTime, 0)) * 128);
        }
        for (int i = 0; i < allSprites.Count; i++)
        {
            allSprites[i].Alpha = (int)((1 - float.Max(-fadeTime, 0)) * 255);
        }

        if (flashTime > 0 && curFlashing != null)
        {
            flashTime -= MainEngine.PreviousFrameDelta;
            curFlashing.Red = (int)(flashTime * 140);
            curFlashing.Green = (int)(flashTime * 140);
            curFlashing.Blue = (int)(flashTime * 140);
            curFlashing.Alpha = (int)(flashTime * 40) + 128;
        }
        else if (curFlashing != null)
        {
            curFlashing.Red = 0;
            curFlashing.Green = 0;
            curFlashing.Blue = 0;
        }
    }
    public BaseWeapon GetCurrentWeapon()
    {
        if (activeSlot == -1) { previousWeapon?.ExtinguishMuzzleflash(); previousWeapon?.OnHolstered(); previousWeapon = null; return null; }

        if (!currentWeapons.TryGetValue((activeSlot, activeRow), out var w)) return null;

        if (w != previousWeapon) { previousWeapon?.ExtinguishMuzzleflash(); previousWeapon?.OnHolstered(); OnWeaponChanged(previousWeapon, w); w?.OnEquipped(); }

        previousWeapon = w;

        return w;
    }

    public bool HasWeaponAt(int slot, int row) => currentWeapons.ContainsKey((slot, row));

    public int GetAmmo(AmmoType type)
        => type == AmmoType.None ? 0 : ammoPools.GetValueOrDefault(type);

    public void AddAmmo(AmmoType type, int amount)
    {
        if (type == AmmoType.None || amount <= 0) return;
        ammoPools[type] = GetAmmo(type) + amount;
    }

    public int TakeAmmo(AmmoType type, int amount)
    {
        if (type == AmmoType.None || amount <= 0) return 0;

        int available = GetAmmo(type);
        int taken = Math.Min(available, amount);
        ammoPools[type] = available - taken;
        return taken;
    }

    //Saves
    public class WeaponEntry
    {
        public int Col { get; set; }
        public int Row { get; set; }
        public BaseWeapon Weapon { get; set; }
        public Dictionary<FireMode, int> AmmoSnapshot { get; set; }
    }

    public object Capture() => new
    {
        ActiveSlot = activeSlot,
        ActiveRow = activeRow,
        AmmoPools = ammoPools,
        Weapons = currentWeapons.Select(kvp => new WeaponEntry
        {
            Col = kvp.Key.c,
            Row = kvp.Key.r,
            Weapon = kvp.Value,
            AmmoSnapshot = kvp.Value.AmmoSnapshot
        }).ToList()
    };

    public void Restore(JToken data)
    {
        activeSlot = data["ActiveSlot"].Value<int>();
        activeRow = data["ActiveRow"].Value<int>();
        ammoPools = data["AmmoPools"]?.ToObject<Dictionary<AmmoType, int>>() ?? new();

        currentWeapons.Clear();
        var entries = data["Weapons"].ToObject<List<WeaponEntry>>();
        foreach (var entry in entries)
        {
            entry.Weapon.Owner = owner;
            entry.Weapon.AmmoSnapshot = entry.AmmoSnapshot;
            AssignWeaponToSlot(entry.Weapon, entry.Col, entry.Row);
        }
    }
}