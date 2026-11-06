using System;
using System.Security.Cryptography;
using System.Text;

namespace RPGFramework.Telemetry
{
    /// <summary>
    /// Ids that tell players apart without naming them.
    /// </summary>
    public static class TelemetryIds
    {
        /// <summary>
        /// A salted SHA-256 of the player's own id (a save id, a platform account id): the same player always gets the
        /// same id under one salt, and the id does not lead back to the account. Keep the salt in the game, not in
        /// the events.
        /// </summary>
        public static string Pseudonymous(string playerId, string salt)
        {
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("A player id", nameof(playerId));
            if (string.IsNullOrEmpty(salt))
                throw new ArgumentException("A salt of the game's own", nameof(salt));
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(salt + ":" + playerId));
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                    text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
