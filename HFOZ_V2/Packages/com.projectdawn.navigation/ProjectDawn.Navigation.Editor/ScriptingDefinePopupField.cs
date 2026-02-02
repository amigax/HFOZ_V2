using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace ProjectDawn.Navigation.Editor
{
    public static class ScriptingDefinePopupField
    {
        public static void Draw(GUIContent label, string[] values, string[] names)
        {
            int index = HasScriptingDefineSymbol(values);

            EditorGUI.BeginChangeCheck();

            index = EditorGUILayout.Popup(label, index, names);

            if (EditorGUI.EndChangeCheck())
            {
                if (!EditorUtility.DisplayDialog("Confirmation", $"This operation will modify scripting defines by adding/removing define symbol {values[index]}", "Yes", "No"))
                {
                    return;
                }

                foreach (var symbol in values)
                    RemoveScriptingDefineSymbol(symbol);
                AddScriptingDefineSymbol(values[index]);
            }
        }

        static int HasScriptingDefineSymbol(string[] defineSymbols)
        {
            string defines = GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            for (int i = 0; i < defineSymbols.Length; i++)
            {
                if (defines.Contains(defineSymbols[i]))
                    return i;
            }
            return 0;
        }

        static void AddScriptingDefineSymbol(string symbol)
        {
            string defines = GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (!defines.Contains(symbol))
            {
                defines += ";" + symbol;
                SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, defines);
            }
        }

        static void RemoveScriptingDefineSymbol(string symbol)
        {
            string defines = GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            if (defines.Contains(symbol))
            {
                defines = defines.Replace(";" + symbol, "").Replace(symbol + ";", "").Replace(symbol, "");
                SetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup, defines);
            }
        }

        static string GetScriptingDefineSymbolsForGroup(BuildTargetGroup targetGroup)
        {
#if UNITY_6000_0_OR_NEWER
            return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup));
#else
            return PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#endif
        }

        static void SetScriptingDefineSymbolsForGroup(BuildTargetGroup targetGroup, string defines)
        {
#if UNITY_6000_0_OR_NEWER
            PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(targetGroup), defines);
#else
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, defines);
#endif
        }
    }
}
