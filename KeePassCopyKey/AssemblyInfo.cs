// Grants KeePassCopyKey.Tests access to this assembly's `internal` types
// (KeyFile, KeyFileCrypto, KeyEntryRecord) without making them public.
// Those types are implementation details of the plugin, not a public
// API - InternalsVisibleTo lets the test project exercise them directly
// while keeping that boundary for everyone else.

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("KeePassCopyKey.Tests")]
