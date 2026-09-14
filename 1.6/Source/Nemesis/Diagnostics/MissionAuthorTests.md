# Nemesis mission author tests

Restart RimWorld after building. Open **Debug actions → Alien | Rimworld → Nemesis → Mission**. Scenario commands deliberately alter the current map and leave fixtures for inspection.

Enable **Log Nemesis** in mod settings for detailed `[XMT][Nemesis][context]` intelligence, selection, route, world-pawn, and lord decisions. The setting is serialized and reset with the other log settings. Debug commands, scenario results, warnings, and errors always log. The settings window scrolls to preserve access to the lower controls.

## Commands

- **Inspect:** selection inputs and pending request, active Nemesis lords and their owned runtime state, retained world pawns, and remembered approach cells.
- **Atomic:** evaluate or recheck selection, cancel a pending request, refresh and compare spatial intelligence, reveal a mission pawn, issue an extraction job, interrupt ordinary cocoon completion, process retained pawns, and place retained hosts.
- **Launch:** force either mission in dormant or awakened mode. This bypasses launch timing and initial light admission; the lord still notices daylight and withdraws.
- **Scenarios:** run the data contracts and mission-lord ownership fixture separately or in sequence.

## Automated scenarios

1. **Mission data contracts** checks def validation, pressure and storyteller-point population bounds, and generic changed-contact detection.
2. **Mission lord ownership** creates a collector and incapacitated player host, then verifies that the lord's dedicated job giver chooses off-map abduction and that reveal bookkeeping reaches the shared Nemesis lord base.
3. **Extraction, retention and placement** performs a destructive synchronous pass through real edge extraction, exact world-pawn retention, one-time delayed processing, cocoon placement, custody removal, and reuse rejection. It avoids storing test progress in a gameplay component.

## Live checks

| Case | Setup and expected result |
| --- | --- |
| Selection cadence | Evaluate with fresh census data and eligible hosts. One mission/map pair is selected by weight. A pending or live mission globally blocks another selection. |
| Pending darkness | Evaluate during daylight. The selected request remains pending and launches after darkness without rerolling. |
| Scouting | Move observed walls or other structures before deployment. Scouts split across changed or remembered spatial contacts, opportunistically take isolated dark/sleeping hosts, and withdraw when revealed, attacked, timed out, or exposed to light. Entering the home area marks success. |
| Collection intelligence | Collection is unavailable without current population intelligence or a viable player host. Remembered bed contacts provide approaches. At least one active claim is player-owned before non-player hosts are selected. |
| Collection population | Xenoforming/Nemesis pressure always supplies the base. `StorytellerUtility.DefaultThreatPointsNow(map)` and the configured pawn kind's combat power add up to one extra base, for a maximum of twice the pressure count. Population confidence can reduce only that bonus. |
| Distributed movement | Multiple members receive staggered route indices and deterministic offsets around each approach. Active abduction jobs provide host claims, preventing duplicate selection. Active attack jobs spread known threats before reuse. |
| Violence | Empty collectors use the threat pheromone response. When a target goes down, rage ends and the mission job giver may abduct it. Carriers suppress violence and extract. The lord waits until all surviving members are revealed/downed before ending the covert phase. |
| Extraction | The shared grab driver performs resistance and adjacency validation. Successful edge extraction retains the exact pawn in `GameComponent_NemesisWorldPawns`; no cocoon is created on the source map. |
| Delayed processing | Make a retained host due. It applies the existing 0.5 implantation gain once after ten days and obeys the existing cap of 10. Dead, spawned, caravan, or otherwise unavailable pawns leave custody without credit. |
| Cocooned placement | Generate a supported hive site or use atomic placement. All available abducted hosts may be placed, even above the site's normal generated host count. A successful placement removes custody immediately, so the pawn cannot be reused. |
| Abandoned rescue | Leave the generated site. The placed pawn follows ordinary cocoon, world-pawn, and xenoforming handling and is never offered by the retention service again. |
| Queen defense | Trigger the established queen-defense flow. Its lord still derives from `LordJob_Nemesis`, preserving shared reveal and threat hooks without using mission scheduling or mission state. |

## Save/load checkpoints

Save and reload with a pending daylight request, an active route, a partial grab, a carried host, revealed members, and a retained host before and after delayed processing. Verify that the lord restores its route/member/threat state, pending selection does not reroll, and world-pawn credit or placement cannot repeat.

## XML tuning

`Defs/Nemesis/NemesisMissions.xml` owns mission eligibility, weights, pressure inputs, lord/worker classes, duration, population range, and worker settings. `NemesisDefs.xml` owns the daily opportunity interval and chance. The scheduler stores only cadence plus a pending mission/map request; each live lord owns route and tactical state.
