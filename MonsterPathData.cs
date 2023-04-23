using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Random = UnityEngine.Random;
using static TowerDefense.Plugin;

namespace TowerDefense;

internal class MonsterPathData
{
    internal MonsterAI monsterAI;
    internal SpawnArea spawnArea;
    internal List<Vector3> path = new();
    internal int currentNodeIndex = 0;
    internal int nextNodeIndex => currentNodeIndex + 1;
    internal Vector3 pos => monsterAI.transform.position;

    public MonsterPathData(MonsterAI monsterAI, SpawnArea spawnArea)
    {
        this.monsterAI = monsterAI;
        this.path = WayPointsSys.GetSpawnerPath(spawnArea);
        this.spawnArea = spawnArea;

        SetCurrentNode(0);
    }

    private void SetCurrentNode(int i)
    {
        currentNodeIndex = i;
    }

    private Vector3 CurrentNode() => path.Count > currentNodeIndex
        ? path[currentNodeIndex]
        : Vector3.zero;

    private Vector3 NextNode() => path.Count > nextNodeIndex
        ? path[nextNodeIndex]
        : Vector3.zero;

    private bool ShouldGoToNext()
    {
        if (OnPathEnd()) return false;
        return (Utils.DistanceXZ(pos, CurrentNode()) <= 1);
    }

    internal bool OnPathEnd()
    {
        return CurrentNode() == Vector3.zero;
    }

    private bool TryGoToNext()
    {
        if (!ShouldGoToNext()) return false;
        SetCurrentNode(nextNodeIndex);
        return true;
    }

    static float distance = 5;

    internal bool GoToNode(float dt)
    {
        if (monsterAI.FindEnemy()) return true;
        else
        {
            monsterAI.m_targetCreature = null;
            monsterAI.SetAlerted(false);
        }

        TryGoToNext();

        List<Piece> pieces = new List<Piece>();
        var node = CurrentNode();
        if (OnPathEnd() && (!monsterAI.m_targetStatic || !monsterAI.m_targetStatic.name.Contains("DestroyMe")))
        {
            var colliders = Physics.OverlapSphere(monsterAI.transform.position, 30).ToList();
            colliders.ForEach(c =>
            {
                var piece = c.GetComponentInParent<Piece>();
                if (piece && piece.m_name == CONST.PIECE_NAME)
                {
                    pieces.Add(piece);
                }
            });

            monsterAI.m_targetStatic = Nearest(Player.m_localPlayer.gameObject, pieces);

            return AttackTargetPiece(dt);
        }

        // if (monsterAI.m_character.GetMoveDir() == Vector3.zero &&
        //     CurrentNode() != path.First())
        //{
        // var destructible = GetMonsterLookingDestructible(monsterAI, out GameObject gameObject);
        // if (destructible == null) destructible = GetDestructibleArountMonster(monsterAI, out gameObject);
        // monsterAI.LookAt(gameObject.transform.position);
        // monsterAI.DoAttack(null, false);
        // if (destructible != null)
        // {
        //     monsterAI.LookAt(gameObject.transform.position);
        //     monsterAI.DoAttack(null, false);
        // }
        //}

        if (monsterAI.MoveTo(dt, node, 1f, true))
        {
            monsterAI.m_targetStatic = monsterAI.FindClosestStaticPriorityTarget();
            if (monsterAI.m_targetStatic)

            {
                return AttackTargetPiece(dt);
            }
        }
        else
        {
            monsterAI.m_targetStatic = null;
        }

        return false;
    }

    private bool AttackTargetPiece(float dt)
    {
        if (!monsterAI.m_targetStatic || !monsterAI) return true;
        Vector3 closestPoint = monsterAI.m_targetStatic.FindClosestPoint(monsterAI.transform.position);
        monsterAI.LookAt(monsterAI.m_targetStatic.GetCenter());

        var itemData = monsterAI.SelectBestAttack(monsterAI.m_character as Humanoid, dt);
        if (Vector3.Distance(closestPoint, monsterAI.transform.position) < (double)itemData.m_shared.m_aiAttackRange &&
            monsterAI.CanSeeTarget(monsterAI.m_targetStatic))
        {
            if (monsterAI.IsLookingAt(monsterAI.m_targetStatic.GetCenter(), 0))
            {
                if (monsterAI.m_aiStatus != null)
                    monsterAI.m_aiStatus = "Attacking piece";
                monsterAI.DoAttack(null, false);
                return false;
            }
        }
        else
        {
            monsterAI.MoveTo(dt, closestPoint, 0, true);
            return false;
        }

        return true;
    }

    public void UpdatePath()
    {
        path = WayPointsSys.LoadPath(spawnArea);
        if (currentNodeIndex > path.Count - 1) currentNodeIndex = path.Count - 1;
    }
}