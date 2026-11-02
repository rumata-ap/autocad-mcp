using System;
using System.IO;
using System.Reflection;

namespace Nodal.AcadMcp.Addin;

/// <summary>
/// AutoCAD's acad.exe.config carries no binding redirects, so the version-pinned shims a
/// .NET Framework add-in ships cannot be unified the way a normal application's app.config
/// would. The concrete failure: System.Memory asks for System.Runtime.CompilerServices.Unsafe
/// 4.0.4.1 while System.Text.Json asks for 6.0.0.0, and the request for 4.0.4.1 throws
/// FileNotFoundException the first time JsonSerializer's static constructor runs — which is
/// during Initialize(), so AutoCAD drops the whole add-in and its commands.
///
/// Resolve that known set by simple name from the add-in's own folder instead. Nothing else
/// is intercepted: AutoCAD's assemblies and other add-ins' dependencies are left alone.
/// </summary>
internal static class AssemblyResolution
{
    private static readonly string[] Shims =
    {
        "System.Runtime.CompilerServices.Unsafe",
        "System.Memory",
        "System.Buffers",
        "System.Numerics.Vectors",
        "System.Threading.Tasks.Extensions",
        "Microsoft.Bcl.AsyncInterfaces",
        "System.Text.Json",
        "System.Text.Encodings.Web",
    };

    private static bool _installed;

    internal static void Install()
    {
        if (_installed) return;

        _installed = true;
        AppDomain.CurrentDomain.AssemblyResolve += Resolve;
    }

    private static Assembly? Resolve(object? sender, ResolveEventArgs args)
    {
        var requested = new AssemblyName(args.Name).Name;
        if (requested is null || Array.IndexOf(Shims, requested) < 0) return null;

        var folder = Path.GetDirectoryName(typeof(AssemblyResolution).Assembly.Location);
        if (folder is null) return null;

        var candidate = Path.Combine(folder, requested + ".dll");
        return File.Exists(candidate) ? Assembly.LoadFrom(candidate) : null;
    }
}
