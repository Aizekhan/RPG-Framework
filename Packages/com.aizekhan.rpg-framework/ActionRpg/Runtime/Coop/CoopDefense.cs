using System.IO;
using RPGFramework.Combat;
using RPGFramework.Events;
using RPGFramework.Networking;

namespace RPGFramework.ActionRpg.Coop
{
    /// <summary>
    /// Who decides whether an attack on a joined player is dodged or blocked.
    /// </summary>
    public enum DefenseAuthority
    {
        /// <summary>
        /// The host's rules: the attack lands as the host sees it (the default).
        /// </summary>
        Host = 0,

        /// <summary>
        /// The player's own machine, which sees its own timing without lag: the host announces the attack
        /// (<see cref="CoopIncomingAttackMessage"/>), the player answers (<see cref="CoopDefenseResultMessage"/>) within
        /// <see cref="CoopHost.DefenseWindow"/> of the strike, or it lands. A friendly trust: a modified client could
        /// dodge everything, which suits co-op among friends, not ranked play.
        /// </summary>
        Owner = 1
    }

    /// <summary>
    /// How a player met an attack.
    /// </summary>
    public enum DefenseOutcome : byte
    {
        Hit = 0,

        /// <summary>
        /// Blocked: the damage is multiplied by the answer's multiplier (0 = all of it stopped).
        /// </summary>
        Blocked = 1,

        Dodged = 2
    }

    /// <summary>
    /// Host → the attacked player's machine: an attack will land in <see cref="HitIn"/> seconds; answer with a
    /// <see cref="CoopDefenseResultMessage"/>.
    /// </summary>
    public sealed class CoopIncomingAttackMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.IncomingAttackId;
        public int AttackId;
        public int AttackerId;
        public int TargetId;
        public float HitIn;
        public int Tags;
        public int SourceId;

        public void Write(BinaryWriter writer)
        {
            writer.Write(AttackId);
            writer.Write(AttackerId);
            writer.Write(TargetId);
            writer.Write(HitIn);
            writer.Write(Tags);
            writer.Write(SourceId);
        }

        public void Read(BinaryReader reader)
        {
            AttackId = reader.ReadInt32();
            AttackerId = reader.ReadInt32();
            TargetId = reader.ReadInt32();
            HitIn = reader.ReadSingle();
            Tags = reader.ReadInt32();
            SourceId = reader.ReadInt32();
        }
    }

    /// <summary>
    /// The attacked player → host: how they met an announced attack.
    /// </summary>
    public sealed class CoopDefenseResultMessage : NetworkMessage, INetworkPayload
    {
        public override int MessageTypeId => CoopProtocol.DefenseResultId;
        public int AttackId;
        public DefenseOutcome Outcome;

        /// <summary>
        /// Share of the damage that still lands when <see cref="DefenseOutcome.Blocked"/> (0..1).
        /// </summary>
        public float DamageMultiplier;

        public void Write(BinaryWriter writer)
        {
            writer.Write(AttackId);
            writer.Write((byte)Outcome);
            writer.Write(DamageMultiplier);
        }

        public void Read(BinaryReader reader)
        {
            AttackId = reader.ReadInt32();
            Outcome = (DefenseOutcome)reader.ReadByte();
            DamageMultiplier = reader.ReadSingle();
        }
    }

    /// <summary>
    /// On the attacked player's machine: an attack is coming (<see cref="DefenseAuthority.Owner"/>). The game checks
    /// its dodge or block and answers with <see cref="CoopClient.Defend"/>; no answer means the attack lands.
    /// </summary>
    public readonly struct IncomingAttackEvent : IEvent
    {
        public IncomingAttackEvent(int attackId, int attackerId, int targetId, float hitIn, AttackContext context)
        {
            AttackId = attackId;
            AttackerId = attackerId;
            TargetId = targetId;
            HitIn = hitIn;
            Context = context;
        }

        public int AttackId { get; }
        public int AttackerId { get; }
        public int TargetId { get; }

        /// <summary>
        /// Seconds until the strike, as the host announced it (lag already eats into this).
        /// </summary>
        public float HitIn { get; }

        public AttackContext Context { get; }
    }

    /// <summary>
    /// On the host: an announced attack was settled (answered by the player, or landed when no answer came in time).
    /// </summary>
    public readonly struct DefenseResolvedEvent : IEvent
    {
        public DefenseResolvedEvent(int attackId, int attackerId, int targetId, DefenseOutcome outcome, bool answered, float damage)
        {
            AttackId = attackId;
            AttackerId = attackerId;
            TargetId = targetId;
            Outcome = outcome;
            Answered = answered;
            Damage = damage;
        }

        public int AttackId { get; }
        public int AttackerId { get; }
        public int TargetId { get; }
        public DefenseOutcome Outcome { get; }

        /// <summary>
        /// False when the player's machine did not answer in time and the attack landed.
        /// </summary>
        public bool Answered { get; }

        public float Damage { get; }
    }
}
