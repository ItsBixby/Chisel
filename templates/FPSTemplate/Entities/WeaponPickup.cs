using Engine;
using Engine.Rendering;
using Engine.Utils;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FPSTemplate.Entities;

[EntityDescriptor]
[ExposeEntityProperty("Weapon Code Name", Rockwall.EntityPropertyType.String, "The code-name for the weapon, setup in GameEngine.InitializeWeapons")]
public class WeaponPickup : WorldEntity
{
    public WeaponPickup()
    {
        Controller = new WeaponPickupController();
        Bounds = new BoundingBox(-Vector3.One * 0.25f, Vector3.One * 0.25f);
        IsSimulated = false;
        CollisionID = -2;

        Collision.AddRule(-1, -2, Collision.CollideType.Trigger);
    }
    private class WeaponPickupController : EntityController
    {
        string weaponName;
        SphericalHarmonicsVisualizer vis;
        public override void OnDespawn()
        {
        }
        public override void OnRender(GameTime gameTime)
        {
            vis.DrawSphere(entity.Position);
        }
        public override void OnSpawn()
        {
            vis = new SphericalHarmonicsVisualizer();
            weaponName = (string)(entity.ReadProperty("Weapon Code Name", Rockwall.EntityPropertyType.String) ?? "");
        }
        public override void OnUpdate(GameTime gameTime)
        {

        }
        public override void OnTriggerCollideWithEntity(WorldEntity other)
        {
            if (other.Controller is not BasePlayerController player) return;

            player.GetWeapon(weaponName);
            EntityManager.DespawnEntity(entity);
        }
    }
}
