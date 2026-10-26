using Unity.Services.Multiplayer;

namespace RPGFramework.Networking.UnityRelay
{
    /// <summary>
    /// Unity Multiplayer Services errors in words a player understands.
    /// </summary>
    public static class RelayErrors
    {
        public const string NotSetUp =
            "Internet play is not set up in this project: in the Unity Editor open Edit > Project Settings > Services, " +
            "link the project to a Unity Gaming Services project, and keep anonymous sign-in on.";

        public const string Offline = "Could not reach Unity's online services. Check the internet connection and try again.";

        /// <param name="error">What the service reported.</param>
        /// <param name="code">The room code typed, when joining.</param>
        public static string Explain(SessionError error, string code = null)
        {
            string room = string.IsNullOrEmpty(code) ? "That game" : $"The game {code}";
            switch (error)
            {
                case SessionError.SessionNotFound:
                case SessionError.InvalidSessionIdentifier:
                case SessionError.AllocationNotFound:
                    return string.IsNullOrEmpty(code) ? "No game with that code." : $"No game with the code {code}. Check it with the host.";
                case SessionError.SessionDeleted:
                    return $"{room} has ended.";
                case SessionError.Forbidden:
                    return $"{room} is full or closed.";
                case SessionError.NotAuthorized:
                    return NotSetUp;
                case SessionError.RateLimitExceeded:
                    return "Too many attempts in a short time. Wait a few seconds and try again.";
                case SessionError.NetworkSetupFailed:
                case SessionError.InvalidNetworkConfig:
                case SessionError.TransportInvalid:
                    return "The relay connection could not be set up. Try again; if it keeps failing, host a new game.";
                default:
                    return $"Unity's online services refused ({error}). Try again.";
            }
        }
    }
}
