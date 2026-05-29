# Agent Instructions

When reviewing this project, read this file and add its contents to the working context before making changes. It stores persistent workflow notes and project references that are useful across sessions.

## Workflow Notes

- Prefer reading nearby XML defs and patches before editing compatibility content; many folders mirror RimWorld mod IDs or feature areas.
- Keep compatibility changes scoped to the relevant folder under `Compatibility/` unless a shared definition or load-folder change is clearly required.
- Store future notes for model utility first: concise, direct, low-ambiguity, and optimized for reliable context loading. Human-readable polish is secondary.
- Future notes should shorten review/reference work and improve workflow, not summarize the whole project or become project ground truth.
- Preserve user or upstream edits already present in the working tree. Do not revert unrelated changes while implementing a compatibility fix.
- Release workflow uses a dev build cadence rather than standard changelist-based git hygiene. A large accumulated git diff can be expected; do not flag it by default as suspicious or try to normalize it. Use the current chat/request as the feature-isolation boundary and keep edits focused there.
- Building `1.6/Source/XMT.csproj` with `dotnet msbuild` requires permission outside the sandbox because the Windows SDK cache under the user profile is not sandbox-readable; request escalation directly instead of first running the sandboxed build.
- Use existing naming, indentation, and patch structure as the local source of truth.
- Compatibility/design work often starts with a proposal checkpoint before code edits. Treat proposal delivery as intermediate unless the user says the larger task is complete; use it to verify logic, balance, def names, and patch shape before implementation.
- For gnarly or open-ended feature prompts, discuss approach before initiating code changes on the first prompt; do not treat early investigation as authorization to implement unless the user explicitly asks for edits.
- After changing XML, validate by checking the touched file for well-formed XML and searching for related def names or patch targets.

## Helpful References

- Top-level mod metadata: `About/`
- Version-specific content: `1.6/`
- Shared cross-version or common content: `Common/`
- Compatibility patches and integrations: `Compatibility/`
- Load folder routing: `LoadFolders.xml`
- Project overview: `README.md`

## Future Notes

Add durable observations here when they would help a future agent work safely and consistently on this project.
- Parasite attach/resist code must revalidate parent/target spawned, alive, same-map, and adjacent after defensive melee; knockback/despawn can otherwise leave stale attach or acid-splash state.
- RimWorld debug actions require `using LudeonTK;` for `DebugAction`, `DebugActionType`, and `AllowedGameStates`.
- New core C# source files must be explicitly added to `1.6/Source/XMT.csproj`; the project does not auto-include all `.cs` files. Compatibility-specific C# belongs in that compatibility folder's own source/project files instead.
- Custom genes inheriting Biotech parents, such as `GeneSkinColorOverride`, should usually be `MayRequire="Ludeon.RimWorld.Biotech"` when assigned in XML patches.
- Last checked RimWorld 1.6 local source on 2026-05-29: animal `CompStealth` pawns do not need a custom invisibility render node; vanilla `Animal` render tree + `HediffComp_Invisibility` handles the stealth material when hediff state is correct.
- For complex RimWorld feature work, prefer a proposal and thin vertical slice before broad implementation; validate one representative object/job/UI path before generalizing.
- Before adding Harmony patches, inspect the exact RimWorld 1.6 target method signatures, overloads, and parameter names; compile success does not guarantee patch validity at launch.
- For dynamic designator/dropdown UI, verify vanilla class assumptions against source and smoke-test in-game; RimWorld UI inheritance often expects specific child command/designator shapes.
- When gameplay differs materially from vanilla construction, prefer a custom job/driver or existing local job pattern over forcing vanilla construction abstractions.
- For ideology/runtime-driven buildings, favor static placeholder defs carrying runtime target/style metadata rather than trying to generate fully dynamic buildable defs.
- For long work jobs, persist progress on the target or a save-loaded component and clean it up when the target is completed, destroyed, or despawned.
- Player-facing strings in C# or custom XML fields must use translation keys in `Languages/*/Keyed`; avoid hardcoded English except ordinary def `label`/`description` fields handled by RimWorld def translation.
- Treat mod settings labels/tooltips and mental-state reason strings as player-facing; route them through keyed translations unless they are dev-gizmo/debug-only.
- Last checked RimWorld 1.6 local assembly on 2026-05-29: `FloatMenuOption` has no native submenu/child-option API; use chained `FloatMenu` windows for nested menu flows.
- For pawn gizmo dropdowns, `FloatMenu` opens from current mouse position by default; setting `FloatMenu.InitialPositionShift` via reflection can bias menus upward while preserving cursor anchoring.
- Mechanitor queen progression uses `CompQueenAssimilation` for consumed-item state and mechanoid material/resource storage; keep assimilation/material integration state there rather than on evolution defs or hediffs.
- Custom `InteractionDef`s used by play-log/social-tab entries need a valid `symbolSource` or icon path; otherwise vanilla social tab rendering can crash while resolving the interaction symbol.
- Last checked RimWorld 1.6 local defs/source on 2026-05-29: vanilla `MechEnergy` need is player-mechs-only and recharge AI is player-mech gated; Electro Metabolic Catalyst currently targets mechs that already have `MechEnergy`, not independent feral/null-faction mech energy simulation.

## Agent Update Rule

- Consider updating this file when a completed task reveals durable workflow, release, compatibility, validation, or file-structure knowledge useful to future sessions.
- If the user did not explicitly request notes-file updates, ask before editing this file with newly discovered context.
- Do not run this reflection at proposal-only checkpoints unless the user explicitly says the larger task is done.
- Prefer compact model-readable bullets over prose. Optimize for retrieval and low ambiguity.
- Do not store transient task details, chat-specific plans, or one-off command output.
- For notes that shortcut broad RimWorld source investigation, include a `Last checked` date and version/source indicator so future agents can invalidate or re-check stale assumptions.
