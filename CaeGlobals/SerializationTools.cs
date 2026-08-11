// PrePoMax - Copyright (C) 2016-2026 Matej Borovinsek
//
// Licensed under the terms defined in the LICENSE file located in the root directory of this source code.

using System;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace CaeGlobals
{
    internal static class SerializationTools
    {
        private static bool _assemblyResolverRegistered;

        public static BinaryFormatter CreateBinaryFormatter()
        {
            RegisterAssemblyResolver();
            //
            return new BinaryFormatter()
            {
                Binder = new CompatibleSerializationBinder()
            };
        }

        private static void RegisterAssemblyResolver()
        {
            if (_assemblyResolverRegistered) return;
            //
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            _assemblyResolverRegistered = true;
        }

        private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            string requestedAssemblyName = new AssemblyName(args.Name).Name;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, requestedAssemblyName, StringComparison.OrdinalIgnoreCase))
                    return assembly;
            }

            return null;
        }

        private class CompatibleSerializationBinder : SerializationBinder
        {
            public override Type BindToType(string assemblyName, string typeName)
            {
                Type type = Type.GetType(typeName + ", " + assemblyName, ResolveAssembly, ResolveType, false);
                if (type != null) return type;

                string requestedAssemblyName = new AssemblyName(assemblyName).Name;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (string.Equals(assembly.GetName().Name, requestedAssemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        type = assembly.GetType(typeName, false);
                        if (type != null) return type;
                    }
                }

                Assembly loadedAssembly = LoadAssemblyByName(requestedAssemblyName);
                if (loadedAssembly != null)
                {
                    type = loadedAssembly.GetType(typeName, false);
                    if (type != null) return type;
                }

                return null;
            }

            private static Assembly ResolveAssembly(AssemblyName assemblyName)
            {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (string.Equals(assembly.GetName().Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                        return assembly;
                }

                try
                {
                    return Assembly.Load(assemblyName);
                }
                catch
                {
                    return LoadAssemblyByName(assemblyName.Name);
                }
            }

            private static Type ResolveType(Assembly assembly, string typeName, bool ignoreCase)
            {
                if (assembly != null) return assembly.GetType(typeName, false, ignoreCase);
                return Type.GetType(typeName, false, ignoreCase);
            }

            private static Assembly LoadAssemblyByName(string assemblyName)
            {
                if (string.IsNullOrEmpty(assemblyName)) return null;

                try
                {
                    return Assembly.Load(assemblyName);
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
