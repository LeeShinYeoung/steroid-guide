# references

Build-time assemblies referenced by `SteroidGuide.csproj` via `<HintPath>`.
These are **NOT** redistributed in the produced `.tmod` (each `<Reference>`
sets `<Private>false</Private>`); they exist only so the project compiles
against the right type metadata. The user's installed copy of the host mod
is what runs at game time.

## MagicStorage.dll

- **Source release:** `v0.7.0.11` ("Hotfix - 1.4.4 Stable") from
  https://github.com/blushiemagic/MagicStorage/releases
- **Direct asset URL:**
  https://github.com/blushiemagic/MagicStorage/releases/download/v0.7.0.11/MagicStorage.dll
- **SHA-256:** `8857de121834b6f8e609809e62cdd6c8d3b00fea8e26c93186a5f56e5c20e9d0`
- **Used types:** `MagicStorage.Components.TEStorageHeart`,
  `MagicStorage.Components.TEAbstractStorageUnit`
  (touched only inside `Common/MagicStorageBridge.cs`)
- **Weak reference floor (`build.txt`):** `MagicStorage@0.7`

### Refresh procedure

When Magic Storage publishes a new compatible release and we need to compile
against newer API surface:

1. Download the new `MagicStorage.dll` from the release page (the asset is
   attached directly to the GitHub release; no archive extraction required).
2. Replace this file in-place: `references/MagicStorage.dll`.
3. If the new release bumps the major or minor `build.txt` `version` (e.g.
   `0.7.x` -> `0.8.x`) and we depend on the new surface, raise the floor in
   our `build.txt`:  `weakReferences = MagicStorage@<new-major.minor>`.
4. Update this README's source-release line and SHA-256.
5. Rebuild and smoke-test (load with MS installed; load without MS installed
   to confirm graceful degradation).
