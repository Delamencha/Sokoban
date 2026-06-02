using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sokoban
{
    public class LevelSolverOptions
    {
        public int maxExploredStates = 100000;
        public float maxDurationSeconds = 5f;
    }

    public class LevelSolverResult
    {
        public LevelSolverResult(
            bool solved,
            bool searchLimitReached,
            bool timeLimitReached,
            int exploredStateCount,
            int pushCount,
            int visitedStateCount,
            int prunedDeadlockCount,
            string actionSequence,
            List<string> errors,
            string message)
        {
            this.solved = solved;
            this.searchLimitReached = searchLimitReached;
            this.timeLimitReached = timeLimitReached;
            this.exploredStateCount = exploredStateCount;
            this.pushCount = pushCount;
            this.visitedStateCount = visitedStateCount;
            this.prunedDeadlockCount = prunedDeadlockCount;
            this.actionSequence = actionSequence ?? string.Empty;
            this.errors = errors ?? new List<string>();
            this.message = message ?? string.Empty;
        }

        public readonly bool solved;
        public readonly bool searchLimitReached;
        public readonly bool timeLimitReached;
        public readonly int exploredStateCount;
        public readonly int pushCount;
        public readonly int visitedStateCount;
        public readonly int prunedDeadlockCount;
        public readonly string actionSequence;
        public readonly List<string> errors;
        public readonly string message;

        public bool HasErrors => errors.Count > 0;
        public bool LimitReached => searchLimitReached || timeLimitReached;
        public bool ConfirmedNoSolution => !solved && !LimitReached && !HasErrors;
    }

    public static class LevelSolver
    {
        private struct SolverState
        {
            public Vector2Int player;
            public List<Vector2Int> boxes;
            public int pushCount;
            public string actionSequence;
        }

        private struct PrioritizedSolverState
        {
            public SolverState state;
            public string stateKey;
            public int priority;
        }

        private class ReachableArea
        {
            private readonly Dictionary<Vector2Int, Vector2Int> parents = new Dictionary<Vector2Int, Vector2Int>();
            private readonly Dictionary<Vector2Int, char> actionsFromParent = new Dictionary<Vector2Int, char>();
            private Vector2Int representative;

            public bool HasAny => parents.Count > 0;
            public Vector2Int Representative => representative;

            public void AddStart(Vector2Int position)
            {
                parents[position] = position;
                representative = position;
            }

            public void Add(Vector2Int position, Vector2Int parent, char actionFromParent)
            {
                parents[position] = parent;
                actionsFromParent[position] = actionFromParent;
                if (IsBefore(position, representative))
                {
                    representative = position;
                }
            }

            public bool Contains(Vector2Int position)
            {
                return parents.ContainsKey(position);
            }

            public string GetPathTo(Vector2Int target)
            {
                if (!parents.ContainsKey(target))
                {
                    return string.Empty;
                }

                List<char> actions = new List<char>();
                Vector2Int current = target;
                while (parents[current] != current)
                {
                    actions.Add(actionsFromParent[current]);
                    current = parents[current];
                }

                actions.Reverse();
                return new string(actions.ToArray());
            }
        }

        private class MinHeap
        {
            private readonly List<PrioritizedSolverState> items = new List<PrioritizedSolverState>();

            public int Count => items.Count;

            public void Enqueue(PrioritizedSolverState item)
            {
                items.Add(item);
                BubbleUp(items.Count - 1);
            }

            public PrioritizedSolverState Dequeue()
            {
                PrioritizedSolverState result = items[0];
                int lastIndex = items.Count - 1;
                items[0] = items[lastIndex];
                items.RemoveAt(lastIndex);
                if (items.Count > 0)
                {
                    BubbleDown(0);
                }

                return result;
            }

            private void BubbleUp(int index)
            {
                while (index > 0)
                {
                    int parent = (index - 1) / 2;
                    if (Compare(items[parent], items[index]) <= 0)
                    {
                        return;
                    }

                    Swap(parent, index);
                    index = parent;
                }
            }

            private void BubbleDown(int index)
            {
                while (true)
                {
                    int left = index * 2 + 1;
                    int right = left + 1;
                    int smallest = index;
                    if (left < items.Count && Compare(items[left], items[smallest]) < 0)
                    {
                        smallest = left;
                    }

                    if (right < items.Count && Compare(items[right], items[smallest]) < 0)
                    {
                        smallest = right;
                    }

                    if (smallest == index)
                    {
                        return;
                    }

                    Swap(index, smallest);
                    index = smallest;
                }
            }

            private void Swap(int first, int second)
            {
                PrioritizedSolverState temp = items[first];
                items[first] = items[second];
                items[second] = temp;
            }

            private static int Compare(PrioritizedSolverState left, PrioritizedSolverState right)
            {
                int priorityComparison = left.priority.CompareTo(right.priority);
                return priorityComparison != 0 ? priorityComparison : left.state.pushCount.CompareTo(right.state.pushCount);
            }
        }

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.down,
            Vector2Int.left,
            Vector2Int.right
        };

        public static LevelSolverResult Solve(LevelData level)
        {
            return Solve(level, new LevelSolverOptions());
        }

        public static LevelSolverResult Solve(LevelData level, LevelSolverOptions options)
        {
            LevelSolverOptions solverOptions = options ?? new LevelSolverOptions();
            List<string> errors = LevelValidator.ValidateBasic(level);
            if (errors.Count > 0)
            {
                return new LevelSolverResult(
                    false,
                    false,
                    false,
                    0,
                    0,
                    0,
                    0,
                    string.Empty,
                    errors,
                    string.Join("\n", errors));
            }

            LevelData workingLevel = level.Clone();
            workingLevel.EnsureTiles();
            HashSet<Vector2Int> targets = new HashSet<Vector2Int>(workingLevel.targets.Select(position => position.ToVector2Int()));
            List<Vector2Int> startBoxes = workingLevel.boxes.Select(position => position.ToVector2Int()).ToList();
            SortBoxes(startBoxes);
            HashSet<Vector2Int> staticDeadlocks = CreateStaticDeadlockPositions(workingLevel, targets);

            SolverState start = new SolverState
            {
                player = workingLevel.player.ToVector2Int(),
                boxes = startBoxes,
                pushCount = 0,
                actionSequence = string.Empty
            };
            ReachableArea startReachableArea = FindReachableArea(workingLevel, start.player, new HashSet<Vector2Int>(start.boxes));

            Queue<SolverState> open = new Queue<SolverState>();
            HashSet<string> visited = new HashSet<string>();
            open.Enqueue(start);
            visited.Add(CreateStateKey(startReachableArea.Representative, start.boxes));

            int exploredStateCount = 0;
            int prunedDeadlockCount = 0;
            int maxExploredStates = Mathf.Max(1, solverOptions.maxExploredStates);
            float maxDurationSeconds = Mathf.Max(0.01f, solverOptions.maxDurationSeconds);
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (open.Count > 0)
            {
                if (exploredStateCount >= maxExploredStates)
                {
                    return new LevelSolverResult(
                        false,
                        true,
                        false,
                        exploredStateCount,
                        0,
                        visited.Count,
                        prunedDeadlockCount,
                        string.Empty,
                        null,
                        "求解达到搜索上限，无法确认是否有解。");
                }

                if (stopwatch.Elapsed.TotalSeconds >= maxDurationSeconds)
                {
                    return new LevelSolverResult(
                        false,
                        false,
                        true,
                        exploredStateCount,
                        0,
                        visited.Count,
                        prunedDeadlockCount,
                        string.Empty,
                        null,
                        "求解超过最大耗时，无法确认是否有解。");
                }

                SolverState current = open.Dequeue();
                exploredStateCount++;
                if (IsSolved(current.boxes, targets))
                {
                    return new LevelSolverResult(
                        true,
                        false,
                        false,
                        exploredStateCount,
                        current.pushCount,
                        visited.Count,
                        prunedDeadlockCount,
                        current.actionSequence,
                        null,
                        "关卡有解。");
                }

                HashSet<Vector2Int> currentBoxes = new HashSet<Vector2Int>(current.boxes);
                ReachableArea reachableArea = FindReachableArea(workingLevel, current.player, currentBoxes);
                for (int boxIndex = 0; boxIndex < current.boxes.Count; boxIndex++)
                {
                    Vector2Int box = current.boxes[boxIndex];
                    for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                    {
                        Vector2Int direction = Directions[directionIndex];
                        Vector2Int pusherPosition = box - direction;
                        Vector2Int nextBoxPosition = box + direction;
                        if (!reachableArea.Contains(pusherPosition) || !IsWalkable(workingLevel, nextBoxPosition) || currentBoxes.Contains(nextBoxPosition))
                        {
                            continue;
                        }

                        if (staticDeadlocks.Contains(nextBoxPosition))
                        {
                            prunedDeadlockCount++;
                            continue;
                        }

                        List<Vector2Int> nextBoxes = new List<Vector2Int>(current.boxes);
                        nextBoxes[boxIndex] = nextBoxPosition;
                        SortBoxes(nextBoxes);

                        Vector2Int nextPlayer = box;
                        ReachableArea nextReachableArea = FindReachableArea(workingLevel, nextPlayer, new HashSet<Vector2Int>(nextBoxes));
                        string nextKey = CreateStateKey(nextReachableArea.Representative, nextBoxes);
                        if (!visited.Add(nextKey))
                        {
                            continue;
                        }

                        string playerPath = reachableArea.GetPathTo(pusherPosition);
                        open.Enqueue(new SolverState
                        {
                            player = nextPlayer,
                            boxes = nextBoxes,
                            pushCount = current.pushCount + 1,
                            actionSequence = current.actionSequence + playerPath + DirectionToAction(direction)
                        });
                    }
                }
            }

            return new LevelSolverResult(
                false,
                false,
                false,
                exploredStateCount,
                0,
                visited.Count,
                prunedDeadlockCount,
                string.Empty,
                null,
                "完整搜索后未找到解。");
        }

        public static LevelSolverResult SolveAStar(LevelData level, LevelSolverOptions options)
        {
            LevelSolverOptions solverOptions = options ?? new LevelSolverOptions();
            List<string> errors = LevelValidator.ValidateBasic(level);
            if (errors.Count > 0)
            {
                return new LevelSolverResult(
                    false,
                    false,
                    false,
                    0,
                    0,
                    0,
                    0,
                    string.Empty,
                    errors,
                    string.Join("\n", errors));
            }

            LevelData workingLevel = level.Clone();
            workingLevel.EnsureTiles();
            HashSet<Vector2Int> targets = new HashSet<Vector2Int>(workingLevel.targets.Select(position => position.ToVector2Int()));
            List<Vector2Int> targetList = targets.ToList();
            List<Vector2Int> startBoxes = workingLevel.boxes.Select(position => position.ToVector2Int()).ToList();
            SortBoxes(startBoxes);
            HashSet<Vector2Int> staticDeadlocks = CreateStaticDeadlockPositions(workingLevel, targets);

            SolverState start = new SolverState
            {
                player = workingLevel.player.ToVector2Int(),
                boxes = startBoxes,
                pushCount = 0,
                actionSequence = string.Empty
            };
            ReachableArea startReachableArea = FindReachableArea(workingLevel, start.player, new HashSet<Vector2Int>(start.boxes));
            string startKey = CreateStateKey(startReachableArea.Representative, start.boxes);

            MinHeap open = new MinHeap();
            Dictionary<string, int> bestPushCounts = new Dictionary<string, int>();
            bestPushCounts[startKey] = 0;
            open.Enqueue(new PrioritizedSolverState
            {
                state = start,
                stateKey = startKey,
                priority = EstimateRemainingPushes(start.boxes, targetList)
            });

            int exploredStateCount = 0;
            int prunedDeadlockCount = 0;
            int maxExploredStates = Mathf.Max(1, solverOptions.maxExploredStates);
            float maxDurationSeconds = Mathf.Max(0.01f, solverOptions.maxDurationSeconds);
            Stopwatch stopwatch = Stopwatch.StartNew();
            while (open.Count > 0)
            {
                if (exploredStateCount >= maxExploredStates)
                {
                    return new LevelSolverResult(
                        false,
                        true,
                        false,
                        exploredStateCount,
                        0,
                        bestPushCounts.Count,
                        prunedDeadlockCount,
                        string.Empty,
                        null,
                        "A* 求解达到搜索上限，无法确认是否有解。");
                }

                if (stopwatch.Elapsed.TotalSeconds >= maxDurationSeconds)
                {
                    return new LevelSolverResult(
                        false,
                        false,
                        true,
                        exploredStateCount,
                        0,
                        bestPushCounts.Count,
                        prunedDeadlockCount,
                        string.Empty,
                        null,
                        "A* 求解超过最大耗时，无法确认是否有解。");
                }

                PrioritizedSolverState prioritized = open.Dequeue();
                SolverState current = prioritized.state;
                if (bestPushCounts.TryGetValue(prioritized.stateKey, out int bestPushCount) && current.pushCount > bestPushCount)
                {
                    continue;
                }

                exploredStateCount++;
                if (IsSolved(current.boxes, targets))
                {
                    return new LevelSolverResult(
                        true,
                        false,
                        false,
                        exploredStateCount,
                        current.pushCount,
                        bestPushCounts.Count,
                        prunedDeadlockCount,
                        current.actionSequence,
                        null,
                        "A* 求解找到可行解。");
                }

                HashSet<Vector2Int> currentBoxes = new HashSet<Vector2Int>(current.boxes);
                ReachableArea reachableArea = FindReachableArea(workingLevel, current.player, currentBoxes);
                for (int boxIndex = 0; boxIndex < current.boxes.Count; boxIndex++)
                {
                    Vector2Int box = current.boxes[boxIndex];
                    for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
                    {
                        Vector2Int direction = Directions[directionIndex];
                        Vector2Int pusherPosition = box - direction;
                        Vector2Int nextBoxPosition = box + direction;
                        if (!reachableArea.Contains(pusherPosition) || !IsWalkable(workingLevel, nextBoxPosition) || currentBoxes.Contains(nextBoxPosition))
                        {
                            continue;
                        }

                        if (staticDeadlocks.Contains(nextBoxPosition))
                        {
                            prunedDeadlockCount++;
                            continue;
                        }

                        List<Vector2Int> nextBoxes = new List<Vector2Int>(current.boxes);
                        nextBoxes[boxIndex] = nextBoxPosition;
                        SortBoxes(nextBoxes);

                        Vector2Int nextPlayer = box;
                        ReachableArea nextReachableArea = FindReachableArea(workingLevel, nextPlayer, new HashSet<Vector2Int>(nextBoxes));
                        string nextKey = CreateStateKey(nextReachableArea.Representative, nextBoxes);
                        int nextPushCount = current.pushCount + 1;
                        if (bestPushCounts.TryGetValue(nextKey, out int knownPushCount) && knownPushCount <= nextPushCount)
                        {
                            continue;
                        }

                        bestPushCounts[nextKey] = nextPushCount;
                        string playerPath = reachableArea.GetPathTo(pusherPosition);
                        SolverState nextState = new SolverState
                        {
                            player = nextPlayer,
                            boxes = nextBoxes,
                            pushCount = nextPushCount,
                            actionSequence = current.actionSequence + playerPath + DirectionToAction(direction)
                        };
                        open.Enqueue(new PrioritizedSolverState
                        {
                            state = nextState,
                            stateKey = nextKey,
                            priority = nextPushCount + EstimateRemainingPushes(nextBoxes, targetList)
                        });
                    }
                }
            }

            return new LevelSolverResult(
                false,
                false,
                false,
                exploredStateCount,
                0,
                bestPushCounts.Count,
                prunedDeadlockCount,
                string.Empty,
                null,
                "A* 完整搜索后未找到解。");
        }

        private static ReachableArea FindReachableArea(LevelData level, Vector2Int start, HashSet<Vector2Int> boxes)
        {
            Queue<Vector2Int> open = new Queue<Vector2Int>();
            ReachableArea reachableArea = new ReachableArea();
            if (!IsWalkable(level, start) || boxes.Contains(start))
            {
                return reachableArea;
            }

            open.Enqueue(start);
            reachableArea.AddStart(start);
            while (open.Count > 0)
            {
                Vector2Int current = open.Dequeue();
                for (int i = 0; i < Directions.Length; i++)
                {
                    Vector2Int direction = Directions[i];
                    Vector2Int next = current + direction;
                    if (!IsWalkable(level, next) || boxes.Contains(next) || reachableArea.Contains(next))
                    {
                        continue;
                    }

                    reachableArea.Add(next, current, DirectionToActionChar(direction));
                    open.Enqueue(next);
                }
            }

            return reachableArea;
        }

        private static bool IsSolved(List<Vector2Int> boxes, HashSet<Vector2Int> targets)
        {
            return boxes.Count > 0 && boxes.All(box => targets.Contains(box));
        }

        private static bool IsWalkable(LevelData level, Vector2Int position)
        {
            return level.IsInside(position) && level.GetTile(position) == LevelTile.Floor;
        }

        private static int EstimateRemainingPushes(List<Vector2Int> boxes, List<Vector2Int> targets)
        {
            if (boxes == null || targets == null || boxes.Count == 0 || targets.Count == 0)
            {
                return 0;
            }

            if (boxes.Count != targets.Count || boxes.Count > 12)
            {
                return EstimateNearestTargetDistance(boxes, targets);
            }

            Dictionary<string, int> memo = new Dictionary<string, int>();
            return EstimateMinimumMatchingDistance(boxes, targets, 0, 0, memo);
        }

        private static int EstimateMinimumMatchingDistance(
            List<Vector2Int> boxes,
            List<Vector2Int> targets,
            int boxIndex,
            int usedTargetMask,
            Dictionary<string, int> memo)
        {
            if (boxIndex >= boxes.Count)
            {
                return 0;
            }

            string key = boxIndex + "|" + usedTargetMask;
            if (memo.TryGetValue(key, out int cached))
            {
                return cached;
            }

            int best = int.MaxValue;
            for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
            {
                int targetBit = 1 << targetIndex;
                if ((usedTargetMask & targetBit) != 0)
                {
                    continue;
                }

                int distance = ManhattanDistance(boxes[boxIndex], targets[targetIndex]);
                int remaining = EstimateMinimumMatchingDistance(boxes, targets, boxIndex + 1, usedTargetMask | targetBit, memo);
                best = Mathf.Min(best, distance + remaining);
            }

            memo[key] = best == int.MaxValue ? 0 : best;
            return memo[key];
        }

        private static int EstimateNearestTargetDistance(List<Vector2Int> boxes, List<Vector2Int> targets)
        {
            int total = 0;
            for (int boxIndex = 0; boxIndex < boxes.Count; boxIndex++)
            {
                int best = int.MaxValue;
                for (int targetIndex = 0; targetIndex < targets.Count; targetIndex++)
                {
                    best = Mathf.Min(best, ManhattanDistance(boxes[boxIndex], targets[targetIndex]));
                }

                if (best != int.MaxValue)
                {
                    total += best;
                }
            }

            return total;
        }

        private static int ManhattanDistance(Vector2Int first, Vector2Int second)
        {
            return Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);
        }

        private static string DirectionToAction(Vector2Int direction)
        {
            return DirectionToActionChar(direction).ToString();
        }

        private static char DirectionToActionChar(Vector2Int direction)
        {
            if (direction == Vector2Int.up)
            {
                return 'U';
            }

            if (direction == Vector2Int.down)
            {
                return 'D';
            }

            if (direction == Vector2Int.left)
            {
                return 'L';
            }

            return 'R';
        }

        private static HashSet<Vector2Int> CreateStaticDeadlockPositions(LevelData level, HashSet<Vector2Int> targets)
        {
            HashSet<Vector2Int> deadlocks = new HashSet<Vector2Int>();
            for (int y = 0; y < level.height; y++)
            {
                for (int x = 0; x < level.width; x++)
                {
                    Vector2Int position = new Vector2Int(x, y);
                    if (targets.Contains(position) || !IsWalkable(level, position))
                    {
                        continue;
                    }

                    bool blockedVertical = IsStaticBlocked(level, position + Vector2Int.up) || IsStaticBlocked(level, position + Vector2Int.down);
                    bool blockedHorizontal = IsStaticBlocked(level, position + Vector2Int.left) || IsStaticBlocked(level, position + Vector2Int.right);
                    if (blockedVertical && blockedHorizontal)
                    {
                        deadlocks.Add(position);
                    }
                }
            }

            return deadlocks;
        }

        private static bool IsStaticBlocked(LevelData level, Vector2Int position)
        {
            return !IsWalkable(level, position);
        }

        private static bool IsBefore(Vector2Int candidate, Vector2Int current)
        {
            int yComparison = candidate.y.CompareTo(current.y);
            return yComparison != 0 ? yComparison < 0 : candidate.x < current.x;
        }

        private static void SortBoxes(List<Vector2Int> boxes)
        {
            boxes.Sort((left, right) =>
            {
                int yComparison = left.y.CompareTo(right.y);
                return yComparison != 0 ? yComparison : left.x.CompareTo(right.x);
            });
        }

        private static string CreateStateKey(Vector2Int player, List<Vector2Int> boxes)
        {
            string key = player.x + "," + player.y;
            for (int i = 0; i < boxes.Count; i++)
            {
                key += "|" + boxes[i].x + "," + boxes[i].y;
            }

            return key;
        }
    }
}
