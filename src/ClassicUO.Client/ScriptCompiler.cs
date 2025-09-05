#region References
using ClassicUO.Utility.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
#endregion

namespace ClassicUO
{
    public static class ScriptCompiler
    {
        public static bool Compiled { get { return CompileTaskStatus == TaskStatus.RanToCompletion || CompileTaskStatus == TaskStatus.Faulted; } }
        public static TaskStatus CompileTaskStatus { get; set; } = TaskStatus.WaitingToRun;
        public static Task CompileTask { get; private set; }

        public static Assembly[] Assemblies { get; set; }

        private static readonly List<string> m_AdditionalReferences = new List<string>();

        public static string[] GetReferenceAssemblies()
        {
            var list = new List<string>
            {
                CUOEnviroment.ExecutablePath + "/ClassicUO.Assets.dll",
                CUOEnviroment.ExecutablePath + "/ClassicUO.IO.dll",
                CUOEnviroment.ExecutablePath + "/ClassicUO.Renderer.dll",
                CUOEnviroment.ExecutablePath + "/ClassicUO.Utility.dll",
                CUOEnviroment.ExecutablePath + "/FNA.dll",
                CUOEnviroment.ExecutablePath + "/ClassicUO.exe"
            };

            // Add all assemblies from the current AppDomain that are not dynamic and have a physical location.
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    {
                        list.Add(assembly.Location);
                    }
                }
                catch (NotSupportedException) { /* Dynamic assemblies might throw this */ }
            }

            var path = Path.Combine(CUOEnviroment.ExecutablePath, "Data/Assemblies.cfg");

            if (File.Exists(path))
            {
                using (var ip = new StreamReader(path))
                {
                    string line;

                    while ((line = ip.ReadLine()) != null)
                    {
                        if (line.Length > 0 && !line.StartsWith("#"))
                        {
                            list.Add(line);
                        }
                    }
                }
            }

            list.AddRange(m_AdditionalReferences);

