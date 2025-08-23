using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PolyMod.modApi
{
    public static class Input
    {
        private class Keybind
        {
            public List<KeyCode> Keys { get; set; }
            public Action Action { get; set; }
        }

        private static readonly List<Keybind> keybinds = new();

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.Update))]
        private static void GameManager_Update()
        {
            foreach (var keybind in keybinds)
            {
                if (keybind.Keys == null || !keybind.Keys.Any()) continue;

                var mainKey = keybind.Keys.Last();
                var modifiers = keybind.Keys.Take(keybind.Keys.Count - 1);

                if (UnityEngine.Input.GetKeyDown(mainKey) && modifiers.All(UnityEngine.Input.GetKey))
                {
                    try
                    {
                        keybind.Action?.Invoke();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[PolyMod.Input] Error executing keybind action: {e}");
                    }
                }
            }
        }

        /// <summary>
        /// Registers an action to be called when a key combination is pressed.
        /// The last key in the list is the main action key (triggers on KeyDown),
        /// while the preceding keys are treated as modifiers (must be held down).
        /// </summary>
        /// <param name="keys">The list of keys for the combination.</param>
        /// <param name="action">The action to execute.</param>
        public static void On(List<KeyCode> keys, Action action)
        {
            if (keys == null || !keys.Any()) return;
            keybinds.Add(new Keybind { Keys = keys, Action = action });
        }

        /// <summary>
        /// Registers an action to be called when a key is pressed.
        /// </summary>
        /// <param name="key">The key to watch.</param>
        /// <param name="action">The action to execute.</param>
        public static void On(KeyCode key, Action action)
        {
            On(new List<KeyCode> { key }, action);
        }
        /// <summary>
        /// Clears all registered keybinds.
        /// </summary>
        internal static void Clear()
        {
            keybinds.Clear();
        }

        /// <summary>
        /// Registers an action to be called when a key combination is pressed.
        /// The last key is the main action key (triggers on KeyDown),
        /// while the preceding keys are treated as modifiers (must be held down).
        /// </summary>
        /// <param name="action">The action to execute.</param>
        /// <param name="keys">The keys for the combination.</param>
        public static void On(Action action, params KeyCode[] keys)
        {
            if (keys == null || !keys.Any()) return;
            On(keys.ToList(), action);
        }
    }
}
