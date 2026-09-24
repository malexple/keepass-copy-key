// C# 9+ init-only properties/record types need this marker type, which
// ships in the BCL from .NET 5+ but doesn't exist in net48's BCL at all.
// The compiler happily emits IL that references it regardless of target
// framework - it just needs *some* type with this exact name/namespace
// to exist somewhere in the compiled assembly. This is the standard,
// widely-used polyfill for enabling records/init-only setters on
// net48/netstandard2.0 (same fix keepasshttp2 already uses - see its own
// Net48Compat.cs).

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}
