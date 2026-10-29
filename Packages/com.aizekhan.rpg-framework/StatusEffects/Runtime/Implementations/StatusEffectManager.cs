using System;
using System.Collections.Generic;
using System.Linq;
using RPGFramework.ECS;
using RPGFramework.Events;

namespace RPGFramework.StatusEffects
{
    /// <summary>
    /// Implementation of IStatusEffectManager.
    /// ECS-based manager for status effects.
    /// With an event publisher, raises EffectApplied, EffectTick, EffectExpired and EffectRemoved
    /// (removal and expiry times are 0: those methods do not receive the current time).
    /// With an <see cref="IStatusEffectTarget"/>, hands the game what each effect does: stat modifiers while the
    /// effect is on (following its stacks), resource changes per tick, the combined movement multiplier and the
    /// controls (stun, silence...) in force. Without one, effects are only tracked and the queries still answer.
    /// </summary>
    public class StatusEffectManager : IStatusEffectManager
    {
        /// <summary>
        /// Ticks one <see cref="ProcessTicks"/> call catches up at most (a long frame or a pause); time beyond that
        /// is dropped instead of landing all at once.
        /// </summary>
        public const int MaxTicksPerCall = 10;

        private readonly IEntityManager _entityManager;
        private readonly Dictionary<string, IStatusEffect> _effectRegistry;
        private readonly IEventPublisher _events;
        private readonly IStatusEffectTarget _target;

        public StatusEffectManager(IEntityManager entityManager, IEventPublisher events = null, IStatusEffectTarget target = null)
        {
            _events = events;
            _target = target;
            _entityManager = entityManager;
            _effectRegistry = new Dictionary<string, IStatusEffect>();
        }

        /// <summary>
        /// Id under which an effect's stat modifier is handed to the target: one per effect and modifier position.
        /// </summary>
        public static string StatModifierId(string effectId, int modifierIndex)
        {
            return "status:" + effectId + ":" + modifierIndex;
        }

        /// <summary>
        /// Register an effect definition.
        /// </summary>
        /// <returns>False if an effect with this id is already registered; the first one is kept.</returns>
        public bool RegisterEffect(IStatusEffect effect)
        {
            if (effect == null)
                throw new ArgumentNullException(nameof(effect));
            if (string.IsNullOrEmpty(effect.Id))
                throw new ArgumentException("Effect ID cannot be null or empty", nameof(effect));

            if (_effectRegistry.ContainsKey(effect.Id))
                return false;

            _effectRegistry[effect.Id] = effect;
            return true;
        }

        /// <summary>
        /// Register an effect from data.
        /// </summary>
        /// <returns>False if an effect with this id is already registered; the first one is kept.</returns>
        public bool RegisterEffect(StatusEffectData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            return RegisterEffect(new StatusEffect(data));
        }

        public bool ApplyEffect(int entityId, string effectId, string sourceId, float currentTime) =>
            ApplyEffect(entityId, effectId, sourceId, currentTime, 1f);

        public bool ApplyEffect(int entityId, string effectId, string sourceId, float currentTime, float power, float? duration = null)
        {
            if (effectId == null || !_effectRegistry.TryGetValue(effectId, out var effect))
                return false;
            if (duration.HasValue && (float.IsNaN(duration.Value) || float.IsInfinity(duration.Value)))
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "A duration is seconds, or -1 for until removed");
            float lasts = duration ?? effect.Duration;

            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
            {
                activeEffects = ActiveEffectsData.CreateEmpty();
            }

            var before = CaptureMovementAndControls(entityId);

