using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RPGFramework.UI;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RPGFramework.ActionRpg.PlayTests
{
    /// <summary>
    /// Panels built from code inside a screen that is not active yet (as <c>CodeViewFactory</c> makes them): their
    /// Awake runs only when the screen opens, which EditMode tests never see.
    /// </summary>
    public sealed class UIViewBasePlayTests
    {
        private readonly List<Object> _cleanup = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _cleanup)
            {
                if (obj != null)
                    Object.Destroy(obj);
            }
            _cleanup.Clear();
        }

        private GameObject InactiveScreen()
        {
            var screen = new GameObject("Screen", typeof(RectTransform));
            _cleanup.Add(screen);
            screen.SetActive(false);
            return screen;
        }

        private static PlayPanel PanelIn(GameObject screen, string name)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(screen.transform, false);
            return child.AddComponent<PlayPanel>();
        }

        [UnityTest]
        public IEnumerator APanelShownInsideAnInactiveScreen_StaysShownWhenTheScreenOpens()
        {
            var screen = InactiveScreen();
            var shown = PanelIn(screen, "Stats");
            shown.Show();
            var untouched = PanelIn(screen, "Untouched");

            screen.SetActive(true);
            yield return null;

            Assert.That(shown.IsVisible, Is.True, "shown before its Awake: stays shown");
            Assert.That(untouched.IsVisible, Is.False, "never shown: hides itself on Awake as before");
        }

        [UnityTest]
        public IEnumerator StartHidden_CanBeTurnedOffFromCode()
        {
            var screen = InactiveScreen();
            var panel = PanelIn(screen, "Hud");
            Assert.That(panel.StartHidden, Is.True);
            panel.StartHidden = false;

            screen.SetActive(true);
            yield return null;

            Assert.That(panel.IsVisible, Is.True);
        }
    }
}
