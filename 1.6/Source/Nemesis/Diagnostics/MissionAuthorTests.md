# Nemesis mission author tests

Restart RimWorld after building. Open **Debug actions → Alien | Rimworld → Nemesis → Mission**. Scenario commands deliberately alter the current map and leave fixtures for inspection.

Enable **Log Nemesis** in mod settings for detailed `[XMT][Nemesis][context]` intelligence, selection, route, world-pawn, and lord decisions. The setting is serialized and reset with the other log settings. Debug commands, scenario results, warnings, and errors always log. The settings window scrolls to preserve access to the lower controls.

## Commands

- **Inspect:** selection inputs and pending request, active Nemesis lords and their owned runtime state, retained world pawns, and remembered approach cells.
- **Atomic:** evaluate or recheck selection, cancel a pending request, refresh and compare spatial intelligence, reveal a mission pawn, issue an extraction job, interrupt ordinary cocoon completion, process retained pawns, and place retained hosts.
- **Launch:** force any mission in dormant or awakened mode. This bypasses launch timing and initial light admission; each mission worker still enforces its own continuation policy.
- **Scenarios:** run the data contracts and mission-lord ownership fixture separately or in sequence.

## Automated scenarios

1. **Mission data contracts** checks def validation, pressure and storyteller-point population bounds, swarm progression ceilings, strict attachment success, follow-up configuration, and generic changed-contact detection.
2. **Mission lord ownership** creates a collector and incapacitated player host, then verifies that the lord's dedicated job giver chooses off-map abduction and that reveal bookkeeping reaches the shared Nemesis lord base.
3. **Extraction, retention and placement** performs a destructive synchronous pass through real edge extraction, exact world-pawn retention, one-time delayed processing, cocoon placement, custody removal, and reuse rejection. It avoids storing test progress in a gameplay component.

## Live checks

| Case | Setup and expected result |
| --- | --- |
| Selection cadence | Evaluate with fresh census data and eligible hosts. One mission/map pair is selected by weight. A pending or live mission globally blocks another selection. |
| Pending darkness | Evaluate during daylight. The selected request remains pending and launches after darkness without rerolling. |
| Follow-up commitment | Complete a prelude at 20%+ xenoforming while its follow-up timing remains valid. A successful chance roll immediately stores one weighted follow-up and its delay; daylight may defer that committed mission but never rerolls it. At 75%, the configured chance is 95%. |
| Scouting | Move observed walls or other structures before deployment. Scouts split across changed or remembered spatial contacts, opportunistically take isolated dark/sleeping hosts, and withdraw when revealed, attacked, timed out, or exposed to light. Entering the home area marks success. |
| Collection intelligence | Collection is unavailable without current population intelligence or a viable player host. Remembered bed contacts provide approaches. Dedicated collectors do not require direct line of sight to hosts inside rooms; after their assigned approaches are exhausted, they expand acquisition to the whole map instead of waiting indefinitely. At least one active claim is player-owned before non-player hosts are selected. |
| Collection priority | Give eligible pawns different states. A pawn with an active facehugger is selected before a merely downed pawn, then downed before sleeping, then other valid hosts. Facehugger attachment remains intact through extraction. |
| Collection population | Xenoforming/Nemesis pressure always supplies the base. `StorytellerUtility.DefaultThreatPointsNow(map)` and the configured pawn kind's combat power add up to one extra base, for a maximum of twice the pressure count. Population confidence can reduce only that bonus. |
| Facehugger population | Xenoforming supplies an 8–60 ceiling that reaches 60 at 50%. Storyteller threat points spend a configured 0.5 budget against the larva's combat power to select the actual count within that ceiling. |
| Subverter population | Normal population scales from 6 at 25% xenoforming to 60 at 75%. Add the target map's bandwidth currently consumed by mechs overseen by player pawns directly to that population. This counter-mechanization bonus is uncapped and may exceed 60. |
| Swarm success | Resolve exactly two-thirds of the original swarm through attachments/subversions: the mission fails. Resolve more than two-thirds: it succeeds and may schedule a follow-up. |
| Power sabotage | With an awakened Nemesis, provide a powered network containing lights or turrets. During its single explicit snapshot pass, the mission caches those priority consumers, providers regardless of their own light, and only dark conduits for which no direct-fire player turret has both range and line of sight. It rolls and saves a 50–75% required-disabled fraction, tries cached conduits first, and checks the original priority-consumer set after each resolved target. Powered-off, broken, destroyed, or despawned priority consumers count as disabled. It considers providers only if the conduit tier cannot meet the threshold. A provider that has no executable walking/climbing/infiltration route triggers structural path recovery toward it. Runtime path failures are reported to the lord and the failed target is refused instead of being retried indefinitely; waiting saboteurs recheck for work on mission ticks. Turret LOS is never recomputed while rotating targets; reveal is ignored and combat causes withdrawal. |
| Destructive assault | Below 25% xenoforming it is ineligible. At or above 25%, it attacks the colony without implant/abduction jobs and uses normal damage/timeout retreat transitions. Daybreak ends it through 50%; above 50% it may continue. |
| Mech bandwidth stance evidence | A full census records bandwidth currently consumed by player-overseen mechs independently of raw mech count. This value contributes directly to Mechanization without population division, allowing one bandwidth-heavy mech to materially favor the Mechanitor stance. |
| Subverter turrets | Player-controlled Subverters can select unmanned direct-fire turrets with their Subvert command and sacrifice themselves to claim the turret. Nemesis/null-faction Subverters instead persistently corrupt player turrets, which display “Malfunctioning”; the vanilla and CE acquisition patches then target only non-cryptimorph-reading pawns. Mortars and manned turrets remain ineligible. |
| Released mission Subverter | Kill or remove a mechanoid host after a mission Subverter attaches. The released Subverter leaves its already-resolved swarm lord assignment and immediately seeks another eligible inorganic pawn or turret instead of waiting on the host/corpse. |
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

`Defs/Nemesis/NemesisMissions.xml` owns mission eligibility, weights, follow-up candidates, pressure inputs, lord/worker classes, timing behavior, duration, population range, threat-point population settings, and worker settings. `NemesisDefs.xml` owns the daily opportunity interval/chance and xenoforming-scaled follow-up chance/delay. The scheduler stores cadence plus a committed pending mission/map request; each live lord owns route, tactical state, and semantic success increments.
