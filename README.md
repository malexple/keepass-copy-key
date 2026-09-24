# KeePassCopyKey

A minimal KeePass 2.x plugin for copying a hand-picked subset of entries
between two independent KeePass installations, with no network and no
cloud service involved.

> Status: development.

## What it does

- **Copy Keys...** (Tools → Copy Key): pick any entries from the open
  database via a checkbox list, optionally set a password, and save them
  to a single `.kck` file.
- **Load Keys...**: pick a `.kck` file, enter its password if it has one,
  and the entries are added to the currently open database. Save the
  database (Ctrl+S) to keep them.

The `.kck` file is self-contained - send it through any channel (a
messenger, a USB stick, email); the password (if any) should travel
through a separate channel.

## Why not the browser-protocol plugin (keepasshttp2)?

This is a separate, single-purpose project rather than a feature bolted
onto `keepasshttp2` - no WebSocket server, no protocol handshake, no
persistent identity keys. Just: select, encrypt, save, load, decrypt.

## File format

```
4 bytes   magic "KCK1"
1 byte    flags: bit 0 = encrypted
if encrypted:
  16 bytes  PBKDF2 salt
  4 bytes   PBKDF2 iteration count (400,000 by default)
  24 bytes  XSalsa20Poly1305 nonce
N bytes   payload (plaintext, or ciphertext + 16-byte Poly1305 tag)
```

The payload is a flat count-prefixed list of entries
(title/username/password/url/notes), each a length-prefixed UTF-8 string.
No JSON, no external format dependency.

Leaving the password blank is a supported, explicit choice (not a
default) - the export dialog requires either a password or a checked
"Do not encrypt" box before it lets you proceed.

## Cryptography

- **Key derivation:** PBKDF2-HMAC-SHA256, 400,000 iterations, 16-byte
  random salt per file.
- **Encryption:** XSalsa20Poly1305 (vendored from `Chaos.NaCl`, copied
  from the `keepasshttp2` project's `Vendor/ChaosNaCl` - same primitive,
  used here directly with a password-derived key rather than via X25519
  key exchange).

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

## Installing

Copy `KeePassCopyKey.dll` into KeePass's `Plugins` folder and restart
KeePass. No `.plgx` packaging - this ships as a plain compiled DLL.

## License

Same license as your other KeePass plugin projects (fill in).
