using HarmonyLib;
using System;
using System.Linq;

namespace Parallax.Harmony_Patches
{
    internal static class ModTypes
    {
        internal static Type Find(string assemblyName, string typeName)
        {
            foreach (AssemblyLoader.LoadedAssembly loadedAssembly in AssemblyLoader.loadedAssemblies)
            {
                if (!string.Equals(loadedAssembly.assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Type type = loadedAssembly.assembly.GetType(typeName, false)
                    ?? AccessTools.GetTypesFromAssembly(loadedAssembly.assembly).FirstOrDefault(t => t.FullName == typeName);
                if (type != null)
                {
                    return type;
                }
            }
            return null;
        }
    }
}
