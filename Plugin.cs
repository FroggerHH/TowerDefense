using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using ItemManager;
using PieceManager;
using ServerSync;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace TowerDefense
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        #region values

        internal const string ModName = "TowerDefense", ModVersion = "1.0.3", ModGUID = "com.Frogger." + ModName;
        private static readonly Harmony harmony = new(ModGUID);
        public static Plugin _self;
        internal BuildPiece piece;
        internal Item wand;
        internal Material lineRendererMaterial;

        #endregion

        #region ConfigSettings

        static string ConfigFileName = "com.Frogger.TowerDefense.cfg";
        DateTime LastConfigChange;

        public static readonly ConfigSync configSync = new(ModName)
            { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };

        public static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = _self.Config.Bind(group, name, value, description);
            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }

        void SetCfgValue<T>(Action<T> setter, ConfigEntry<T> config)
        {
            setter(config.Value);
            config.SettingChanged += (_, _) => setter(config.Value);
        }

        public enum Toggle
        {
            On = 1,
            Off = 0
        }

        #endregion

        #region values

        internal static ConfigEntry<LineShowMode> lineShowModeConfig;

        internal static LineShowMode lineShowMode;

        //internal static ConfigEntry<Color> lineColorConfig;
        //internal static Color lineColor;
        internal static ConfigEntry<float> minDistanceBetweenPointsConfig;
        internal static float minDistanceBetweenPoints;
        internal static ConfigEntry<float> upModifierConfig;
        internal static float upModifier;
        internal static ConfigEntry<KeyCode> undoKeyConfig;
        internal static KeyCode undoKey;
        internal static ConfigEntry<string> onDestroyMessageConfig;
        internal static string onDestroyMessage;
        internal static ConfigEntry<bool> noLootConfig;
        internal static bool noLoot;

        #endregion

        private void Awake()
        {
            _self = this;

            #region config

            Config.SaveOnConfigSet = false;

            lineShowModeConfig = config("General", "Line Show Mode", LineShowMode.Admin_WhenWandInHands,
                "0-EveryOne\n1-Admin\n2-Nobody\n3-Admin_WhenWandInHands\n4-EveryOne_WhenWandInHands");
            //lineColorConfig = config("General", "Line Color", Color.green, "");
            minDistanceBetweenPointsConfig = config("General", "Min Distance Between Points", 3.85f, "");
            upModifierConfig = config("General", "UpModifier", 0.6f, "");
            undoKeyConfig = config("General", "Undo Key", KeyCode.U, "Works only when wand is equipped");
            onDestroyMessageConfig = config("General", "onDestroy Cristal Message", "You lost bitch", "");
            noLootConfig = config("General", "Should path monsters dont drop loot when they die", false, "");

            SetupWatcherOnConfigFile();
            Config.ConfigReloaded += (_, _) => { UpdateConfiguration(); };
            Config.SaveOnConfigSet = true;
            Config.Save();

            #endregion

            AssetBundle bundle = PrefabManager.RegisterAssetBundle("towerdefense");

            #region wand

            wand = new(bundle, "JF_WayPointsWand");
            wand.Name
                .English("WayPoints Wand");
            wand.Description
                .English("")
                .Russian("");
            wand.RequiredItems.Add("SwordCheat", 1);
            wand.Crafting.Add(ItemManager.CraftingTable.Workbench, 10);
            MaterialReplacer.RegisterGameObjectForShaderSwap(wand.Prefab, MaterialReplacer.ShaderType.UseUnityShader);

            #endregion

            #region DestroyMe

            piece = new(bundle, "JF_DestroyMe");
            piece.Name
                .English("Destroy Me");
            piece.Description
                .English("Break me completely, sweet monster")
                .Russian("Сломай меня полностью, сладкий монстер");
            piece.Crafting.Set(PieceManager.CraftingTable.Workbench);
            piece.Category.Add(PieceManager.BuildPieceCategory.Misc);
            piece.SpecialProperties.AdminOnly = true;
            piece.RequiredItems.Add("SwordCheat", 1, false);
            MaterialReplacer.RegisterGameObjectForShaderSwap(piece.Prefab, MaterialReplacer.ShaderType.UseUnityShader);

            #endregion

            lineRendererMaterial = bundle.LoadAsset<Material>("line");

            harmony.PatchAll();
        }

        #region tools

        public static void Debug(string msg)
        {
            _self.DebugPrivate(msg);
        }

        private void DebugPrivate(string msg)
        {
            Logger.LogInfo(msg);
        }

        public void DebugError(string msg)
        {
            Logger.LogError($"{msg} Write to the developer and moderator if this happens often.");
        }

        public static Collider Nearest(GameObject to, List<Collider> list)
        {
            Collider current = null;
            float oldDistance = 9999;
            foreach (Collider o in list)
            {
                if (!o) continue;
                float dist = Vector3.Distance(to.transform.position, o.transform.position);
                if (dist < oldDistance && dist <= 40)
                {
                    current = o;
                    oldDistance = dist;
                }
            }

            return current;
        }

        #endregion

        #region Config

        public void SetupWatcherOnConfigFile()
        {
            FileSystemWatcher fileSystemWatcherOnConfig = new(Paths.ConfigPath, ConfigFileName);
            fileSystemWatcherOnConfig.Changed += ConfigChanged;
            fileSystemWatcherOnConfig.IncludeSubdirectories = true;
            fileSystemWatcherOnConfig.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            fileSystemWatcherOnConfig.EnableRaisingEvents = true;
        }

        private void ConfigChanged(object sender, FileSystemEventArgs e)
        {
            if ((DateTime.Now - LastConfigChange).TotalSeconds <= 2.0)
            {
                return;
            }

            LastConfigChange = DateTime.Now;
            try
            {
                Config.Reload();
                Debug("Reloading Config...");
            }
            catch
            {
                DebugError("Can't reload Config");
            }
        }

        private void UpdateConfiguration()
        {
            lineShowMode = lineShowModeConfig.Value;
            //lineColor = lineColorConfig.Value;
            minDistanceBetweenPoints = minDistanceBetweenPointsConfig.Value;
            upModifier = upModifierConfig.Value;
            undoKey = undoKeyConfig.Value;
            onDestroyMessage = onDestroyMessageConfig.Value;
            noLoot = noLootConfig.Value;
            WayPointsSys.UpdateLines();
            Debug("Configuration Received");
        }

        #endregion
    }
}