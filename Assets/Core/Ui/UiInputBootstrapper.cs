using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Century.Core.Ui
{
    /// <summary>
    /// Guarantees the scene has an EventSystem so UI Toolkit panels receive pointer input.
    /// </summary>
    /// <remarks>
    /// This exists because the failure it prevents is invisible. UI Toolkit renders perfectly well
    /// without an EventSystem — panels draw, styles apply, text updates — but no element receives a
    /// single pointer event. The result is an interface that looks completely correct and is entirely
    /// inert, with nothing in the console because nothing is in an error state.
    ///
    /// The input module is resolved by name rather than by assembly reference, so this works whether
    /// the project is on the Input System package, the legacy input manager, or both, without Core
    /// needing to depend on the Input System assembly.
    /// </remarks>
    public static class UiInputBootstrapper
    {
        private const string InputSystemModuleType =
            "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem";

        /// <summary>
        /// Creates an EventSystem if the scene has none. Safe to call repeatedly and from any scene.
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

#if UNITY_2023_1_OR_NEWER
            EventSystem existing = UnityEngine.Object.FindFirstObjectByType<EventSystem>();
#else
            EventSystem existing = UnityEngine.Object.FindObjectOfType<EventSystem>();
#endif
            if (existing != null) return;

            var host = new GameObject("EventSystem (auto)");
            host.AddComponent<EventSystem>();

            string moduleName = AttachInputModule(host);

            Debug.Log(
                $"[UiInput] No EventSystem was present, so one was created automatically with " +
                $"{moduleName}. UI Toolkit cannot receive clicks without it. Add a permanent " +
                "EventSystem via GameObject > UI > Event System to silence this.");
        }

        /// <summary>
        /// Attaches whichever input module the project can actually use. Returns its name for logging.
        /// </summary>
        private static string AttachInputModule(GameObject host)
        {
            Type inputSystemModule = Type.GetType(InputSystemModuleType);

            if (inputSystemModule != null)
            {
                Component module = host.AddComponent(inputSystemModule);

                // Critical: a module added at runtime has no actions asset, so it routes no pointer
                // input and every UI Toolkit button is silently inert. AssignDefaultActions wires the
                // built-in UI action map (point, click, scroll...). Without this call, any scene that
                // has no hand-placed EventSystem — the Overmap and Battle scenes both lack one — looks
                // completely correct and receives not one click.
                MethodInfo assignDefaults =
                    inputSystemModule.GetMethod("AssignDefaultActions", Type.EmptyTypes);
                if (assignDefaults == null)
                {
                    Debug.LogWarning(
                        "[UiInput] InputSystemUIInputModule has no AssignDefaultActions on this Input " +
                        "System version. Add a permanent EventSystem (GameObject > UI > Event System) " +
                        "to the scene so UI buttons receive clicks.");
                    return "InputSystemUIInputModule (no actions — buttons may be inert)";
                }

                try
                {
                    assignDefaults.Invoke(module, null);
                    return "InputSystemUIInputModule (default actions assigned)";
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[UiInput] AssignDefaultActions failed: {e.Message}. " +
                                     "Add a permanent EventSystem to the scene if buttons are inert.");
                    return "InputSystemUIInputModule (actions failed — buttons may be inert)";
                }
            }

            host.AddComponent<StandaloneInputModule>();
            return "StandaloneInputModule";
        }
    }
}
