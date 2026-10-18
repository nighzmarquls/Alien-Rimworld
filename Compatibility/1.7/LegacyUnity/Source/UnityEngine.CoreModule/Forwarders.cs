// 1.7 SHIM: forward the legacy Unity module identities used by RJW/Menstruation
// to the game-supplied UnityStubs facade backed by Waylith. No legacy Unity binary is loaded.
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(UnityEngine.Color))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Debug))]
[assembly: TypeForwardedTo(typeof(UnityEngine.KeyCode))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Material))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Mathf))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Object))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Rect))]
[assembly: TypeForwardedTo(typeof(UnityEngine.RectOffset))]
[assembly: TypeForwardedTo(typeof(UnityEngine.RenderTexture))]
[assembly: TypeForwardedTo(typeof(UnityEngine.StackTraceUtility))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Texture))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Texture2D))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Vector2))]
[assembly: TypeForwardedTo(typeof(UnityEngine.Vector3))]
