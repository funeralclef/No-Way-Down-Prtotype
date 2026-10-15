using Godot;
using System.Collections.Generic;

public partial class PlayerJumpTrajectory : Node3D
{
    [Export(PropertyHint.Range, "10,30,1")]
    public int MarkerCount { get; set; } = 22;

    [Export(PropertyHint.Range, "12.0,40.0,0.5")]
    public float TargetSearchRadius { get; set; } = 29.0f;

    [Export(PropertyHint.Range, "1.0,3.0,0.05")]
    public float StompHorizontalRadius { get; set; } = 1.75f;

    [Export(PropertyHint.Range, "3.0,10.0,0.25")]
    public float MaximumPathMiss { get; set; } = 6.5f;

    [Export(PropertyHint.Range, "0.02,0.15,0.01")]
    public float UpdateInterval { get; set; } = 0.04f;

    [Export(PropertyHint.Layers3DPhysics)]
    public uint WorldCollisionMask { get; set; } =
        (1u << 2) | (1u << 3);

    public Mob CurrentTarget { get; private set; }
    public bool TargetReachable { get; private set; }
    public bool TargetOnCourse { get; private set; }
    public bool TrajectoryBlocked { get; private set; }
    public int VisibleMarkerCount { get; private set; }
    public int ClosestMarkerIndex { get; private set; } = -1;

    private readonly List<Vector3> _trajectoryPoints = new();
    private readonly List<float> _trajectoryTimes = new();
    private MultiMesh _markerInstances;
    private Player _player;
    private float _updateTimer;
    private float _launchVerticalSpeed;
    private float _gravity;

    private static readonly Color PlayerPathColor =
        new(0.18f, 0.78f, 1.0f);
    private static readonly Color ReachableColor =
        new(0.12f, 1.0f, 0.32f);
    private static readonly Color OnCourseColor =
        new(1.0f, 0.78f, 0.08f);
    private static readonly Color UnreachableColor =
        new(1.0f, 0.08f, 0.045f);
    public override void _Ready()
    {
        TopLevel = true;
        BindTrajectoryMarkers();
    }