            return list.Distinct().ToArray();
        }

        private static void AppendCompilerOption(ref StringBuilder sb, string define)
        {
            if (sb == null)
            {
                sb = new StringBuilder();
            }
            else
            {
                sb.Append(' ');
            }

            sb.Append(define);
        }

        private static byte[] GetHashCode(string compiledFile, string[] scriptFiles, bool debug)
        {
            using (var ms = new MemoryStream())
            {
                using (var bin = new BinaryWriter(ms))
                {
                    var fileInfo = new FileInfo(compiledFile);

                    bin.Write(fileInfo.LastWriteTimeUtc.Ticks);

                    foreach (var scriptFile in scriptFiles)
                    {
                        fileInfo = new FileInfo(scriptFile);

                        bin.Write(fileInfo.LastWriteTimeUtc.Ticks);
                    }

                    bin.Write(debug);
                    bin.Write(CUOEnviroment.Version.ToString());

                    ms.Position = 0;

                    using (var sha1 = SHA1.Create())
                    {
                        return sha1.ComputeHash(ms);
                    }
                }
            }
        }

        public static bool CompileCSScripts(out Assembly assembly)
        {
            return CompileCSScripts(false, true, out assembly);
        }

        public static bool CompileCSScripts(bool debug, out Assembly assembly)
        {
            return CompileCSScripts(debug, true, out assembly);
        }

        public static bool CompileCSScripts(bool debug, bool cache, out Assembly assembly)
        {
            Console.Write("Scripts: Compiling C# scripts...");
            var files = GetScripts("*.cs");

            if (files.Length == 0)
            {
                Console.WriteLine("no files found.");
                assembly = null;
                return true;
            }

            if (File.Exists("Scripts/Output/Scripts.CS.dll"))
            {
                if (cache && File.Exists("Scripts/Output/Scripts.CS.hash"))
                {
                    try
                    {
                        var hashCode = GetHashCode("Scripts/Output/Scripts.CS.dll", files, debug);

                        using (var fs = new FileStream("Scripts/Output/Scripts.CS.hash", FileMode.Open, FileAccess.Read, FileShare.Read))
                        {
                            using (var bin = new BinaryReader(fs))
                            {
                                var bytes = bin.ReadBytes(hashCode.Length);

                                if (bytes.Length == hashCode.Length)
                                {
                                    var valid = true;

                                    for (var i = 0; i < bytes.Length; ++i)
                                    {
                                        if (bytes[i] != hashCode[i])
                                        {
                                            valid = false;
                                            break;
                                        }
                                    }

                                    if (valid)
                                    {
                                        assembly = Assembly.LoadFrom("Scripts/Output/Scripts.CS.dll");

                                        if (!m_AdditionalReferences.Contains(assembly.Location))
                                        {
                                            m_AdditionalReferences.Add(assembly.Location);
                                        }

                                        Console.WriteLine("done (cached)");

                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    catch
                    { }
                }
            }

            DeleteFiles("Scripts.CS*.dll");

            var compilation = CSharpCompilation.Create
            (
                "Scripts.CS.dll",
                syntaxTrees: files.Select(file => CSharpSyntaxTree.ParseText(File.ReadAllText(file))),
                references: GetReferenceAssemblies().Select(r => MetadataReference.CreateFromFile(r)),
                options: new CSharpCompilationOptions
                (
                    OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: debug ? OptimizationLevel.Debug : OptimizationLevel.Release,
                    allowUnsafe: true
                )
            );

            using (var ms = new MemoryStream())
            {
                EmitResult results = compilation.Emit(ms);

                if (!results.Success)
                {
                    Display(results.Diagnostics);
                    assembly = null;
                    return false;
                }

                ms.Seek(0, SeekOrigin.Begin);
                assembly = Assembly.Load(ms.ToArray());

                var path = GetUnusedPath("Scripts.CS");

                File.WriteAllBytes(path, ms.ToArray());

                m_AdditionalReferences.Add(path);

                Console.WriteLine("done");

                if (cache && Path.GetFileName(path) == "Scripts.CS.dll")
                {
                    try
                    {
                        var hashCode = GetHashCode(path, files, debug);

                        using var fs = new FileStream("Scripts/Output/Scripts.CS.hash", FileMode.Create, FileAccess.Write, FileShare.None);
                        using (var bin = new BinaryWriter(fs))
                        {
                            bin.Write(hashCode, 0, hashCode.Length);
                        }
                    }
                    catch
                    { }
                }
            }

            return true;
        }

        public static void Display(IEnumerable<Diagnostic> diagnostics)
        {
            var errors = new List<Diagnostic>();
            var warnings = new List<Diagnostic>();

            foreach (var diagnostic in diagnostics)
            {
                if (diagnostic.IsWarningAsError || diagnostic.Severity == DiagnosticSeverity.Error)
                {
                    errors.Add(diagnostic);
                }
                else if (diagnostic.Severity == DiagnosticSeverity.Warning)
                {
                    warnings.Add(diagnostic);
                }
            }

            if (errors.Count > 0)
            {
                Console.WriteLine("Failed with: {0} errors, {1} warnings", errors.Count, warnings.Count);
            }
            else
            {
                Console.WriteLine("Finished with: {0} errors, {1} warnings", errors.Count, warnings.Count);
            }

            if (warnings.Count > 0)
            {
                Console.WriteLine("Warnings:");
                foreach (var diagnostic in warnings)
                {
                    Console.WriteLine("    {0} ({1}): {2}", diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition, diagnostic.GetMessage());
                }
            }

            if (errors.Count > 0)
            {
                Console.WriteLine("Errors:");
                foreach (var diagnostic in errors)
                {
                    Console.WriteLine("    {0} ({1}): {2}", diagnostic.Id, diagnostic.Location.GetLineSpan().StartLinePosition, diagnostic.GetMessage());
                }
            }
        }

        public static string GetUnusedPath(string name)
        {
            var path = Path.Combine(CUOEnviroment.ExecutablePath, String.Format("Scripts/Output/{0}.dll", name));

            for (var i = 2; File.Exists(path) && i <= 1000; ++i)
            {
                path = Path.Combine(CUOEnviroment.ExecutablePath, String.Format("Scripts/Output/{0}.{1}.dll", name, i));
            }

            return path;
        }

        public static void DeleteFiles(string mask)
        {
            try
            {
                var files = Directory.GetFiles(Path.Combine(CUOEnviroment.ExecutablePath, "Scripts/Output"), mask);

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch
                    { }
                }
            }
            catch
            { }
        }

        public static void Compile()
        {
            Compile(false);
        }

        public static void Compile(bool debug)
        {
            Compile(debug, true);
        }

        public static void Compile(bool debug, bool cache)
        {
            CompileTask = Task.Factory.StartNew(() =>
            {
                try
                {
                    EnsureDirectory("Scripts/");
                    EnsureDirectory("Scripts/Output/");

                    if (m_AdditionalReferences.Count > 0)
                    {
                        m_AdditionalReferences.Clear();
                    }

                    var assemblies = new List<Assembly>();

                    Assembly assembly;

                    if (CompileCSScripts(debug, cache, out assembly))
                    {
                        if (assembly != null)
                        {
                            assemblies.Add(assembly);
                        }
                    }
                    else
                    {
                        CompileTaskStatus = TaskStatus.Faulted;
                        return;
                    }

                    CompileTaskStatus = TaskStatus.RanToCompletion;

                    if (assemblies.Count == 0)
                    {
                        return;
                    }

                    Assemblies = assemblies.ToArray();
                }
                catch (Exception ex)
                {
                    CompileTaskStatus = TaskStatus.Faulted;
                    Log.Panic(ex.ToString());
                    string path = Path.Combine(CUOEnviroment.ExecutablePath, "Logs");

                    if (!Directory.Exists(path))
                        Directory.CreateDirectory(path);

                    using (LogFile crashfile = new LogFile(path, "crash.txt"))
                    {
                        crashfile.WriteAsync(ex.ToString()).RunSynchronously();
                    }
                }
            });
        }

        public static void InvokeAfterCompiling(string method)
        {
            if (CompileTask != null)
            {
                if (CompileTask.Status == TaskStatus.Running || CompileTask.Status == TaskStatus.WaitingToRun)
                {
                    Task.Factory.StartNew(() =>
                    {
                        CompileTask.Wait();
                        Invoke(method);
                    });
                }
                else
                {
                    Invoke(method);
                }
            }
        }

        public static void Invoke(string method)
        {
            try
            {
                if (Assemblies != null)
                {
                    var invoke = new List<MethodInfo>();

                    foreach (var a in Assemblies)
                    {
                        var types = a.GetTypes();

                        foreach (var t in types)
                        {
                            var m = t.GetMethod(method, BindingFlags.Static | BindingFlags.Public);

                            if (m != null)
                            {
                                invoke.Add(m);
                            }
                        }
                    }

                    invoke.Sort(new CallPriorityComparer());

                    foreach (var m in invoke)
                    {
                        m.Invoke(null, null);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Panic(ex.ToString());
                string path = Path.Combine(CUOEnviroment.ExecutablePath, "Logs");

                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                using (LogFile crashfile = new LogFile(path, "crash.txt"))
                {
                    crashfile.WriteAsync(ex.ToString()).RunSynchronously();
                }
            }
        }

        private static readonly Dictionary<Assembly, TypeCache> m_TypeCaches = new Dictionary<Assembly, TypeCache>();
        private static TypeCache m_NullCache;

        public static TypeCache GetTypeCache(Assembly asm)
        {
            if (asm == null)
            {
                return m_NullCache ?? (m_NullCache = new TypeCache(null));
            }

            TypeCache c;

            m_TypeCaches.TryGetValue(asm, out c);

            if (c == null)
            {
                m_TypeCaches[asm] = c = new TypeCache(asm);
            }

            return c;
        }

        public static Type FindTypeByFullName(string fullName)
        {
            return FindTypeByFullName(fullName, true);
        }

        public static Type FindTypeByFullName(string fullName, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            Type type = null;

            for (var i = 0; type == null && i < Assemblies.Length; ++i)
            {
                type = GetTypeCache(Assemblies[i]).GetTypeByFullName(fullName, ignoreCase);
            }

            return type ?? GetTypeCache(CUOEnviroment.Assembly).GetTypeByFullName(fullName, ignoreCase);
        }

        public static IEnumerable<Type> FindTypesByFullName(string name)
        {
            return FindTypesByFullName(name, true);
        }

        public static IEnumerable<Type> FindTypesByFullName(string name, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                yield break;
            }

            for (var i = 0; i < Assemblies.Length; ++i)
            {
                foreach (var t in GetTypeCache(Assemblies[i]).GetTypesByFullName(name, ignoreCase))
                {
                    yield return t;
                }
            }

            foreach (var t in GetTypeCache(CUOEnviroment.Assembly).GetTypesByFullName(name, ignoreCase))
            {
                yield return t;
            }
        }

        public static Type FindTypeByName(string name)
        {
            return FindTypeByName(name, true);
        }

        public static Type FindTypeByName(string name, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            Type type = null;

            for (var i = 0; type == null && i < Assemblies.Length; ++i)
            {
                type = GetTypeCache(Assemblies[i]).GetTypeByName(name, ignoreCase);
            }

            return type ?? GetTypeCache(CUOEnviroment.Assembly).GetTypeByName(name, ignoreCase);
        }

        public static IEnumerable<Type> FindTypesByName(string name)
        {
            return FindTypesByName(name, true);
        }

        public static IEnumerable<Type> FindTypesByName(string name, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                yield break;
            }

            for (var i = 0; i < Assemblies.Length; ++i)
            {
                foreach (var t in GetTypeCache(Assemblies[i]).GetTypesByName(name, ignoreCase))
                {
                    yield return t;
                }
            }

            foreach (var t in GetTypeCache(CUOEnviroment.Assembly).GetTypesByName(name, ignoreCase))
            {
                yield return t;
            }
        }

        public static void EnsureDirectory(string dir)
        {
            var path = Path.Combine(CUOEnviroment.ExecutablePath, dir);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public static string[] GetScripts(string filter)
        {
            var list = new List<string>();

            GetScripts(list, Path.Combine(CUOEnviroment.ExecutablePath, "Scripts"), filter);

            return list.ToArray();
        }

        public static void GetScripts(List<string> list, string path, string filter)
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                GetScripts(list, dir, filter);
            }

            list.AddRange(Directory.GetFiles(path, filter));
        }
    }

    public class TypeCache
    {
        private readonly Type[] m_Types;
        private readonly TypeTable m_Names;
        private readonly TypeTable m_FullNames;

        public Type[] Types { get { return m_Types; } }
        public TypeTable Names { get { return m_Names; } }
        public TypeTable FullNames { get { return m_FullNames; } }

        public Type GetTypeByName(string name, bool ignoreCase)
        {
            return GetTypesByName(name, ignoreCase).FirstOrDefault(t => t != null);
        }

        public IEnumerable<Type> GetTypesByName(string name, bool ignoreCase)
        {
            return m_Names.Get(name, ignoreCase);
        }

        public Type GetTypeByFullName(string fullName, bool ignoreCase)
        {
            return GetTypesByFullName(fullName, ignoreCase).FirstOrDefault(t => t != null);
        }

        public IEnumerable<Type> GetTypesByFullName(string fullName, bool ignoreCase)
        {
            return m_FullNames.Get(fullName, ignoreCase);
        }

        public TypeCache(Assembly asm)
        {
            if (asm == null)
            {
                m_Types = Type.EmptyTypes;
            }
            else
            {
                m_Types = asm.GetTypes();
            }

            m_Names = new TypeTable(m_Types.Length);
            m_FullNames = new TypeTable(m_Types.Length);

            foreach (var g in m_Types.ToLookup(t => t.Name))
            {
                m_Names.Add(g.Key, g);

                foreach (var type in g)
                {
                    m_FullNames.Add(type.FullName, type);

                    var attr = type.GetCustomAttribute<TypeAliasAttribute>(false);

                    if (attr != null)
                    {
                        foreach (var a in attr.Aliases)
                        {
                            m_FullNames.Add(a, type);
                        }
                    }
                }
            }

            m_Names.Prune();
            m_FullNames.Prune();

            m_Names.Sort();
            m_FullNames.Sort();
        }
    }

    public class TypeTable
    {
        private readonly Dictionary<string, List<Type>> m_Sensitive;
        private readonly Dictionary<string, List<Type>> m_Insensitive;

        public void Prune()
        {
            Prune(m_Sensitive);
            Prune(m_Insensitive);
        }

        private static void Prune(Dictionary<string, List<Type>> types)
        {
            var buffer = new List<Type>();

            foreach (var list in types.Values)
            {
                if (list.Count == 1)
                {
                    continue;
                }

                buffer.AddRange(list.Distinct());

                list.Clear();
                list.AddRange(buffer);

                buffer.Clear();
            }

            buffer.TrimExcess();
        }

        public void Sort()
        {
            Sort(m_Sensitive);
            Sort(m_Insensitive);
        }

        private static void Sort(Dictionary<string, List<Type>> types)
        {
            foreach (var list in types.Values)
            {
                list.Sort(InternalSort);
            }
        }

        private static int InternalSort(Type l, Type r)
        {
            if (l == r)
            {
                return 0;
            }

            if (l != null && r == null)
            {
                return -1;
            }

            if (l == null && r != null)
            {
                return 1;
            }

            var a = IsEntity(l);
            var b = IsEntity(r);

            if (a && b)
            {
                if (a && !b)
                {
                    return -1;
                }

                if (!a && b)
                {
                    return 1;
                }

                return 0;
            }

            return a ? -1 : b ? 1 : 0;
        }

        private static bool IsEntity(Type type)
        {
            return type.GetInterface("IEntity") != null;
        }

        public void Add(string key, IEnumerable<Type> types)
        {
            if (!string.IsNullOrWhiteSpace(key) && types != null)
            {
                Add(key, types.ToArray());
            }
        }

        public void Add(string key, params Type[] types)
        {
            if (string.IsNullOrWhiteSpace(key) || types == null || types.Length == 0)
            {
                return;
            }

            if (!m_Sensitive.TryGetValue(key, out var sensitive) || sensitive == null)
            {
                m_Sensitive[key] = new List<Type>(types);
            }
            else if (types.Length == 1)
            {
                sensitive.Add(types[0]);
            }
            else
            {
                sensitive.AddRange(types);
            }

            if (!m_Insensitive.TryGetValue(key, out var insensitive) || insensitive == null)
            {
                m_Insensitive[key] = new List<Type>(types);
            }
            else if (types.Length == 1)
            {
                insensitive.Add(types[0]);
            }
            else
            {
                insensitive.AddRange(types);
            }
        }

        public IEnumerable<Type> Get(string key, bool ignoreCase)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return Type.EmptyTypes;
            }

            List<Type> t;

            if (ignoreCase)
            {
                m_Insensitive.TryGetValue(key, out t);
            }
            else
            {
                m_Sensitive.TryGetValue(key, out t);
            }

            if (t == null)
            {
                return Type.EmptyTypes;
            }

            return t.AsEnumerable();
        }

        public TypeTable(int capacity)
        {
            m_Sensitive = new Dictionary<string, List<Type>>(capacity);
            m_Insensitive = new Dictionary<string, List<Type>>(capacity, StringComparer.OrdinalIgnoreCase);
        }
    }

    public class CallPriorityComparer : IComparer<MethodInfo>
    {
        public int Compare(MethodInfo x, MethodInfo y)
        {
            if (x == null && y == null)
            {
                return 0;
            }

            if (x == null)
            {
                return 1;
            }

            if (y == null)
            {
                return -1;
            }

            var xPriority = GetPriority(x);
            var yPriority = GetPriority(y);

            if (xPriority > yPriority)
                return 1;

            if (xPriority < yPriority)
                return -1;

            return 0;
        }

        private int GetPriority(MethodInfo mi)
        {
            var objs = mi.GetCustomAttributes(typeof(CallPriorityAttribute), true);

            if (objs == null)
            {
                return 0;
            }

            if (objs.Length == 0)
            {
                return 0;
            }

            CallPriorityAttribute attr = objs[0] as CallPriorityAttribute;

            if (attr == null)
            {
                return 0;
            }

            return attr.Priority;
        }

        public class CallPriorityAttribute : Attribute
        {
            public int Priority { get; set; }

            public CallPriorityAttribute(int priority)
            {
                Priority = priority;
            }
        }
    }

    public class TypeAliasAttribute : Attribute
    {
        private readonly string[] m_Aliases;

        public string[] Aliases { get { return m_Aliases; } }

        public TypeAliasAttribute(params string[] aliases)
        {
            m_Aliases = aliases;
        }
    }
}
