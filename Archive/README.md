# Repository cleanup and upload inventory

These notes distinguish the live Unity project from loose uploads and historical delivery material. Documentation was reorganized on 7 October 2026; ongoing uploads and deployments were left untouched.

## Loose root uploads

The root currently contains 13 loose scripts, five Fractured Realm textures, five texture metadata files, and `vfx-polish.patch`. These paths remain in place while an upload is ongoing. Unity compiles and imports the versions under `Assets/`; a root copy does not update the corresponding runtime file.

After the transfer finishes, unchanged delivery copies can be moved together into `Archive/RootUploads/`. First compare against the latest branch: do not archive a path still being uploaded or merge replacement code automatically.

Ten root script uploads matched their active `Assets/` counterpart exactly at inspection. These three differed and need manual comparison:

| Root upload | Live counterpart |
|---|---|
| `../GildedCombatPresentation.cs` | [GildedCombatPresentation.cs](../Assets/Scripts/UI/GildedCombatPresentation.cs) |
| `../GildedShardReliquary.cs` | [GildedShardReliquary.cs](../Assets/Scripts/UI/GildedShardReliquary.cs) |
| `../GildedShardShrine.cs` | [GildedShardShrine.cs](../Assets/Scripts/UI/GildedShardShrine.cs) |

Keep root art uploads and metadata together when organizing them. An orphan `fractured_realm_splitling.tga.meta` had no matching root texture; do not import that metadata on its own. Compare any delivery texture or GUID with the active enemy art before integrating it.

## Historical VFX delivery bundle

[`GildedFate_VFX_Polish`](../GildedFate_VFX_Polish) remains at its original path because its upload instructions and patch are tied to that package structure. Its nested `Assets` directory is delivery material. Several files differ from the live project. Do not overlay the whole bundle blindly; compare the affected live files and apply only intended changes.

## Future uploads

Put Unity changes at their intended `Assets/...` path and preserve existing `.meta` GUIDs. Keep design notes under `Docs/`. Use a feature branch or a delivery archive for files that still need comparison, rather than adding another loose copy at the repository root.

[Main README](../README.md) · [Fate Shards guide](../FATESHARD_OVERHAUL_README.md)