    public void Initialize(Player player)
    {
        _player = player;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_player))
        {
            _player = GetTree().GetFirstNodeInGroup("player") as Player;
        }

        _updateTimer -= (float)delta;
        if (_updateTimer > 0.0f)
        {
            return;
        }

        _updateTimer = Mathf.Max(0.02f, UpdateInterval);
        if (!IsInstanceValid(_player) ||
            !_player.StompAssistEnabled ||
            _player.IsInsideTunnel)
        {
            ClearProjection();
            return;
        }

        BuildPlayerTrajectory();
        SelectTargetAlongTrajectory();
        UpdateTrajectoryVisual();
    }

    public override void _ExitTree()
    {
        ClearTargetCue();
        if (IsInstanceValid(_player))
        {
            _player.SetFirstStompGuideCandidate(
                null,
                false);
        }
    }

    private void BindTrajectoryMarkers()
    {
        MultiMeshInstance3D markerField =
            GetNode<MultiMeshInstance3D>(
                "TrajectoryMarkers");
        _markerInstances = markerField.Multimesh;
        int count = Mathf.Clamp(MarkerCount, 10, 30);
        _markerInstances.InstanceCount = count;
        _markerInstances.VisibleInstanceCount = 0;
        for (int index = 0; index < count; index++)
        {
            float radius = index % 4 == 0 ? 0.13f : 0.095f;
            float scale = radius / 0.095f;
            _markerInstances.SetInstanceTransform(
                index,
                new Transform3D(
                    Basis.Identity.Scaled(Vector3.One * scale),
                    Vector3.Zero));
            _markerInstances.SetInstanceCustomData(
                index,
                new Color(
                    PlayerPathColor.R,
                    PlayerPathColor.G,
                    PlayerPathColor.B,
                    0.0f));
        }
    }

    private void BuildPlayerTrajectory()
    {
        _trajectoryPoints.Clear();
        _trajectoryTimes.Clear();
        TrajectoryBlocked = false;

        bool grounded = _player.IsOnFloor();
        Vector3 startPosition =
            _player.GlobalPosition + Vector3.Up * 0.32f;
        Vector3 horizontalVelocity = new(
            _player.Velocity.X,
            0.0f,
            _player.Velocity.Z);
        _gravity = Mathf.Max(1.0f, _player.FallAcceleration);
        _launchVerticalSpeed = grounded
            ? _player.ProjectedJumpLaunchSpeed
            : _player.Velocity.Y;

        float horizon;
        if (grounded)
        {
            horizon =
                2.0f * _launchVerticalSpeed / _gravity + 0.08f;
        }
        else
        {
            float heightAboveGround =
                Mathf.Max(0.0f, _player.GlobalPosition.Y);
            float discriminant =
                _launchVerticalSpeed * _launchVerticalSpeed +
                2.0f * _gravity * heightAboveGround;
            horizon =
                (_launchVerticalSpeed +
                    Mathf.Sqrt(Mathf.Max(0.0f, discriminant))) /
                _gravity +
                0.08f;
        }

        horizon = Mathf.Clamp(horizon, 0.28f, 1.1f);
        int requestedCount = Mathf.Min(
            _markerInstances.InstanceCount,
            Mathf.Max(10, MarkerCount));
        Vector3 previousPoint = startPosition;
        _trajectoryPoints.Add(startPosition);
        _trajectoryTimes.Add(0.0f);

        for (int index = 1; index < requestedCount; index++)
        {
            float ratio = index / (float)(requestedCount - 1);
            float time = horizon * ratio;
            Vector3 point =
                startPosition +
                horizontalVelocity * time +
                Vector3.Up *
                    (_launchVerticalSpeed * time -
                        0.5f * _gravity * time * time);
            if (TryClipTrajectoryAtWorld(
                previousPoint,
                point,
                out Vector3 clippedPoint,
                out bool blocksForwardRoute))
            {
                _trajectoryPoints.Add(
                    clippedPoint + Vector3.Up * 0.035f);
                _trajectoryTimes.Add(time);
                // Landing on the floor ends this jump but does not make the
                // player's forward route wrong. Obstacles do.
                TrajectoryBlocked = blocksForwardRoute;
                break;
            }

            _trajectoryPoints.Add(point);
            _trajectoryTimes.Add(time);
            previousPoint = point;
        }
    }

    private bool TryClipTrajectoryAtWorld(
        Vector3 from,
        Vector3 to,
        out Vector3 clippedPoint,
        out bool blocksForwardRoute)
    {
        clippedPoint = to;
        blocksForwardRoute = false;
        if (!IsInsideTree() || from.IsEqualApprox(to))
        {
            return false;
        }

        PhysicsRayQueryParameters3D query =
            PhysicsRayQueryParameters3D.Create(
                from,
                to,
                WorldCollisionMask);
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid>
        {
            _player.GetRid()
        };
        Godot.Collections.Dictionary result =
            GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
        {
            return false;
        }

        clippedPoint = (Vector3)result["position"];
        if (result.TryGetValue("collider", out Variant colliderValue) &&
            colliderValue.AsGodotObject() is CollisionObject3D collider)
        {
            const uint obstacleLayer = 1u << 3;
            blocksForwardRoute =
                (collider.CollisionLayer & obstacleLayer) != 0;
        }

        return true;
    }

    private void SelectTargetAlongTrajectory()
    {
        Mob bestTarget = null;
        Mob directedComboTarget = _player.ComboRouteTarget;
        bool bestReachable = false;
        bool bestOnCourse = false;
        int bestMarkerIndex = -1;
        float bestScore = float.MaxValue;

        foreach (Node node in GetTree().GetNodesInGroup("mob"))
        {
            if (node is not Mob mob ||
                !IsValidTarget(mob))
            {
                continue;
            }

            Vector3 toMob =
                mob.GlobalPosition - _player.GlobalPosition;
            toMob.Y = 0.0f;
            if (toMob.LengthSquared() >
                TargetSearchRadius * TargetSearchRadius)
            {
                continue;
            }

            EvaluateTarget(
                mob,
                out bool reachable,
                out bool onCourse,
                out int closestIndex,
                out float minimumHorizontalMiss,
                out float trajectoryScore);
            bool isDirectedComboTarget =
                mob == directedComboTarget;
            if (!onCourse &&
                minimumHorizontalMiss > MaximumPathMiss &&
                !isDirectedComboTarget)
            {
                continue;
            }

            trajectoryScore += toMob.Length() * 0.012f;
            trajectoryScore -= mob.JumpOpportunityPriority;
            if (isDirectedComboTarget)
            {
                // Keep the authored combo choice visually stable while the
                // player's arc is being shortened or extended toward it.
                trajectoryScore -= 1.8f;
            }
            if (reachable)
            {
                trajectoryScore -= 0.62f;
            }
            else if (onCourse)
            {
                trajectoryScore -= 0.3f;
            }

            if (mob == CurrentTarget)
            {
                trajectoryScore -= 0.2f;
            }

            if (trajectoryScore < bestScore)
            {
                bestScore = trajectoryScore;
                bestTarget = mob;
                bestReachable = reachable;
                bestOnCourse = onCourse;
                bestMarkerIndex = closestIndex;
            }
        }

        if (CurrentTarget != bestTarget)
        {
            ClearTargetCue();
        }

        CurrentTarget = bestTarget;
        TargetReachable = bestReachable;
        TargetOnCourse = bestOnCourse;
        ClosestMarkerIndex = bestMarkerIndex;
        if (!IsInstanceValid(CurrentTarget))
        {
            _player.SetFirstStompGuideCandidate(
                null,
                false);
            return;
        }

        float markerProgress = ClosestMarkerIndex < 0
            ? 0.0f
            : ClosestMarkerIndex /
                (float)Mathf.Max(1, _trajectoryPoints.Count - 1);
        float urgency = TargetReachable
            ? Mathf.Lerp(1.0f, 0.7f, markerProgress)
            : TargetOnCourse
                ? 0.7f
                : 0.62f;
        CurrentTarget.SetJumpReachCue(
            TargetReachable
                ? Mob.JumpReachCue.Reachable
                : TargetOnCourse
                    ? Mob.JumpReachCue.OnCourse
                    : Mob.JumpReachCue.Unreachable,
            urgency);
        _player.SetFirstStompGuideCandidate(
            CurrentTarget,
            TargetReachable &&
                !TrajectoryBlocked);
    }

    private void EvaluateTarget(
        Mob mob,
        out bool reachable,
        out bool onCourse,
        out int closestIndex,
        out float minimumHorizontalMiss,
        out float trajectoryScore)
    {
        reachable = false;
        onCourse = false;
        closestIndex = -1;
        minimumHorizontalMiss = float.MaxValue;
        trajectoryScore = float.MaxValue;

        for (int index = 1;
            index < _trajectoryPoints.Count;
            index++)
        {
            float time = _trajectoryTimes[index];
            Vector3 predictedMobPosition =
                mob.GlobalPosition + mob.Velocity * time;
            Vector3 playerPosition = _trajectoryPoints[index];
            Vector2 horizontalOffset = new(
                predictedMobPosition.X - playerPosition.X,
                predictedMobPosition.Z - playerPosition.Z);
            float horizontalMiss = horizontalOffset.Length();
            float verticalGap =
                playerPosition.Y - predictedMobPosition.Y;
            float verticalVelocity =
                _launchVerticalSpeed - _gravity * time;
            bool descending = verticalVelocity < -0.35f;
            bool stompHeight =
                verticalGap >= 0.42f &&
                verticalGap <= 2.45f;
            bool reachesAtSample =
                descending &&
                stompHeight &&
                horizontalMiss <= StompHorizontalRadius;

            float verticalPenalty;
            if (!descending)
            {
                verticalPenalty = 0.75f;
            }
            else if (verticalGap < 0.42f)
            {
                verticalPenalty = 0.42f - verticalGap;
            }
            else if (verticalGap > 2.45f)
            {
                verticalPenalty =
                    (verticalGap - 2.45f) * 0.55f;
            }
            else
            {
                verticalPenalty = 0.0f;
            }

            float sampleScore =
                horizontalMiss /
                    Mathf.Max(0.1f, StompHorizontalRadius) +
                verticalPenalty +
                time * 0.08f;
            if (reachesAtSample)
            {
                sampleScore -= 0.8f;
            }

            if (sampleScore < trajectoryScore)
            {
                trajectoryScore = sampleScore;
                closestIndex = index;
            }

            minimumHorizontalMiss = Mathf.Min(
                minimumHorizontalMiss,
                horizontalMiss);
            reachable |= reachesAtSample;
        }

        if (reachable ||
            TrajectoryBlocked ||
            _trajectoryPoints.Count < 2)
        {
            return;
        }

        Vector3 horizontalVelocity = new(
            _player.Velocity.X,
            0.0f,
            _player.Velocity.Z);
        float playerSpeed = horizontalVelocity.Length();
        if (playerSpeed < 1.0f)
        {
            return;
        }

        Vector3 heading = horizontalVelocity / playerSpeed;
        float endTime = _trajectoryTimes[^1];
        Vector3 predictedTarget =
            mob.GlobalPosition + mob.Velocity * endTime;
        Vector3 targetOffset =
            predictedTarget - _trajectoryPoints[0];
        targetOffset.Y = 0.0f;
        float distanceAlongPath = targetOffset.Dot(heading);
        Vector3 lateralOffset =
            targetOffset - heading * distanceAlongPath;
        float arcTravelDistance =
            _trajectoryPoints[0]
                .DistanceTo(_trajectoryPoints[^1]);
        bool remainsAhead =
            distanceAlongPath >
                Mathf.Max(2.5f, arcTravelDistance - 1.0f);
        bool alignedWithRoute =
            lateralOffset.Length() <=
                Mathf.Max(2.55f, StompHorizontalRadius * 1.55f);
        bool closestAtArcEnd =
            closestIndex >= _trajectoryPoints.Count - 3;
        onCourse =
            remainsAhead &&
            alignedWithRoute &&
            closestAtArcEnd;
    }

    private static bool IsValidTarget(Mob mob)
    {
        return mob != null &&
            IsInstanceValid(mob) &&
            !mob.IsQueuedForDeletion() &&
            !mob.IsArenaAttackResolved &&
            !mob.IsPostArenaRetreating &&
            !mob.IsYieldingToTunnel;
    }

    private void UpdateTrajectoryVisual()
    {
        bool show =
            IsInstanceValid(CurrentTarget) &&
            _trajectoryPoints.Count > 1;
        Color targetColor = TargetReachable
            ? ReachableColor
            : TargetOnCourse
                ? OnCourseColor
                : UnreachableColor;
        bool greenCommitment =
            IsInstanceValid(_player) &&
            _player.FirstStompGuideTarget == CurrentTarget;
        VisibleMarkerCount = show
            ? Mathf.Min(
                _markerInstances.InstanceCount,
                _trajectoryPoints.Count)
            : 0;
        _markerInstances.VisibleInstanceCount =
            VisibleMarkerCount;

        for (int index = 0;
            index < VisibleMarkerCount;
            index++)
        {
            float baseRadius =
                index % 4 == 0 ? 0.13f : 0.095f;
            float markerScale =
                baseRadius / 0.095f *
                (greenCommitment ? 1.08f : 1.0f);
            _markerInstances.SetInstanceTransform(
                index,
                new Transform3D(
                    Basis.Identity.Scaled(
                        Vector3.One * markerScale),
                    _trajectoryPoints[index]));
            float progress =
                index /
                (float)Mathf.Max(1, VisibleMarkerCount - 1);
            float targetProximity = ClosestMarkerIndex < 0
                ? 0.0f
                : 1.0f -
                    Mathf.Clamp(
                        Mathf.Abs(index - ClosestMarkerIndex) / 4.0f,
                        0.0f,
                        1.0f);
            Color markerColor = PlayerPathColor.Lerp(
                targetColor,
                targetProximity * 0.78f);
            float alpha =
                Mathf.Lerp(0.24f, 0.68f, progress) *
                Mathf.Lerp(0.7f, 1.0f, targetProximity);
            if (greenCommitment)
            {
                alpha = Mathf.Min(0.82f, alpha * 1.16f);
            }
            _markerInstances.SetInstanceCustomData(
                index,
                new Color(
                    markerColor.R,
                    markerColor.G,
                    markerColor.B,
                    alpha));
        }
    }

    private void ClearProjection()
    {
        ClearTargetCue();
        if (IsInstanceValid(_player))
        {
            _player.SetFirstStompGuideCandidate(
                null,
                false);
        }
        _trajectoryPoints.Clear();
        _trajectoryTimes.Clear();
        ClosestMarkerIndex = -1;
        VisibleMarkerCount = 0;
        TrajectoryBlocked = false;
        if (_markerInstances != null)
        {
            _markerInstances.VisibleInstanceCount = 0;
        }
    }

    private void ClearTargetCue()
    {
        if (IsInstanceValid(CurrentTarget))
        {
            CurrentTarget.SetJumpReachCue(
                Mob.JumpReachCue.None,
                0.0f);
        }

        CurrentTarget = null;
        TargetReachable = false;
        TargetOnCourse = false;
    }
}
