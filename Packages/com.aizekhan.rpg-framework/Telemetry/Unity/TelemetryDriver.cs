using UnityEngine;

namespace RPGFramework.Telemetry.Unity
{
    /// <summary>
    /// Lets real time reach the telemetry client every frame (a paused game still sends), sends what is waiting when
    /// the app goes to the background and ends the session on quit (waiting up to <see cref="QuitWaitMilliseconds"/>
    /// for the last batch).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class TelemetryDriver : MonoBehaviour
    {
        public const int QuitWaitMilliseconds = 1500;

        public TelemetryClient Client { get; set; }

        private void Update() => Client?.Update(Time.unscaledDeltaTime);

        private void OnApplicationPause(bool paused)
        {
            if (paused)
                Client?.Flush();
        }

        private void OnApplicationQuit()
        {
            if (Client == null)
                return;
            Client.EndSession();
            Client.WaitForSending(QuitWaitMilliseconds);
        }
    }
}
