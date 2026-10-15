using Godot;
using System.Collections.Generic;

/// <summary>
/// Node-free contact memory. Collider discovery and physical reactions stay
/// in Player; this component guarantees debouncing and one reaction window
/// per enemy instance.
/// </summary>
public sealed class EnemyContactTracker
{
    private readonly Dictionary<ulong, float>
        _exposure = new();
    private readonly Dictionary<ulong, float>
        _nextReactionTime = new();
    private readonly HashSet<ulong>
        _observed = new();
    private readonly HashSet<ulong>
        _physical = new();
    private readonly List<ulong>
        _keysToRemove = new();

    public float Clock { get; private set; }
    public int ExposureCount => _exposure.Count;

    public void BeginFrame(float deltaSeconds)
    {
        Clock += Mathf.Max(
            0.0f,
            deltaSeconds);
        _observed.Clear();
        _physical.Clear();
    }

    public void Register(
        ulong instanceId,
        bool physicalContact)
    {
        _observed.Add(instanceId);
        if (physicalContact)
        {
            _physical.Add(instanceId);
        }
    }

    public bool IsPhysical(ulong instanceId)
    {
        return _physical.Contains(
            instanceId);
    }

    public bool AccumulateDanger(
        ulong instanceId,
        float deltaSeconds,
        float requiredContactTime)
    {
        float exposure =
            _exposure.GetValueOrDefault(
                instanceId) +
            Mathf.Max(
                0.0f,
                deltaSeconds);
        _exposure[instanceId] = exposure;
        return exposure >=
            Mathf.Max(
                0.0f,
                requiredContactTime);
    }

    public void ResetExposure(ulong instanceId)
    {
        _exposure.Remove(instanceId);
    }

    public bool TryBeginReaction(
        ulong instanceId,
        float cooldown)
    {
        if (_nextReactionTime.TryGetValue(
                instanceId,
                out float nextTime) &&
            nextTime > Clock)
        {
            return false;
        }

        _nextReactionTime[instanceId] =
            Clock +
            Mathf.Max(
                0.05f,
                cooldown);
        return true;
    }

    public void RemoveContact(ulong instanceId)
    {
        _exposure.Remove(instanceId);
    }

    public void ClearDanger()
    {
        _exposure.Clear();
    }

    public void Reset()
    {
        _exposure.Clear();
        _nextReactionTime.Clear();
        _observed.Clear();
        _physical.Clear();
        _keysToRemove.Clear();
        Clock = 0.0f;
    }

    public void EndFrame()
    {
        _keysToRemove.Clear();
        foreach (ulong instanceId in
            _exposure.Keys)
        {
            if (!_observed.Contains(
                    instanceId))
            {
                _keysToRemove.Add(
                    instanceId);
            }
        }
        foreach (ulong instanceId in
            _keysToRemove)
        {
            _exposure.Remove(instanceId);
        }

        _keysToRemove.Clear();
        foreach ((ulong instanceId,
            float nextReactionTime) in
            _nextReactionTime)
        {
            if (nextReactionTime <= Clock &&
                !_observed.Contains(
                    instanceId))
            {
                _keysToRemove.Add(
                    instanceId);
            }
        }
        foreach (ulong instanceId in
            _keysToRemove)
        {
            _nextReactionTime.Remove(
                instanceId);
        }
    }
}