            // Check if effect already exists
            var existingEffect = activeEffects.GetEffect(effectId);
            StatusEffectInstance instance;
            if (existingEffect.HasValue)
            {
                instance = new StatusEffectInstance(existingEffect.Value, effect);
                int stacksBefore = instance.CurrentStacks;
                float powerBefore = instance.Power;

                // Stackable effects gain a stack; either way the duration starts over, and the stronger power stays
                if (effect.IsStackable)
                    instance.AddStack();
                instance.Refresh(lasts);
                instance.SetPower(Math.Max(powerBefore, power > 0f ? power : 1f));

                activeEffects.RemoveEffect(effectId);
                activeEffects.AddEffect(instance.ToData());
                _entityManager.AddComponent(entityId, activeEffects);

                if (instance.CurrentStacks != stacksBefore || instance.Power != powerBefore)
                {
                    RemoveStatModifiers(entityId, effect);
                    ApplyStatModifiers(entityId, effect, instance.CurrentStacks, instance.Power);
                }
            }
            else
            {
                instance = new StatusEffectInstance(effect, entityId, sourceId, currentTime);
                instance.Refresh(lasts);
                instance.SetPower(power);
                activeEffects.AddEffect(instance.ToData());
                _entityManager.AddComponent(entityId, activeEffects);
                ApplyStatModifiers(entityId, effect, instance.CurrentStacks, instance.Power);
            }

            // Effects that never tick change their resources once per application
            if (effect.TickInterval <= 0f)
                ApplyResourceChanges(entityId, effect, 1, instance.Power);

            NotifyMovementAndControls(entityId, before);
            _events?.Publish(new EffectAppliedEvent(entityId, effectId, sourceId, currentTime, instance.CurrentStacks));
            return true;
        }

        public bool RemoveEffect(int entityId, string effectId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return false;

            if (!activeEffects.HasEffect(effectId))
                return false;

            var before = CaptureMovementAndControls(entityId);
            activeEffects.RemoveEffect(effectId);
            _entityManager.AddComponent(entityId, activeEffects);

            if (_effectRegistry.TryGetValue(effectId, out var effect))
                RemoveStatModifiers(entityId, effect);
            NotifyMovementAndControls(entityId, before);

            _events?.Publish(new EffectRemovedEvent(entityId, effectId, 0f, false));
            return true;
        }

        public void Reapply(int entityId)
        {
            if (_target == null)
                return;
            foreach (var (effect, stacks, power) in ActiveDefinitions(entityId))
            {
                RemoveStatModifiers(entityId, effect);
                ApplyStatModifiers(entityId, effect, stacks, power);
            }
            // As if nothing was in force: the target hears every movement change and control there is
            _target.SetMovementMultiplier(entityId, GetMovementMultiplier(entityId));
            foreach (var control in GetActiveControls(entityId))
                _target.SetControl(entityId, control, true);
        }

        public void RemoveAllEffects(int entityId)
        {
            // An entity that is gone has nothing left to remove
            if (!_entityManager.Exists(entityId))
                return;

            List<string> removedIds = null;
            if (_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var existing)
                && existing.ActiveInstances != null)
            {
                removedIds = existing.ActiveInstances.Select(i => i.EffectId).ToList();
            }

            var before = CaptureMovementAndControls(entityId);
            var activeEffects = ActiveEffectsData.CreateEmpty();
            _entityManager.AddComponent(entityId, activeEffects);

            if (removedIds == null)
                return;

            foreach (var effectId in removedIds)
            {
                if (_effectRegistry.TryGetValue(effectId, out var effect))
                    RemoveStatModifiers(entityId, effect);
            }
            NotifyMovementAndControls(entityId, before);

