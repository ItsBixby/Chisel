using Engine;
using Engine.Entities.LogicEntities;
using Engine.Rendering;
using Engine.SaveSystem;
using Engine.Sound;
using Engine.UI;
using Engine.Utils;
using Engine.Utils.Settings;
using EngineTesting.Entities;
using EngineTesting.Entities.Npcs;
using EngineTesting.NPC;
using EngineTesting.NPC.Core;
using EngineTesting.Rendering;
using EngineTesting.Utilities;
using EngineTesting.Utilities.Bullet;
using EngineTesting.Utilities.Player;
using EngineTesting.Weapons;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using Silk.NET.OpenAL;
using System;
using System.Collections.Generic;
using System.Linq;

public enum GameDamageTypes
{
    Bullet,
    Explosive,
}
public class GameEngine : MainEngine
{
    public static CVarBool AIDebug = new CVarBool("ai_debug", false);
    public static CVarBool AIDebugNearest = new CVarBool("ai_debug_nearest", false);
    public static CVarBool AINoTarget = new CVarBool("ai_notarget", false);
    public static CVarBool AIDisable = new CVarBool("ai_disable", false);
    public static BulletTracerSystem Tracers { get; private set; }
    public static bool CameraIsUnderwater;
    private const float WaterTransitionDuration = 0.02f;
    private float underwaterAmount;
    private float totalSeconds;

    public static AutoExposure Exposure;
    public static Bloom Bloom;
    bool isActivelyPlaying => IsMapLoaded;

    private float autosaveTimer = 0;

    public GameEngine() : base()
    {
        GameSettings.GameName = "Bark A1";
        Content.RootDirectory = "Content";
        IsMouseVisible = false;
        Instance = this;

        PlayerInput.InitializeInputs();
    }

    protected override void Initialize()
    {
        DefaultCursorLockState = true;
        Window.AllowUserResizing = false;
        Window.Title = "Game";

        GameSettings.Settings.Add("mouse_sensitivity", 1f);

        // The tab can also be an arbitrary string, but you have to make sure
        // to register the tab itself first if so. The tabs in the enum are
        // registered by default :)
        GameSettings.RegisterOption(OptionsTab.Gameplay, new SliderOption
        {
            OptionLabel = "Mouse Sensitivity",
            GameOption = "mouse_sensitivity",
            Min = 1,
            Max = 100,
            SmallChange = 1,
            GetCurrentValue = () => Convert.ToSingle(GameSettings.Settings["mouse_sensitivity"]) * 50.0f,
            OnChanged = val =>
            {
                GameSettings.Settings["mouse_sensitivity"] = (float)val / 50.0f;
                BasePlayerController.MouseSensitivity = Convert.ToSingle(GameSettings.Settings["mouse_sensitivity"]);
            }
        });

        base.Initialize();
    }

    protected override void LoadSetting(string settingName, object value)
    {
        base.LoadSetting(settingName, value);
        switch(settingName)
        {
            case "mouse_sensitivity":
                BasePlayerController.MouseSensitivity = Convert.ToSingle(value);
                break;
        }
    }
    protected override void LoadContent()
    {
        PostProcessingShader = ShaderBuilder.BuildContentShader(GraphicsDevice, "PostProcessing");

        var bulletHole = Content.Load<Texture2D>($"{FullPath}/Textures/particles/bulletImpact");
        var bloodSpray = Content.Load<Texture2D>($"{FullPath}/Textures/particles/bloodspray");
        var spark = Content.Load<Texture2D>($"{FullPath}/Textures/spark");

        BulletHitEffects.BulletHoleTex = bulletHole;
        BulletHitEffects.BloodSprayTex = bloodSpray;

        Tracers = new BulletTracerSystem(GraphicsDevice, spark);

        Exposure = new AutoExposure();
        Bloom = new Bloom();
        Window.ClientSizeChanged += (s, a) =>
        {
            Exposure.Resize(RenderEngine.ScreenRenderTexture.Width, RenderEngine.ScreenRenderTexture.Height);
            Bloom.Resize(RenderEngine.ScreenRenderTexture.Width, RenderEngine.ScreenRenderTexture.Height);
        };

        Skybox.SetSkyTexture(Skybox.LoadSkybox(Content,GraphicsDevice, "Textures/Sky/skyDay"));

        base.LoadContent();

        InitializeWeapons();

        var args = Environment.GetCommandLineArgs();
        if (args != null)
        {
            for (int i = 0; i < args.Length; i++)
            {
                // regular command execution. Map is a bit different because of the way it's passed from RW2
                if (args[i].StartsWith('+'))
                {
                    var commandParts = new List<string> { args[i].Substring(1) };
                    int j = i + 1;
                    while (j < args.Length && !args[j].StartsWith('+') && !args[j].StartsWith('-'))
                    {
                        commandParts.Add(args[j]);
                        j++;
                    }

                    Console.Execute(string.Join(' ', commandParts));
                    i = j - 1;
                    continue;
                }

                if (!args[i].StartsWith('-')) continue;
                if (args[i] == "-map") { LoadMap(args[i + 1]); IsPaused = false; }
            }
        }

        // Prefetch all common particles
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/bloodMist.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/bloodSpray.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/bulletSpark.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/concreteImpact.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/dustScuff.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/flame.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/smokeImpact.crp");
        ParticleManager.PrefetchParticleSystem($"{GameEngine.FullPath}/Particles/explosion.crp");
    }
    void InitializeWeapons()
    {
        WeaponRegistry.RegisterWeapon("w_fists",   typeof(WeaponFists),   0, 0);
        WeaponRegistry.RegisterWeapon("w_smg",     typeof(WeaponSMG),     1, 0, (AmmoType.NineMM, 170));
        WeaponRegistry.RegisterWeapon("w_shotgun", typeof(WeaponShotgun), 2, 0, (AmmoType.Shells,  80));
        WeaponRegistry.RegisterWeapon("w_rpg",     typeof(WeaponRPG),     3, 0, (AmmoType.Grenade,  8));
    }

