// 1.7 SHIM: forward the legacy Unity module identities used by RJW/Menstruation
// to the game-supplied UnityStubs facade backed by Waylith. No legacy Unity binary is loaded.
using System.Runtime.CompilerServices;

[assembly: TypeForwardedTo(typeof(UnityEngine.TextAnchor))]
