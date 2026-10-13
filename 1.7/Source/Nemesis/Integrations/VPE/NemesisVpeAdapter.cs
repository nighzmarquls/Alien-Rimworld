using System;
using System.Reflection;
using VEF.Abilities;
using VefAbility = VEF.Abilities.Ability;

namespace Xenomorphtype
{
    public sealed class NemesisVpePsycastDescriptor
    {
        public string pathDefName;
        public int level;
        public float psyfocusCost;
        public float entropyGain;
    }

    public static class NemesisVpeAdapter
    {
        private const string PsycastExtensionTypeName = "VanillaPsycastsExpanded.AbilityExtension_Psycast";

        public static bool TryDescribePsycast(VefAbility ability, out NemesisVpePsycastDescriptor descriptor)
        {
            descriptor = null;
            if (ability?.AbilityModExtensions == null)
            {
                return false;
            }

            foreach (AbilityExtension_AbilityMod extension in ability.AbilityModExtensions)
            {
                Type type = extension?.GetType();
                if (!IsPsycastExtension(type))
                {
                    continue;
                }

                descriptor = new NemesisVpePsycastDescriptor
                {
                    pathDefName = ReadDefName(extension, type, "path"),
                    level = ReadValue(extension, type, "level", 0),
                    psyfocusCost = ReadValue(extension, type, "psyfocusCost", 0f),
                    entropyGain = ReadValue(extension, type, "entropyGain", 0f)
                };
                return true;
            }
            return false;
        }

        private static bool IsPsycastExtension(Type type)
        {
            while (type != null)
            {
                if (type.FullName == PsycastExtensionTypeName)
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }

        private static T ReadValue<T>(object instance, Type type, string fieldName, T fallback)
        {
            FieldInfo field = FindField(type, fieldName);
            return field?.GetValue(instance) is T value ? value : fallback;
        }

        private static string ReadDefName(object instance, Type type, string fieldName)
        {
            object value = FindField(type, fieldName)?.GetValue(instance);
            return value is Verse.Def def ? def.defName : null;
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            while (type != null)
            {
                FieldInfo field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    return field;
                }
                type = type.BaseType;
            }
            return null;
        }
    }
}
