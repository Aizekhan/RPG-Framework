using System;
using System.Collections.Generic;
using RPGFramework.Events;

namespace RPGFramework.Interaction
{
    /// <summary>
    /// <see cref="IInteractionService"/> that publishes <see cref="InteractedEvent"/> on the event bus.
    /// </summary>
    public sealed class InteractionService : IInteractionService
    {
        private readonly IEventPublisher _events;
        private readonly List<InteractionPoint> _points = new List<InteractionPoint>();
        private readonly Dictionary<string, Func<int, InteractionPoint, bool>> _rules =
            new Dictionary<string, Func<int, InteractionPoint, bool>>(StringComparer.Ordinal);

        /// <param name="events">Where <see cref="InteractedEvent"/> goes; null publishes nothing.</param>
        public InteractionService(IEventPublisher events = null)
        {
            _events = events;
        }

        public Func<int, InteractionPoint, float> Distance { get; set; }

        public IReadOnlyCollection<InteractionPoint> Points => _points;

        public void Add(InteractionPoint point)
        {
            if (point == null)
                throw new ArgumentNullException(nameof(point));
            int index = IndexOf(point.Id);
            if (index >= 0)
                _points[index] = point;
            else
                _points.Add(point);
        }

        public bool Remove(string pointId)
        {
            int index = IndexOf(pointId);
            if (index < 0)
                return false;
            _points.RemoveAt(index);
            return true;
        }

        public InteractionPoint Get(string pointId)
        {
            int index = IndexOf(pointId);
            return index >= 0 ? _points[index] : null;
        }

        public void Clear() => _points.Clear();

        public void SetRule(string kind, Func<int, InteractionPoint, bool> allowed)
        {
            if (string.IsNullOrEmpty(kind))
                throw new ArgumentException("A rule is for a kind of place", nameof(kind));
            if (allowed == null)
                _rules.Remove(kind);
            else
                _rules[kind] = allowed;
        }

        public bool CanReach(int playerId, InteractionPoint point) =>
            point != null && point.Enabled && InReach(playerId, point, out _);

        public InteractionPoint NearestFor(int playerId)
        {
            InteractionPoint nearest = null;
            float best = float.PositiveInfinity;
            foreach (var point in _points)
            {
                if (point.Enabled && InReach(playerId, point, out float distance) && (nearest == null || distance < best))
                {
                    nearest = point;
                    best = distance;
                }
            }
            return nearest;
        }

        public InteractionOutcome Interact(int playerId, string pointId = null)
        {
            var point = pointId == null ? NearestFor(playerId) : Get(pointId);
            if (point == null)
                return InteractionOutcome.NotFound;
            if (!point.Enabled)
                return InteractionOutcome.Closed;
            if (!InReach(playerId, point, out _))
                return InteractionOutcome.TooFar;
            if (_rules.TryGetValue(point.Kind, out var allowed) && !allowed(playerId, point))
                return InteractionOutcome.Refused;
            if (point.Once)
                point.Enabled = false;
            _events?.Publish(new InteractedEvent(playerId, point.Id, point.Kind));
            return InteractionOutcome.Done;
        }

        // NaN (cannot be measured) fails the comparison, so it is out of reach
        private bool InReach(int playerId, InteractionPoint point, out float distance)
        {
            distance = Distance?.Invoke(playerId, point) ?? 0f;
            return distance <= point.Radius;
        }

        private int IndexOf(string pointId)
        {
            if (pointId == null)
                return -1;
            for (int i = 0; i < _points.Count; i++)
            {
                if (_points[i].Id == pointId)
                    return i;
            }
            return -1;
        }
    }
}
