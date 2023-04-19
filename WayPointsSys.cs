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
        internal static List<SpawnArea> AllSpawners = new();
        internal static Dictionary<MonsterAI, MonsterPathData> MonsterPathDatas = new();

        private static SpawnArea currentSpawner;
        private static List<Vector3> currentPath = new();
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
            RefreshAllSpawnersList();
            RefreshAllMonstersDic();
            SaveSpawnerData();
            if (!FindSpawner(out SpawnArea newSA))
            {
                m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Spawner deselected");
                currentSpawner = null;
                currentLineRenderer = null;
                currentPath = new();
                return false;
            }


            LoadPathToCurrent(newSA);
            CreateLineRenderer(newSA, currentPath);
            Heightlight(newSA.gameObject);
            if (!AllSpawners.Contains(newSA)) AllSpawners.Add(currentSpawner);

            if (newSA == currentSpawner)
            {
                if (currentLineRenderer.enabled)
                {
                    m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Line visul loacaly deactivated");
                    currentLineRenderer.enabled = false;
                    currentSpawner.m_nview.GetZDO().Set("enabledLineRenderer", false);
                }
                else
                {
                    m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Line visul loacaly activated");
                    currentLineRenderer.enabled = true;
                    currentSpawner.m_nview.GetZDO().Set("enabledLineRenderer", true);
                }

                return false;
            }

            currentSpawner = newSA;
            currentLineRenderer.enabled = true;

            return true;
        }

        private static bool CreateWayPoint()
        {
            if (!currentSpawner) return false;
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
                    float dist = Vector3.Distance(currentPath[currentPath.Count - 1], point);
                    if (dist < minDistanceBetweenPoints)
                    {
                        m_localPlayer.Message(MessageHud.MessageType.TopLeft, "The previous point is too close");
                        return false;
                    }
                }

                point.y += upModifier;
                currentPath.Add(point);
                currentLineRenderer.positionCount++;
                currentLineRenderer.SetPosition(currentLineRenderer.positionCount - 1, point);
            }

            SaveSpawnerData();

            //m_localPlayer.Message(MessageHud.MessageType.TopLeft, "CreateWayPoint");
            return true;
        }

        private static bool TargetDestroyObj()
        {
            if (!currentSpawner) return false;
            if (currentPath == null)
            {
                currentPath = new();
                return false;
            }

            Piece hoveringPiece = m_localPlayer.GetHoveringPiece();

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
            currentPath.RemoveAt(currentPath.Count - 1);
            SaveSpawnerData();

            return true;
        }

        internal static List<Vector3> GetSpawnerPath(SpawnArea spawnArea)
        {
            if (spawnArea == null) throw new ArgumentException("spawnArea = null");

            return LoadPath(spawnArea);
        }

        internal static SpawnArea GetSpawnerOnPosition(Vector3 point)
        {
            foreach (SpawnArea item in AllSpawners)
            {
                if (Mathf.Approximately((int)item.transform.position.x, (int)point.x) &&
                    Mathf.Approximately((int)item.transform.position.z, (int)point.z)) return item;
            }

            return null;
        }

        internal static void MoveMonsterAlongPath(MonsterAI monster, float dt)
        {
            MonsterPathDatas[monster].GoToNode(dt);
        }

        internal static void RegisterMonster(BaseAI __instance)
        {
            if (!(__instance && __instance.m_character && __instance is MonsterAI monsterAI)) return;
            if (MonsterPathDatas.ContainsKey(monsterAI)) return;
            RefreshAllSpawnersList();
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
            List<CustomVector3> customVectorsToSave = new();
            currentPath.ForEach(x => customVectorsToSave.Add(x.ToCustomVector3()));
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
            RefreshAllSpawnersList();
            //FillAllSpawnersList();

            foreach (SpawnArea spawner in AllSpawners)
            {
                List<Vector3> loadPath = LoadPath(spawner);
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
            //spawnArea.GetComponentsInChildren<Collider>().ToList().ForEach(x => x.enabled = false);
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

        internal static List<Vector3> LoadPath(SpawnArea spawner)
        {
            List<Vector3> Rvector3s = new();
            if (spawner == null || spawner.m_nview == null || !spawner.m_nview.IsValid()) return Rvector3s;
            string json = spawner.m_nview.GetZDO().GetString(CONST.ZDO_PATH);
            if (string.IsNullOrEmpty(json)) return Rvector3s;
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            List<CustomVector3> vector3s = deserializer.Deserialize<List<CustomVector3>>(json);
            if (vector3s == null) return Rvector3s;

            for (int i = 0; i < vector3s.Count; i++) Rvector3s.Add(vector3s[i].ToVector3());

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

        internal static void CreateLineRenderer(SpawnArea spawner, List<Vector3> path = null)
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
            if (path != null && path.Count != 0)
            {
                lineRenderer.positionCount = path.Count + 1;
                for (int i = 1; i < lineRenderer.positionCount - 1; i++)
                {
                    lineRenderer.SetPosition(i, path[i - 1]);
                }

                lineRenderer.SetPosition(lineRenderer.positionCount - 1, path[path.Count - 1]);
            }
        }

        internal static void RefreshAllSpawnersList()
        {
            List<SpawnArea> returnList = new();
            AllSpawners.ForEach(x =>
            {
                if (x != null) returnList.Add(x);
            });
            AllSpawners = returnList;
        }

        private static void FillAllSpawnersList()
        {
            if (SceneManager.GetActiveScene().name != "main") return;
            Task.Run((() =>
            {
                var spawnAreas = Object.FindObjectsOfType<SpawnArea>().ToList();
                spawnAreas.ForEach(x =>
                {
                    if (x.m_nview.GetZDO().GetString(CONST.ZDO_PATH, "") != "" && !AllSpawners.Contains(x))
                        AllSpawners.Add(x);
                });
            }));
            Task.WaitAll();
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

        private static void ResetSpawnerData(SpawnArea spawner1 = null)
        {
            if (spawner1 == null) spawner1 = currentSpawner;
            spawner1.m_nview.GetZDO().m_strings.Remove(CONST.ZDO_PATH.GetHashCode());
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
            Task task = null;


            task = Task.Run(() =>
            {
                AllSpawners.ForEach(x =>
                {
                    if (x.m_nview.GetZDO().GetString(ZDO_PATH, "") != "")
                    {
                        AllSpawners.Add(x);
                        LineRenderer line = x.GetComponentInChildren<LineRenderer>();
                        {
                            var loadColor = LoadColor(x);
                            line.startColor = loadColor;
                            line.endColor = loadColor;
                            if(!x.m_nview.GetZDO().GetBool("enabledLineRenderer", true)) return;
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
                                    if (!configSync.IsAdmin && !ZNet.m_isServer)
                                    {
                                        line.enabled = false;
                                        break;
                                    }

                                    ShowpointsIfWandInHands(line);
                                    break;
                                case LineShowMode.EveryOne_WhenWandInHands:
                                    ShowpointsIfWandInHands(line);
                                    break;
                            }
                        }
                    }
                });
            });

            Task.WaitAll();
        }
    }
}