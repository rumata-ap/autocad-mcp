// PolySharp does not emit the platform-compatibility attributes, and .NET Framework 4.8
// has no System.Runtime.Versioning.SupportedOSPlatformAttribute. The annotation is inert
// at runtime; it only documents the platform to analyzers.
#if NET48
namespace System.Runtime.Versioning
{
    [AttributeUsage(
        AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor |
        AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field |
        AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module |
        AttributeTargets.Property | AttributeTargets.Struct,
        AllowMultiple = true, Inherited = false)]
    internal sealed class SupportedOSPlatformAttribute : Attribute
    {
        public SupportedOSPlatformAttribute(string platformName) => PlatformName = platformName;

        public string PlatformName { get; }
    }

    [AttributeUsage(
        AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Constructor |
        AttributeTargets.Enum | AttributeTargets.Event | AttributeTargets.Field |
        AttributeTargets.Interface | AttributeTargets.Method | AttributeTargets.Module |
        AttributeTargets.Property | AttributeTargets.Struct,
        AllowMultiple = true, Inherited = false)]
    internal sealed class UnsupportedOSPlatformAttribute : Attribute
    {
        public UnsupportedOSPlatformAttribute(string platformName) => PlatformName = platformName;

        public string PlatformName { get; }
    }
}
#endif
