# Changelog

## v1.0.0 — first release

### Added

- **Export Keys...** / **Import Keys...** commands (Tools menu and main
  toolbar icons) for transferring a hand-picked subset of entries
  between two independent KeePass installations via a single encrypted
  `.kck` file - no network, no cloud service.
- Checkbox tree entry picker mirroring the database's real group
  structure, with cascading group selection.
- Password dialog pre-filled with a random, dictation-friendly password
  (Crockford Base32, e.g. `HPAZ-ZAYX-M9W2-BN6A`); encryption is always
  on - the OK button refuses an empty password field rather than
  silently saving unencrypted or substituting a hidden default.
- Procedurally drawn (no binary image assets) paired toolbar icons -
  export (up arrow out of a tray) and import (down arrow into a tray).
- Automatic group-tree/entry-list UI refresh after import
  (`MainForm.UpdateUI`), so imported entries are visible immediately.
- Bilingual (`ru`/`en`) UI strings based on `CurrentUICulture`.
- xUnit test suite (`KeyFileCrypto`, `KeyFile` format round-trips) that
  runs with no KeePass installation required, wired to run automatically
  on every `dotnet build` via `Directory.Build.targets`.
- Reproducible build setup (`Deterministic`, `ContinuousIntegrationBuild`,
  `PathMap`) - byte-identical DLL from a clean clone, given the same
  .NET SDK and KeePass.exe version.
- GPLv3-or-later license (`LICENSE`), MIT-licensed vendored `Chaos.NaCl`
  subset (`Vendor/ChaosNaCl/`) for XSalsa20Poly1305/PBKDF2-based
  encryption - zero external dependencies, no NuGet package, no native
  DLL.

### File format history (`.kck`)

The encryption model went through four iterations before landing on the
final design - kept here, and in more detail in `IO/KeyFile.cs`, because
each earlier design's specific flaw is exactly why the final one looks
the way it does:

- `KCK1` — optional password behind a "do not encrypt" checkbox. Problem:
  an unchecked box is easy to forget; the safe path was opt-in, not
  default.
- `KCK2` — password mandatory, but an empty field silently substituted a
  generated password behind the user's back.
- `KCK3` — tried "no password, but still encrypted." Turned out to be
  cryptographically impossible without either a hardcoded key (fake
  security, defeated by decompiling the DLL) or asymmetric encryption
  (a much bigger feature - recipient identity keys, out of scope for
  v1.0.0).
- `KCK4` (current) — password is always mandatory. The dialog pre-fills
  a random one so there's always something valid to submit, and
  clicking OK with an empty field just re-shows a warning instead of
  closing.

### UI iteration notes

- Toolbar buttons added via the community-standard `Controls.Find("m_toolMain", true)`
  technique (KeePass has no official "add a toolbar button" plugin API).
- Dialog layouts (entry picker, password dialog) use dynamically computed
  label heights and `Anchor`-based button positioning instead of
  hardcoded coordinates, after early versions clipped or overlapped text
  when message length varied (long file paths, wrapped labels) or the
  window was resized.
- Commands are named "Export"/"Import" rather than "Copy"/"Load" to avoid
  colliding with KeePass's own clipboard-based "Copy Entry (Encrypted)"
  command, which is a different mechanism entirely - see the "Naming
  note" section in `README.md`.