            if (_events != null)
            {
                foreach (var effectId in removedIds)
                    _events.Publish(new EffectRemovedEvent(entityId, effectId, 0f, false));
            }
        }

        public void UpdateEffects(int entityId, float deltaTime)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return;

            if (activeEffects.ActiveInstances == null || activeEffects.ActiveInstances.Count == 0)
                return;

            var before = CaptureMovementAndControls(entityId);
            var instancesToUpdate = new List<StatusEffectInstanceData>(activeEffects.ActiveInstances);
            List<IStatusEffect> expired = null;

            foreach (var instanceData in instancesToUpdate)
            {
                if (!_effectRegistry.TryGetValue(instanceData.EffectId, out var effect))
                    continue;

                var instance = new StatusEffectInstance(instanceData, effect);
                instance.Update(deltaTime);

                // Update the instance in ActiveEffects
                activeEffects.RemoveEffect(instanceData.EffectId);
                if (!instance.IsExpired)
                {
                    activeEffects.AddEffect(instance.ToData());
                }
                else
                {
                    if (expired == null)
                        expired = new List<IStatusEffect>();
                    expired.Add(effect);
                }
            }

            // Remove expired effects
            activeEffects.RemoveExpired();
            _entityManager.AddComponent(entityId, activeEffects);

            if (expired == null)
                return;

            foreach (var effect in expired)
                RemoveStatModifiers(entityId, effect);
            NotifyMovementAndControls(entityId, before);

            if (_events != null)
            {
                foreach (var effect in expired)
                {
                    _events.Publish(new EffectExpiredEvent(entityId, effect.Id, 0f));
                    _events.Publish(new EffectRemovedEvent(entityId, effect.Id, 0f, true));
                }
            }
        }

        public IReadOnlyList<IStatusEffectInstance> GetActiveEffects(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return new List<IStatusEffectInstance>();

            if (activeEffects.ActiveInstances == null)
                return new List<IStatusEffectInstance>();

            var instances = new List<IStatusEffectInstance>();
            foreach (var instanceData in activeEffects.ActiveInstances)
            {
                if (_effectRegistry.TryGetValue(instanceData.EffectId, out var effect))
                {
                    instances.Add(new StatusEffectInstance(instanceData, effect));
                }
            }

            return instances;
        }

        /// <summary>
        /// Who put the effect on the entity (its source id), or null when it is not on it. Allocates nothing.
        /// </summary>
        public string SourceOf(int entityId, string effectId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return null;
            var instances = activeEffects.ActiveInstances;
            if (instances == null)
                return null;
            for (int i = 0; i < instances.Count; i++)
            {
                if (instances[i].EffectId == effectId)
                    return instances[i].SourceId;
            }
            return null;
        }

        public bool HasEffect(int entityId, string effectId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return false;

            return activeEffects.HasEffect(effectId);
        }

        /// <summary>
        /// Get an effect definition by ID.
        /// </summary>
        public IStatusEffect GetEffect(string effectId)
        {
            return _effectRegistry.TryGetValue(effectId, out var effect) ? effect : null;
        }

        /// <summary>
        /// Combined movement multiplier from every Movement modifier on the entity (1 = normal, 0 = rooted).
        /// If any modifier is an Override, the smallest override wins (a root beats a sprint); otherwise
        /// (1 + sum of Add values) x product of Multiply values, never below 0. Stacks count as in
        /// <see cref="IStatusEffectTarget.ApplyStatModifier"/>. The modifier's TargetId is not used.
        /// </summary>
        public float GetMovementMultiplier(int entityId)
        {
            float added = 0f;
            float multiplied = 1f;
            float? overridden = null;

            foreach (var (effect, stacks, power) in ActiveDefinitions(entityId))
            {
                foreach (var modifier in effect.Modifiers)
                {
                    if (modifier.Type != ModifierType.Movement)
                        continue;

                    float value = StackedValue(modifier, stacks, power);
                    switch (modifier.Operation)
                    {
                        case ModifierOperation.Add:
                            added += value;
                            break;
                        case ModifierOperation.Multiply:
                            multiplied *= value;
                            break;
                        case ModifierOperation.Override:
                            overridden = overridden.HasValue ? Math.Min(overridden.Value, value) : value;
                            break;
                    }
                }
            }

            float result = overridden ?? (1f + added) * multiplied;
            return Math.Max(0f, result);
        }

        /// <summary>
        /// True while any active effect on the entity has a Control modifier with this TargetId (e.g. "Stun").
        /// </summary>
        public bool IsControlled(int entityId, string controlId)
        {
            return GetActiveControls(entityId).Contains(controlId);
        }

        /// <summary>
        /// Every control (TargetId of a Control modifier) in force on the entity. Value and operation of Control
        /// modifiers are not used: the control holds for as long as the effect is on.
        /// </summary>
        public IReadOnlyCollection<string> GetActiveControls(int entityId)
        {
            var controls = new HashSet<string>();
            foreach (var (effect, _, _) in ActiveDefinitions(entityId))
            {
                foreach (var modifier in effect.Modifiers)
                {
                    if (modifier.Type == ModifierType.Control && !string.IsNullOrEmpty(modifier.TargetId))
                        controls.Add(modifier.TargetId);
                }
            }
            return controls;
        }

        /// <summary>
        /// Process DoT/HoT ticks for an entity.
        /// Damage and Healing are reported (callback and EffectTick event) for the game to apply. Add is a flat
        /// amount per stack; Multiply and Override work on the current value of the TargetId read through the
        /// target (e.g. Damage x0.9 on "Health" deals 10% of current health) and are skipped if it cannot be read.
        /// Resource modifiers are handed to the target as a change of the resource, using the same rules.
        /// Ticks missed since the last call (a long frame) land together, up to <see cref="MaxTicksPerCall"/>, reported
        /// as one total; the next tick stays on the effect's own rhythm.
        /// </summary>
        public void ProcessTicks(int entityId, float currentTime, System.Action<int, string, float, float> onTick = null)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects))
                return;

            if (activeEffects.ActiveInstances == null || activeEffects.ActiveInstances.Count == 0)
                return;

            var instancesToUpdate = new List<StatusEffectInstanceData>(activeEffects.ActiveInstances);
            List<EffectTickEvent> ticks = null;

            foreach (var instanceData in instancesToUpdate)
            {
                if (!_effectRegistry.TryGetValue(instanceData.EffectId, out var effect))
                    continue;

                var instance = new StatusEffectInstance(instanceData, effect);

                if (instance.ShouldTick(currentTime))
                {
                    int due = (int)Math.Min(MaxTicksPerCall, Math.Floor((currentTime - instance.LastTickTime) / effect.TickInterval));
                    due = Math.Max(1, due);
                    float next = instance.LastTickTime + due * effect.TickInterval;
                    // Further behind than the cap: start the rhythm from now
                    instance.UpdateLastTickTime(currentTime - next >= effect.TickInterval ? currentTime : next);

                    float totalDamage = 0f;
                    float totalHealing = 0f;

                    for (int tick = 0; tick < due; tick++)
                    {
                        foreach (var modifier in effect.Modifiers)
                        {
                            if (modifier.Type == ModifierType.Damage)
                            {
                                // Damage is how far the value goes down
                                if (TryGetChange(entityId, modifier, instance.CurrentStacks, instance.Power, -1f, out float change))
                                    totalDamage += Math.Max(0f, -change);
                            }
                            else if (modifier.Type == ModifierType.Healing)
                            {
                                if (TryGetChange(entityId, modifier, instance.CurrentStacks, instance.Power, 1f, out float change))
                                    totalHealing += Math.Max(0f, change);
                            }
                        }

                        ApplyResourceChanges(entityId, effect, instance.CurrentStacks, instance.Power);
                    }

                    if (totalDamage > 0f || totalHealing > 0f)
                    {
                        onTick?.Invoke(entityId, effect.Id, totalDamage, totalHealing);

                        if (_events != null)
                        {
                            if (ticks == null)
                                ticks = new List<EffectTickEvent>();
                            ticks.Add(new EffectTickEvent(entityId, effect.Id, totalDamage, totalHealing, currentTime));
                        }
                    }

                    // Update the instance in ActiveEffects
                    activeEffects.RemoveEffect(instanceData.EffectId);
                    activeEffects.AddEffect(instance.ToData());
                }
            }

            _entityManager.AddComponent(entityId, activeEffects);

            if (ticks != null)
            {
                foreach (var tick in ticks)
                    _events.Publish(tick);
            }
        }

        // ---- What effects hand to the target ----

        // Add values grow with each stack, Multiply values compound, an Override is the same at any stack count
        private static float StackedValue(IEffectModifier modifier, int stacks, float power)
        {
            // The power counts like stacks: 2 doubles an Add value and squares a Multiply value
            float count = Math.Max(1, stacks) * (power > 0f ? power : 1f);
            switch (modifier.Operation)
            {
                case ModifierOperation.Add:
                    return modifier.Value * count;
                case ModifierOperation.Multiply:
                    return (float)Math.Pow(modifier.Value, count);
                default:
                    return modifier.Value;
            }
        }

        // How much one application of the modifier changes its TargetId. addSign turns a flat Add amount into a
        // change (damage amounts go down, healing and resources as authored). Multiply and Override need the
        // current value from the target.
        private bool TryGetChange(int entityId, IEffectModifier modifier, int stacks, float power, float addSign, out float change)
        {
            change = 0f;
            if (modifier.Operation == ModifierOperation.Add)
            {
                change = addSign * StackedValue(modifier, stacks, power);
                return true;
            }

            if (_target == null || !_target.TryGetResource(entityId, modifier.TargetId, out float current))
                return false;

            change = StackedValue(modifier, stacks, power) * (modifier.Operation == ModifierOperation.Multiply ? current : 1f)
                     - current;
            return true;
        }

        private void ApplyResourceChanges(int entityId, IStatusEffect effect, int stacks, float power)
        {
            if (_target == null)
                return;

            foreach (var modifier in effect.Modifiers)
            {
                if (modifier.Type != ModifierType.Resource)
                    continue;

                if (TryGetChange(entityId, modifier, stacks, power, 1f, out float change) && change != 0f)
                    _target.ChangeResource(entityId, modifier.TargetId, change);
            }
        }

        private void ApplyStatModifiers(int entityId, IStatusEffect effect, int stacks, float power)
        {
            if (_target == null)
                return;

            for (int i = 0; i < effect.Modifiers.Count; i++)
            {
                var modifier = effect.Modifiers[i];
                if (modifier.Type == ModifierType.Stat)
                {
                    _target.ApplyStatModifier(entityId, StatModifierId(effect.Id, i), modifier.TargetId,
                        modifier.Operation, StackedValue(modifier, stacks, power));
                }
            }
        }

        private void RemoveStatModifiers(int entityId, IStatusEffect effect)
        {
            if (_target == null)
                return;

            for (int i = 0; i < effect.Modifiers.Count; i++)
            {
                if (effect.Modifiers[i].Type == ModifierType.Stat)
                    _target.RemoveStatModifier(entityId, StatModifierId(effect.Id, i));
            }
        }

        private IEnumerable<(IStatusEffect effect, int stacks, float power)> ActiveDefinitions(int entityId)
        {
            if (!_entityManager.TryGetComponent<ActiveEffectsData>(entityId, out var activeEffects)
                || activeEffects.ActiveInstances == null)
                yield break;

            foreach (var instance in activeEffects.ActiveInstances)
            {
                if (_effectRegistry.TryGetValue(instance.EffectId, out var effect))
                    yield return (effect, instance.CurrentStacks, instance.Power > 0f ? instance.Power : 1f);
            }
        }

        // Movement and controls are worked out from all effects together, so the target is told only what changed
        private (float movement, IReadOnlyCollection<string> controls) CaptureMovementAndControls(int entityId)
        {
            if (_target == null)
                return (1f, null);

            return (GetMovementMultiplier(entityId), GetActiveControls(entityId));
        }

        private void NotifyMovementAndControls(int entityId, (float movement, IReadOnlyCollection<string> controls) before)
        {
            if (_target == null)
                return;

            float movement = GetMovementMultiplier(entityId);
            if (movement != before.movement)
                _target.SetMovementMultiplier(entityId, movement);

            var controls = GetActiveControls(entityId);
            foreach (var control in before.controls)
            {
                if (!controls.Contains(control))
                    _target.SetControl(entityId, control, false);
            }
            foreach (var control in controls)
            {
                if (!before.controls.Contains(control))
                    _target.SetControl(entityId, control, true);
            }
        }
    }
}
