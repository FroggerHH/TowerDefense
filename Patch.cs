using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using static Player;
using static TowerDefense.Plugin;


namespace TowerDefense
{
    [HarmonyPatch]
    internal class Patch
    {
        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.SetPatrolPoint), new Type[0]), HarmonyPostfix]
        internal static void MonsterAISetPatrolPoint(BaseAI __instance)
        {
            WayPointsSys.RegisterMonster(__instance);
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.OnDeath)), HarmonyPostfix]
        internal static void MonsterAIOnDeath(BaseAI __instance)
        {
            if (__instance is not MonsterAI monsterAI) return;
            WayPointsSys.MonsterPathDatas.Remove(monsterAI);
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.SetPatrolPoint), new Type[1] { typeof(Vector3) }), HarmonyPostfix]
        internal static void MonsterAISetPatrolPoint1(BaseAI __instance)
        {
            WayPointsSys.RegisterMonster(__instance);
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.Start)), HarmonyPostfix]
        internal static void MonsterAIAwake(MonsterAI __instance)
        {
            if (!(__instance && __instance.m_character)) return;

            __instance.GetPatrolPoint(out __instance.m_patrolPoint);
            WayPointsSys.RegisterMonster(__instance);
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.IdleMovement)), HarmonyPrefix]
        internal static bool MonsterAiIdleMovement(BaseAI __instance)
        {
            if (!__instance || !__instance.m_character || __instance is not MonsterAI monsterAI) return true;
            var isPathMonster = WayPointsSys.IsPathMonster(monsterAI, out MonsterPathData pathData);
            if (isPathMonster && !pathData.OnPathEnd()) return false;

            return true;
        }

        [HarmonyPatch(typeof(BaseAI), nameof(BaseAI.GetPatrolPoint)), HarmonyPostfix]
        internal static void MonsterGetPatrolPoint(BaseAI __instance, ref bool __result)
        {
            if (!__instance || !__instance.m_character || __instance is not MonsterAI monsterAI) return;
            var isPathMonster = WayPointsSys.IsPathMonster(monsterAI, out MonsterPathData _);
            if (!isPathMonster) return;

            __result = false;
            monsterAI.m_patrol = false;
            monsterAI.m_spawnPoint = monsterAI.transform.position;
            return;
        }

        [HarmonyPatch(typeof(MonsterAI), nameof(MonsterAI.UpdateAI)), HarmonyPrefix]
        internal static void MonsterAIUpdateAI(MonsterAI __instance, float dt)
        {
            if (!__instance || !__instance.m_character) return;

            if (WayPointsSys.MonsterPathDatas.ContainsKey(__instance))
            {
                WayPointsSys.RefreshAllMonstersDic();
                WayPointsSys.MoveMonsterAlongPath(__instance, dt);
            }
        }

        [HarmonyPatch(typeof(Attack), nameof(Attack.Start)), HarmonyPrefix]
        private static bool PreventAnimation(Humanoid character, ItemDrop.ItemData weapon)
        {
            if (SceneManager.GetActiveScene().name != "main") return true;
            if (character == null) return true;
            if (weapon == null || weapon.m_shared.m_name != "$item_JF_WayPointsWand") return true;

            return false;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.Update)), HarmonyPrefix]
        private static void PlayerUpdate(Player __instance)
        {
            if (SceneManager.GetActiveScene().name != "main") return;
            if (__instance == null || __instance != m_localPlayer) return;
            ItemDrop.ItemData weapon = m_localPlayer.GetCurrentWeapon();
            if (weapon == null || weapon?.m_shared.m_name != "$item_JF_WayPointsWand") return;


            if (Input.GetMouseButtonDown(0) && !InventoryGui.IsVisible() && !Game.IsPaused())
            {
                WayPointsSys.UseWan_LMB();
            }

            if (Input.GetMouseButtonDown(1) && !InventoryGui.IsVisible() && !Game.IsPaused())
            {
                WayPointsSys.UseWan_RMB();
            }

            if (Input.GetKeyDown(undoKey))
                WayPointsSys.RemoveLastWayPoint();
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem)), HarmonyPostfix]
        private static void PlayerEquipItem(Humanoid __instance)
        {
            if (SceneManager.GetActiveScene().name != "main") return;
            if (__instance is not Player player) return;
            if (player == null || player != m_localPlayer) return;

            WayPointsSys.UpdateLines();
        }

        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.UnequipItem)), HarmonyPostfix]
        private static void PlayerUnEquipItem(Humanoid __instance)
        {
            if (SceneManager.GetActiveScene().name != "main") return;
            if (__instance is not Player player) return;
            if (player == null || player != m_localPlayer) return;

            WayPointsSys.UpdateLines();
        }

        [HarmonyPatch(typeof(SpawnArea), nameof(SpawnArea.Awake)), HarmonyPostfix]
        private static void SpawnAreaAwake(SpawnArea __instance)
        {
            WayPointsSys.RefreshAllSpawnersList();
            if (!WayPointsSys.AllSpawners.Contains(__instance)) WayPointsSys.AllSpawners.Add(__instance);
            List<Vector3> vector3s = WayPointsSys.LoadPath(__instance);
            if (vector3s != null && vector3s.Count > 0)
            {
                WayPointsSys.CreateLineRenderer(__instance, vector3s);
                WayPointsSys.AllSpawners.Add(__instance);
                __instance.m_spawnRadius = 0;
                __instance.GetComponentsInChildren<Collider>().ToList().ForEach(x => x.enabled = false);
                __instance.m_setPatrolSpawnPoint = true;
            }
        }

        [HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.Awake)), HarmonyPostfix]
        private static void RegisterRPC(ZNetScene __instance)
        {
            if (SceneManager.GetActiveScene().name != "main") return;

            ZRoutedRpc.instance.Register(nameof(WayPointsSys.SyncPathsWithOtherPlayers),
                new Action<long>(WayPointsSys.RPC_SyncPathsWithOtherPlayers));
        }
    }
}