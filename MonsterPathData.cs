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
    internal bool inMove =false;

    public MonsterPathData(MonsterAI monsterAI, SpawnArea spawnArea)
    {
        this.monsterAI = monsterAI;
        this.path = WayPointsSys.GetSpawnerPath(spawnArea).ToList();
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
        if (monsterAI.FindEnemy()) return false;
        else
        {
            monsterAI.m_targetCreature = null;
            monsterAI.m_targetStatic = null;
            monsterAI.SetAlerted(false);
        }

        TryGoToNext();

        List<Piece> pieces = new List<Piece>();
        var node = CurrentNode();
        var onPathEnd = OnPathEnd();
        if (onPathEnd is false)
        {
            Debug("onPathEnd false");
            var moveTo = monsterAI.MoveTo(dt, node, 1f, true);
            Debug($"moveTo result is {moveTo}");
            if (moveTo)
            {
                monsterAI.m_targetStatic = monsterAI.FindClosestStaticPriorityTarget();
                Debug("Finding closest target");

                if (monsterAI.m_targetStatic) return AttackTargetPiece(dt); 
                return true;
            }

            monsterAI.m_targetStatic = null;
            return true;
        }
        if (!monsterAI.m_targetStatic || !monsterAI.m_targetStatic.IsPriorityTarget())
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

            monsterAI.m_targetStatic = Nearest(monsterAI.transform.position, pieces);
            monsterAI.StopMoving();
            return false;
        }
        
        return AttackTargetPiece(dt);
    }

    private bool AttackTargetPiece(float dt)
    {
        Debug("AttackTargetPiece");
        if (!monsterAI.m_targetStatic || !monsterAI) return false;
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
                return true;
            }
        }
        else
        {
            monsterAI.MoveTo(dt, closestPoint, 0, true);
            return true;
        }

        return false;
    }

    public void UpdatePath()
    {
        path = WayPointsSys.LoadPath(spawnArea).ToList();
        if (currentNodeIndex > path.Count - 1) currentNodeIndex = path.Count - 1;
    }
}