    protected override void Update(GameTime gameTime)
    {
        CameraIsUnderwater = false;

        DefaultCursorLockState = isActivelyPlaying;
        PauseWhenMenusOpen = isActivelyPlaying;

        if(!isActivelyPlaying)
        {
            UnlockMouse();
        }
        bool showMenu = (IsPaused || !isActivelyPlaying);
        if (showMenu && !GumUtils.IsScreenActive("Menu"))
        {
            ShowMenu();
        }
        if (!showMenu && GumUtils.IsScreenActive("Menu"))
        {
            GumUtils.RemoveScreen("Menu");
        }

        autosaveTimer -= PreviousFrameDelta;

        if(autosaveTimer <= 0 && isActivelyPlaying)
        {
            SaveManager.SaveGame(SaveManager.SaveType.Auto);
            autosaveTimer = 60*5;
        }

        ExplosionManager.UpdateExplosions();

        base.Update(gameTime);

        if (KeyboardManager.HasBeenPressed(Keys.NumPad0) && !IsPaused)
        {
            var ragdoll = new GruntEnemy();
            ragdoll.Position = CameraPosition + CameraForward * 3 + Vector3.Up * 2;
            ragdoll.Scale = Vector3.One;
            EntityManager.SpawnEntity(ragdoll);
        }
        if (KeyboardManager.HasBeenPressed(Keys.NumPad1) && !IsPaused)
        {
            var ragdoll = new CompanionNpc();
            ragdoll.Position = CameraPosition + CameraForward * 3 + Vector3.Up * 2;
            ragdoll.Scale = Vector3.One;
            EntityManager.SpawnEntity(ragdoll);
        }

        float target = CameraIsUnderwater ? 1f : 0f;
        underwaterAmount = MathHelper.Clamp(
            underwaterAmount + Math.Sign(target - underwaterAmount) * (PreviousFrameDelta / WaterTransitionDuration),
            0f, 1f);

        totalSeconds += PreviousFrameDelta;

        PostProcessingShader.Param("UnderwaterAmount").SetValue(underwaterAmount);
        PostProcessingShader.Param("time").SetValue(totalSeconds);

        DamageBuffer.Flush();
    }

    public override void OnMapLoaded()
    {
        // Autosaves should probably be a bit smarter than that
        autosaveTimer = 1;

        ExplosionManager.ClearExplosions();

        base.OnMapLoaded();
    }

    protected override void Draw(GameTime gameTime)
    {
        PrepWorldRender();
        base.Draw(gameTime);
        Tracers.Draw(GraphicsDevice);

        Exposure.Update(PreviousFrameDelta);
        Bloom.Update(RenderEngine.ScreenRenderTexture);

        PostProcessingShader.Param("ExposureTexture").SetValue(Exposure.ExposureTexture);
        PostProcessingShader.Param("KeyValue").SetValue(Exposure.KeyValue);
        PostProcessingShader.Param("MinExposure").SetValue(Exposure.MinExposure);
        PostProcessingShader.Param("MaxExposure").SetValue(Exposure.MaxExposure);
        PostProcessingShader.Param("BloomTexture").SetValue(Bloom.BloomTexture);
        PostProcessingShader.Param("BloomIntensity").SetValue(Bloom.Intensity);

        EndWorldRender();
    }

    private void ShowMenu()
    {
        var menu = GumUtils.ShowScreen("Menu");

        var saveButton = ((InteractiveGue)menu.GetChildByNameRecursively("SaveButton"));

        var text = (TextRuntime)saveButton.GetChildByName("TextInstance");

        text.Text = isActivelyPlaying ? "SAVE GAME" : "NEW GAME";

        saveButton.Click += (s,a) =>
        {
            if(!isActivelyPlaying)
            {
                // Start a new game
                SaveManager.ClearSessionMapStates();
                LoadMap($"{FullPath}/Maps/demo_01");
            }
            else
            {
                Console.Execute("save_wnd");
            }
        };
        ((InteractiveGue)menu.GetChildByNameRecursively("LoadButton")).Click += (s, a) =>
        {
            Console.Execute("load_wnd");
        };
        ((InteractiveGue)menu.GetChildByNameRecursively("OptionsButton")).Click += (s, a) =>
        {
            Console.Execute("opt_wnd");
        };
        ((InteractiveGue)menu.GetChildByNameRecursively("QuitButton")).Click += (s, a) =>
        {
            var window = CGUI.Window("Confirmation",200,150)
                            .Label("Are you sure?")
                            .BottomBar(("Cancel",(from)=>from.Close()),("Confirm",(from)=> Console.Execute("quit")))
                            .Build();
        };
    }
}