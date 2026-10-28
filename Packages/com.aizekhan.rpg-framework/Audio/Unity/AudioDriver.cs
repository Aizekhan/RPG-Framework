using System;
using UnityEngine;

namespace RPGFramework.Audio.Unity
{
    /// <summary>
    /// Lets real time reach the audio service every frame (a paused game still clicks and plays music).
    /// </summary>
    [AddComponentMenu("")]
    public sealed class AudioDriver : MonoBehaviour
    {
        public Action<float> Tick { get; set; }

        private void Update() => Tick?.Invoke(Time.unscaledDeltaTime);
    }
}
