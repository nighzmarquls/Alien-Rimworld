// 1.7 SHIM: forward the legacy Unity module identities used by RJW/Menstruation
// to the game-supplied UnityStubs facade backed by Waylith. No legacy Unity binary is loaded.
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(UnityEngine.Event))]
[assembly: TypeForwardedTo(typeof(UnityEngine.EventType))]
[assembly: TypeForwardedTo(typeof(UnityEngine.GUI))]
[assembly: TypeForwardedTo(typeof(UnityEngine.GUIContent))]
[assembly: TypeForwardedTo(typeof(UnityEngine.GUISkin))]
[assembly: TypeForwardedTo(typeof(UnityEngine.GUIStyle))]
[assembly: TypeForwardedTo(typeof(UnityEngine.GUIStyleState))]
[assembly: TypeForwardedTo(typeof(UnityEngine.ScaleMode))]
