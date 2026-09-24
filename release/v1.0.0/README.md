# KeePassCopyKey

A minimal KeePass 2.x plugin for transferring a hand-picked subset of
entries between two independent KeePass installations, with no network
and no cloud service involved.

> Status: development.

## Naming note

The project and assembly are called `KeePassCopyKey`, but the user-facing
commands are **Export Keys** / **Import Keys**, not "Copy" / "Load".
KeePass itself already has a clipboard-based "Copy Entry (Encrypted)"
command between two open databases (Entry → Data Exchange) - naming this
plugin's file-based commands "Copy" as well would suggest to anyone
familiar with stock KeePass that it does the same clipboard operation,
which it doesn't. "Export"/"Import" reuses vocabulary KeePass already has
for file-based operations (File → Export, File → Import, Entry → Data
Exchange → Export Entry), so it matches a mental model users already
have. The project's own name stays as `KeePassCopyKey` for practical
reasons (renaming would touch `Directory.Build.props`, the `.sln`,
`AssemblyInfo.cs`'s `InternalsVisibleTo`, every `using`, and this file,
for no functional benefit) - only the visible strings changed.

## What it does

- **Export Keys...** (Tools → Copy Key, or the toolbar export icon): pick
  entries from the open database via a checkbox tree (mirrors the real
  group structure), set a password (a random one is pre-filled and
  editable), and save them to a single `.kck` file.
- **Import Keys...** (Tools → Copy Key, or the toolbar import icon): pick
  a `.kck` file, enter its password, and the entries are added to the
  currently open database. Save the database (Ctrl+S) to keep them.

The `.kck` file is self-contained - send it through any channel (a
messenger, a USB stick, email); the password should travel through a
separate channel.

## File format

```
4 bytes   magic "KCK4"
1 byte    format version (1)
16 bytes  PBKDF2 salt
4 bytes   PBKDF2 iteration count (400,000 by default)
24 bytes  XSalsa20Poly1305 nonce
N bytes   ciphertext + 16-byte Poly1305 tag
```

The payload is a flat count-prefixed list of entries
(title/username/password/url/notes), each a length-prefixed UTF-8 string.
No JSON, no external format dependency.

Encryption is always on - there is no "save without a password" option.
Earlier iterations of this format tried that (an optional checkbox, a
silently auto-generated fallback password, and a "no password but still
encrypted" mode) and each had a real problem - see the comment at the
top of `IO/KeyFile.cs` for the full history. The password field in the
export dialog is pre-filled with a random value so there's always
something valid to submit; the OK button simply refuses an empty field
rather than doing something silently on the user's behalf.

## Cryptography

- **Key derivation:** PBKDF2-HMAC-SHA256, 400,000 iterations, 16-byte
  random salt per file.
- **Encryption:** XSalsa20Poly1305, via a vendored, MIT-licensed subset
  of the `Chaos.NaCl` library (`Vendor/ChaosNaCl/` - see
  `Vendor/ChaosNaCl/License.txt`), used here directly with a
  password-derived key. Zero external dependencies - no NuGet package,
  no native DLL.

## Building

```powershell
dotnet build KeePassCopyKey.sln
```

Building `KeePassCopyKey` automatically runs the test suite afterwards
(see `Directory.Build.targets`) - no separate `dotnet test` step needed.

Override the KeePass installation path used as the compile-time
reference:

```powershell
dotnet build KeePassCopyKey.sln -p:KeePassDir="C:\Path\To\Your\KeePass"
```

### Reproducible builds

The project sets `Deterministic`, `ContinuousIntegrationBuild`, and
`PathMap` (see `Directory.Build.props`) so that building from a clean
clone produces a byte-identical DLL regardless of the machine or the
folder it was cloned into - verify with:

```powershell
Get-FileHash bin\Release\net48\KeePassCopyKey.dll
```

This guarantee additionally requires building against **the same
KeePass.exe version** on every machine (it's used as a compile-time-only
reference, `Private=false`, but its version can still affect emitted
metadata) - pin a specific KeePass release across all build machines,
not just the `KeePassDir` path.

## Project layout

```
KeePassCopyKey/          the plugin itself
KeePassCopyKey.Tests/    xUnit tests - no KeePass install required to run them
```

`IO/KeyFile.cs` and `Crypto/KeyFileCrypto.cs` work with a plain
`KeyEntryRecord` type, not KeePassLib's `PwEntry` - the PwEntry mapping
lives only in `KeePassCopyKeyExt.cs` and `UI/EntrySelectionDialog.cs`.
This keeps the file-format and crypto logic testable on any machine.

`AssemblyInfo.cs` grants `KeePassCopyKey.Tests` access to this
assembly's `internal` types via `InternalsVisibleTo` - `KeyFile`,
`KeyFileCrypto`, and `KeyEntryRecord` are implementation details, not a
public API, so they stay `internal` rather than `public`.

## Installing

Copy `KeePassCopyKey.dll` into KeePass's `Plugins` folder and restart
KeePass. No `.plgx` packaging - this ships as a plain compiled DLL.

## License

GNU General Public License v3.0 or later - see `LICENSE` for the full
text.

This does not conflict with anything the project depends on: KeePass
itself is GPLv2+, but this plugin is not thereby required to use GPL -
it only compiles against `KeePass.exe` as a reference assembly
(`Private=false`, no KeePass source code copied in), and the copyleft
obligation applies to distributing modified/derived KeePass code, not to
independent programs linking against it at runtime. GPLv3 is chosen
here as an independent, deliberate choice, not a requirement. The
vendored `Chaos.NaCl` subset is MIT-licensed, and MIT is freely
combinable into a GPLv3 project (the reverse - GPL code into an
MIT-licensed project - would not be permitted, but that's not the
direction here).