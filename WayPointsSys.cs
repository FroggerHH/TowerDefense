using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using static Player;
using static TowerDefense.Plugin;
using static TowerDefense.CONST;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace TowerDefense
{
    internal class WayPointsSys
    {
        internal static HashSet<SpawnArea> AllSpawners = new();
        internal static HashSet<LineRenderer> AllLineRenderers = new();
        internal static Dictionary<MonsterAI, MonsterPathData> MonsterPathDatas = new();

        private static SpawnArea currentSpawner;
        private static HashSet<Vector3> currentPath = new();
        private static LineRenderer currentLineRenderer;
        private static WandMode mode;

        internal static bool UseWan_LMB()
        {
            //Debug($"UseWand LeftMouseButton");
            return mode switch
            {
                WandMode.SpawnerConnectingMode => SelectSpawner(),
                WandMode.WayPointPlacingMode => CreateWayPoint(),
                _ => false
            };
        }

        internal static bool UseWan_RMB()
        {
            //Debug($"UseWand RightMouseButton");

            SwitchWandMode();

            return true;
        }

        private static void SwitchWandMode()
        {
            switch (mode)
            {
                case WandMode.SpawnerConnectingMode:
                    mode = WandMode.WayPointPlacingMode;
                    break;
                case WandMode.WayPointPlacingMode:
                    mode = WandMode.SpawnerConnectingMode;
                    break;
            }

            m_localPlayer.Message(MessageHud.MessageType.TopLeft,
                $"Wand mode has been switched to <color=yellow>{mode}</color>");
        }

        private static bool SelectSpawner()
        {
            RefreshAllMonstersDic();
            SaveSpawnerData();
            if (!FindSpawner(out SpawnArea newSA)) return false;

            LoadPathToCurrent(newSA);
            CreateLineRenderer(newSA, currentPath);
            Heightlight(newSA.gameObject);
            if (!AllSpawners.Contains(newSA)) AllSpawners.Add(currentSpawner);

            currentSpawner = newSA;

            return true;
        }

        private static bool CreateWayPoint()
        {
            if (!currentSpawner || !currentLineRenderer) return false;
            if (currentPath == null)
            {
                currentPath = new();
                return false;
            }

            if (PieceRayTest(out Vector3 point, out Vector3 _))
            {
                if (currentPath.Contains(point)) return false;
                if (currentPath.Count >= 1)
                {
                    float dist = Vector3.Distance(currentPath.Last(), point);
                    if (dist < minDistanceBetweenPoints)
                    {
                        m_localPlayer?.Message(MessageHud.MessageType.TopLeft, "The previous point is too close");
                        return false;
                    }
                }

                point.y += upModifier;
                currentPath.Add(point);
                currentLineRenderer.positionCount++;
                currentLineRenderer.SetPosition(currentLineRenderer.positionCount - 1, point);
            }

            SaveSpawnerData();
            return true;
        }
        
        internal static bool RemoveLastWayPoint()
        {
            Debug("RemoveLastWayPoint");
            m_localPlayer.Message(MessageHud.MessageType.TopLeft, "RemoveLastWayPoint");
            if (!currentSpawner) return false;
            if (currentPath == null) return false;
            if (currentLineRenderer.positionCount <= 1 || currentPath.Count <= 0) return false;

            currentLineRenderer.positionCount--;
            currentPath.Remove(currentPath.Last());
            SaveSpawnerData();

            return true;
        }

        internal static HashSet<Vector3> GetSpawnerPath(SpawnArea spawnArea)
        {
            if (spawnArea == null) throw new ArgumentException("spawnArea = null");

            return LoadPath(spawnArea);
        }

        internal static SpawnArea GetSpawnerOnPosition(Vector3 point)
        {
            RefreshAllSpawnersList();
            if(AllSpawners.Count == 0) return null;
            foreach (SpawnArea item in AllSpawners)
            {
                if(item == null) continue;
                if (Mathf.Approximately((int)item.transform.position.x, (int)point.x) &&
                    Mathf.Approximately((int)item.transform.position.z, (int)point.z)) return item;
            }

            return null;
        }

        internal static bool MoveMonsterAlongPath(MonsterAI monster, float dt)
        {
            return MonsterPathDatas[monster].GoToNode(dt);
        }

        internal static void RegisterMonster(BaseAI __instance)
        {
            if (!(__instance && __instance.m_character && __instance is MonsterAI monsterAI)) return;
            if (MonsterPathDatas.ContainsKey(monsterAI)) return;
            SpawnArea spawnArea = GetSpawnerOnPosition(monsterAI.m_patrolPoint);
            if (spawnArea == null) return;

            MonsterPathDatas.Add(monsterAI, new(monsterAI, spawnArea));
        }

        internal static bool IsPathMonster(MonsterAI monsterAI, out MonsterPathData pathData)
        {
            return MonsterPathDatas.TryGetValue(monsterAI, out pathData);
        }

        private static bool SaveSpawnerData()
        {
            if (currentPath == null || currentSpawner == null) return false;
            HashSet<CustomVector3> customVectorsToSave = new();
            currentPath.ToList().ForEach(x => customVectorsToSave.Add(x.ToCustomVector3()));
            ISerializer serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            string yaml = serializer.Serialize(customVectorsToSave);

            //string jsonToSave = JSON.ToJSON(customVectorsToSave);
            //Debug($"SaveSpawnerData yaml = {yaml}");
            currentSpawner.m_nview.GetZDO().Set(CONST.ZDO_PATH, yaml);
            foreach (var pathData in MonsterPathDatas)
            {
                if (pathData.Value.spawnArea == currentSpawner)
                {
                    pathData.Value.UpdatePath();
                }
            }

            SyncPathsWithOtherPlayers();
            

            return true;
        }

        internal static void SyncPathsWithOtherPlayers() =>
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody, nameof(SyncPathsWithOtherPlayers));

        internal static void RPC_SyncPathsWithOtherPlayers(long _)
        {
            RefreshAllLists();
            foreach (SpawnArea spawner in AllSpawners)
            {
                HashSet<Vector3> loadPath = LoadPath(spawner);
                CreateLineRenderer(spawner, loadPath);
                Chat.instance.SetNpcText(spawner.gameObject, Vector3.up * 1.5f, 20f, 2.5f, "", "Loaded", false);
            }
        }

        private static bool FindSpawner(out SpawnArea spawner)
        {
            spawner = null;
            GameObject hoverObject = m_localPlayer.GetHoverObject();
            if (!hoverObject) return false;
            SpawnArea spawnArea =
                hoverObject.GetComponentInParent<SpawnArea>() ?? hoverObject.GetComponent<SpawnArea>();
            if (!spawnArea) return false;

            string names = "";
            foreach (SpawnArea.SpawnData item in spawnArea.m_prefabs)
            {
                names += item.m_prefab.name + " ";
            }

            m_localPlayer.Message(MessageHud.MessageType.TopLeft, $"A spawner of {names} has been found");
            spawnArea.m_spawnRadius = 0;
            //spawnArea.GetComponentsInChildren<Collider>().ToHashSet().ForEach(x => x.enabled = false);
            spawnArea.m_setPatrolSpawnPoint = true;

            spawner = spawnArea;
            return true;
        }

        internal static bool LoadPathToCurrent(SpawnArea spawner)
        {
            currentPath = LoadPath(spawner);
            if (currentPath == null) currentPath = new();
            return true;
        }

        internal static HashSet<Vector3> LoadPath(SpawnArea spawner)
        {
            HashSet<Vector3> Rvector3s = new();
            if (spawner == null || spawner.m_nview == null || !spawner.m_nview.IsValid()) return Rvector3s;
            string json = spawner.m_nview.GetZDO().GetString(CONST.ZDO_PATH);
            if (string.IsNullOrEmpty(json)) return Rvector3s;
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            HashSet<CustomVector3> vector3s = deserializer.Deserialize<HashSet<CustomVector3>>(json);
            if (vector3s == null) return Rvector3s;

            for (int i = 0; i < vector3s.Count; i++) Rvector3s.Add(vector3s.ToList()[i].ToVector3());

            return Rvector3s;
        }

        internal static Color32 LoadColor(SpawnArea spawner)
        {
            Color32 color = Color.clear;
            string savedColor = spawner.m_nview.GetZDO().GetString(ZDO_COLOR, "");
            if (savedColor != "")
            {
                IDeserializer deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                color = deserializer.Deserialize<Color32>(savedColor);
            }

            return color;
        }

        internal static void SaveColor(SpawnArea spawner, Color32 color)
        {
            ISerializer serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            string yaml = serializer.Serialize(color);
            spawner.m_nview.GetZDO().Set(ZDO_COLOR, yaml);
        }

        internal static void CreateLineRenderer(SpawnArea spawner, HashSet<Vector3> path = null)
        {
            LineRenderer lineRenderer = spawner.GetComponentInChildren<LineRenderer>();
            Vector3 position = new(spawner.transform.position.x, spawner.transform.position.y + upModifier,
                spawner.transform.position.z);
            if (lineRenderer == null)
            {
                var zdo = spawner.m_nview.GetZDO();
                lineRenderer = Object.Instantiate(new GameObject("LineRenderer"), spawner.transform)
                    .AddComponent<LineRenderer>();
                lineRenderer.positionCount = 1;
                lineRenderer.useWorldSpace = true;
                lineRenderer.widthMultiplier = 0.1f;
                lineRenderer.numCornerVertices = 10;
                lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                Color32 color = Random.ColorHSV(0f, 1f, 1f, 1f, 0.5f, 1f);
                Color32 savedColor = LoadColor(spawner);
                if (savedColor != Color.clear) color = savedColor;
                else SaveColor(spawner, color);

                lineRenderer.startColor = color;
                lineRenderer.endColor = color;
                lineRenderer.material = _self.lineRendererMaterial;
                lineRenderer.loop = false;
            }

            currentLineRenderer = lineRenderer;
            lineRenderer.SetPosition(0, position);
            var pathList = path.ToList();
            if (path != null && path.Count != 0)
            {
                lineRenderer.positionCount = path.Count + 1;
                for (int i = 1; i < lineRenderer.positionCount - 1; i++)
                {
                    lineRenderer.SetPosition(i, pathList[i - 1]);
                }

                lineRenderer.SetPosition(lineRenderer.positionCount - 1, pathList[path.Count - 1]);
            }

            if (!AllLineRenderers.Contains(lineRenderer)) AllLineRenderers.Add(lineRenderer);
        }

        internal static void RefreshAllMonstersDic()
        {
            Dictionary<MonsterAI, MonsterPathData> returnD = new();
            foreach (KeyValuePair<MonsterAI, MonsterPathData> item in MonsterPathDatas)
            {
                if (item.Key != null) returnD.Add(item.Key, item.Value);
            }

            MonsterPathDatas = returnD;
        }

        private static bool PieceRayTest(out Vector3 point, out Vector3 normal)
        {
            int layerMask = m_localPlayer.m_placeRayMask;
            if (Physics.Raycast(GameCamera.instance.transform.position, GameCamera.instance.transform.forward,
                    out RaycastHit hitInfo, 50f, layerMask) && hitInfo.collider &&
                !hitInfo.collider.attachedRigidbody && Vector3.Distance(m_localPlayer.m_eye.position, hitInfo.point) <
                m_localPlayer.m_maxPlaceDistance)
            {
                point = hitInfo.point;
                normal = hitInfo.normal;
                return true;
            }

            point = Vector3.zero;
            normal = Vector3.zero;
            return false;
        }

        private static void Heightlight(GameObject obj)
        {
            _self.StartCoroutine(HeightlightChest(obj));
        }

        private static IEnumerator HeightlightChest(GameObject obj)
        {
            Renderer[] componentsInChildren = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in componentsInChildren)
            {
                foreach (Material material in renderer.materials)
                {
                    if (material.HasProperty("_EmissionColor"))
                        material.SetColor("_EmissionColor", Color.yellow * 0.7f);
                    material.color = Color.yellow;
                }
            }

            yield return new WaitForSeconds(1f);
            foreach (Renderer renderer in componentsInChildren)
            {
                foreach (Material material in renderer.materials)
                {
                    if (material.HasProperty("_EmissionColor"))
                        material.SetColor("_EmissionColor", Color.white * 0f);
                    material.color = Color.white;
                }
            }
        }

        static void ShowpointsIfWandInHands(LineRenderer lineRenderer)
        {
            var weapon = Player.m_localPlayer.GetCurrentWeapon();

            if (weapon == null)
            {
                lineRenderer.enabled = false;
                return;
            }

            if (weapon.m_shared.m_name == "$item_JF_WayPointsWand")
            {
                lineRenderer.enabled = true;
                return;
            }
            else
            {
                lineRenderer.enabled = false;
            }
        }

        public static void UpdateLines()
        {
            Task.Run(() =>
            {
                try
                {
                    foreach (LineRenderer line in AllLineRenderers)
                    {
                        var loadColor = LoadColor(line.GetComponentInParent<SpawnArea>());
                        line.startColor = loadColor;
                        line.endColor = loadColor;
                        switch (lineShowMode)
                        {
                            case LineShowMode.Admin:
                                line.enabled = configSync.IsAdmin || ZNet.m_isServer;
                                break;
                            case LineShowMode.Nobody:
                                line.enabled = false;
                                break;
                            case LineShowMode.EveryOne:
                                line.enabled = true;
                                break;
                            case LineShowMode.Admin_WhenWandInHands:
                                if (configSync.IsAdmin || ZNet.m_isServer)
                                {
                                    ShowpointsIfWandInHands(line);
                                    break;
                                }
                                else line.enabled = false;

                                break;
                            case LineShowMode.EveryOne_WhenWandInHands:
                                ShowpointsIfWandInHands(line);
                                break;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug(e.Message);
                }
            });


            Task.WaitAll();
        }
        
        internal static void RefreshAllLists()
        {
            RefreshAllSpawnersList();
            RefreshAllLineRenderersList();
        }
        internal static void RefreshAllSpawnersList()
        {
            HashSet<SpawnArea> returnList = new();
            foreach (var spawner in AllSpawners)
            {
                if (spawner != null && !returnList.Contains(spawner)) returnList.Add(spawner);
            }
            AllSpawners = returnList;
        }
        
        internal static void RefreshAllLineRenderersList()
        {
            HashSet<LineRenderer> returnList = new();
            
            foreach (var line in AllLineRenderers)
            {
                if (line != null && !returnList.Contains(line)) returnList.Add(line);
            }
            AllLineRenderers = returnList;
        }
    }
}