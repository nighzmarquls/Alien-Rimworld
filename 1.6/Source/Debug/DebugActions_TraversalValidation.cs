using LudeonTK;
using System.Collections.Generic;

namespace Xenomorphtype
{
    public static class DebugActions_TraversalValidation
    {
        private const string Category = "Alien | Rimworld";

        [DebugActionYielder]
        private static IEnumerable<DebugActionNode> TraversalValidationNodes()
        {
            yield return new DebugActionNode("Traversal validation", DebugActionType.Action, null)
            {
                category = Category,
                childGetter = delegate
                {
                    return new List<DebugActionNode>
                    {
                        DebugActions_ClimbNavigation.MakeRootNode(),
                        DebugActions_InfiltrationNavigation.MakeRootNode()
                    };
                }
            };
        }
    }
}
