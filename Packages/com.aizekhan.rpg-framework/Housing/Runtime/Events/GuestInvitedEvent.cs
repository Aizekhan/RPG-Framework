using System;
using RPGFramework.Events;

namespace RPGFramework.Housing.Runtime.Events
{
    /// <summary>
    /// Event raised when a guest is invited to a house with permissions.
    /// Published by <c>HousePermissions.GrantPermission</c> when an entity gets a new level above Banned
    /// (permissions created by HousingManager get its publisher). InvitedBy is Guid.Empty: the grant is not told who asked.
    /// </summary>
    public class GuestInvitedEvent : IEvent
    {
        /// <summary>
        /// ID of the house the guest was invited to.
        /// </summary>
        public Guid HouseId { get; }

        /// <summary>
        /// ID of the guest being invited.
        /// </summary>
        public Guid GuestId { get; }

        /// <summary>
        /// Permission level granted.
        /// </summary>
        public Data.PermissionLevel PermissionLevel { get; }

        /// <summary>
        /// Entity that sent the invitation.
        /// </summary>
        public Guid InvitedBy { get; }

        /// <summary>
        /// Timestamp of invitation.
        /// </summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// Create a new guest invited event.
        /// </summary>
        public GuestInvitedEvent(
            Guid houseId,
            Guid guestId,
            Data.PermissionLevel permissionLevel,
            Guid invitedBy)
        {
            HouseId = houseId;
            GuestId = guestId;
            PermissionLevel = permissionLevel;
            InvitedBy = invitedBy;
            Timestamp = DateTime.UtcNow;
        }
    }
}
