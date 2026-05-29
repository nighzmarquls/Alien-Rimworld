# Alien | RimWorld

Alien | RimWorld is a RimWorld mod focused on Alien/Xenomorph-themed content and compatibility integrations for RimWorld 1.6.

## Project Layout

- `About/` contains mod metadata.
- `1.6/` contains RimWorld 1.6-specific content.
- `Common/` contains shared content used across versions or load folders.
- `Compatibility/` contains optional integrations and patches for other mods.
- `LoadFolders.xml` controls version and compatibility folder routing.

## Working On Compatibility

Compatibility folders usually mirror another mod, mod ID, or feature area. Before editing one, inspect nearby defs and patches to match local naming, indentation, and patch structure.

Keep changes scoped to the relevant compatibility folder unless shared definitions or load-folder routing clearly need to change.

## Validation

After changing XML, check that touched files are well-formed and search for related def names or patch targets to confirm the change matches existing references.

## License

See `LICENSE`.
