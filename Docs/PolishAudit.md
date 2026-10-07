# Gilded Fate: Polish Audit (October 2026)

Whole-game audit before the professional polish pass. Enemy animation is out of scope (the team will replace the procedural motion later). Status column: **Confirmed** means the code was re-read and the defect reproduced by reading; **Sweep** means reported by the audit sweep and re-checked when the fix is made.

Baseline before any change: native harness PASS 107,484 / FAIL 0, compile checks (with and without DEVELOPMENT_BUILD) 0 errors.

## Batch 1: Rules and card correctness

| ID | Sev | Finding | Status |
|----|-----|---------|--------|
| R-1 | High | Player **Vulnerable never expires**. `EndPlayerTurn` ticks Weak/Frail and enemy Vulnerable, nothing ticks the player's. Rune Mage, Wild tear/eruption claws and Debuff intents stack it for the rest of the combat (+50% damage taken). | Confirmed |
| R-2 | High | "**First card this turn**" effects use the combat-wide `cardsPlayed`: Crown of Sacrifice, Opening Blow, Controlled Breathing, Gilded Toss only work on the first card of the whole combat. Battle Rhythm ("every third card each turn") and Silver Feather ("fourth card each turn") carry their count across turns. | Confirmed |
| R-3 | High | `retaliateTriggeredTurn` is zeroed in `NextTurn` and only raised during the enemy phase, so "if Retaliate triggered this turn" (No Mercy, Countercharge, Vengeful Sweep) can **never be true**. | Confirmed |
| R-4 | Med | **Crown Breaker and Final Judgment count Strength twice** (card adds it, the generic damage path adds it again). Final Judgment deals 24 + 3x Strength instead of 24 + 2x. | Confirmed |
| R-5 | Med | Worn Whetstone ("first Attack each combat") fires every turn and on every hit of a multi-hit attack. Duelist's Pin ignores "only Attack played". | Confirmed (Whetstone) |
| R-6 | Med | Mirror Fragment ("first 0-cost card each turn") fires once per combat; `zeroCostRelicUsed` is never reset. | Confirmed |
| R-7 | Med | Stolen Hourglass breaks Twisted Fate (hand is shuffled away and not redrawn) and blocks relic draws the text does not mention. | Confirmed |
| R-8 | Low | Soul Drain took no target in multi-enemy fights (Marked was consumed from whichever enemy was last selected). Adapt and Dark Veil use the currently selected target, which is intended. | Confirmed, fixed for Soul Drain |
| R-9 | Med | **Everlasting Ember has no implementation** (relic is in the pool, does nothing). | Confirmed |
| R-10 | Low | Overhead Strike applies Vulnerable before the hit, so Heavy deals 37 instead of the printed 25. | Sweep |
| R-11 | Low | 12-card hand cap is skipped by return-from-discard/exhaust, curses, temporary cards, Lingering. | Sweep |
| R-12 | Low | Dead branches for relic ids that do not exist (`thorn`, `immortal_thread`). | Sweep |
| R-13 | Low | Sigil of Malice can be played for nothing when the sigil row is full. | Sweep |

## Batch 2: Menus, flow and input safety

| ID | Sev | Finding | Status |
|----|-----|---------|--------|
| F-1 | High | **NEW RUN and DAILY RUN silently destroy the saved run.** No confirmation anywhere. | Confirmed |
| F-2 | High | No input debounce after a screen change: only the Map has one. A double-click on SEVER A THREAD or UPGRADE lands on a card in the next screen and spends gold or the rest-site action. | Confirmed |
| F-3 | High | Stale controller focus index after a screen change: highlight and action disagree on the first A press (merchant removal, sanctuary upgrade, binding). | Confirmed |
| F-4 | Med | B/Backspace in the shop leaves it instantly and irreversibly (unspent gold lost) while Esc opens the pause menu. | Confirmed |
| F-5 | Med | A saved resolution is applied without validation (monitor swap, damaged profile). | Sweep |
| F-6 | Med | A corrupt save makes CONTINUE vanish with no message. | Sweep |
| F-7 | Low | Invisible controller slot in the shop when no shard is offered. | Sweep |
| F-8 | Low | A double-click on an event choice also presses RETURN TO THE MAP on the result panel that opens under the cursor, skipping the result text. | Confirmed |
| F-9 | Low | Statistics screen reachable only from Credits and its BACK skips Credits. | Sweep |
| F-10 | Low | RunResult ignores B/Esc. | Sweep |
| F-11 | Low | Sanctuary UPGRADE tile stays active when nothing can be upgraded. | Sweep |

## Batch 3: UI layout and readability

| ID | Sev | Finding | Status |
|----|-----|---------|--------|
| UI-1 | High | The **End Turn button and energy orb cover the edge cards** of a hand of 6 or more at 1440x810 and up to 16:10 (fixed rects, `HandLayout` ignores them). | Confirmed |
| UI-2 | High | The enemy status strip's second row hides behind the hand; icons are lost in 4-enemy fights. | Re-checked: not a defect. Rows end at y 529 against a resting hand top of 539 (1440x810), more room at taller aspect ratios, and overflow already collapses into a "+N" chip. |
| UI-3 | Med | Shop and shrine hover tooltips cover the price of the item being hovered. | Sweep |
| UI-4 | Med | Key numbers use 8 to 12 px fonts (floor, energy, piles, incoming damage, status stacks, colorblind codes). | Sweep |
| UI-5 | Med | Relic row collides with page headings from about 12 relics. | Sweep |
| UI-6 | Med | `DrawButtonFrame` "disabled" draws a stray line and does not dim; "disabled" is reused for "selected". | Confirmed |
| UI-7 | Med | TAKE RELIC is an unframed bare button that hangs below its panel. | Sweep |
| UI-8 | Low | Small hit targets (settings sliders, Fate Debt arrows); scroll rail is not interactive. | Sweep |
| UI-9 | Low | Small overlaps (SELECT label over rules text, fixed-size upgrade comparison, dead code). | Sweep |

## Batch 4: Feedback and juice

| ID | Sev | Finding | Status |
|----|-----|---------|--------|
| J-3 | High | Unaffordable or unavailable clicks are silent (shop, shrine, events). `UiDenied` exists but is only used in combat and Gild. | Confirmed |
| J-4 | High | The Settings screen is silent (no hover, confirm or volume preview) and ducks the music while the music slider is adjusted. | Sweep |
| J-5 | High | Combat number popups overlap: lane counters restart at 0 for every card, and under reduce-motion there is no drift. | Confirmed (structure) |
| J-6 | High | No shared button hover sound, pressed tint or cursor feedback on the 51 `DrawButtonFrame` call sites. | Confirmed |
| J-7 | Med | Status sound cues inverted (the player's own debuffs play the Energy chime). | Confirmed |
| J-8 | Med | No enemy-death sound. | Sweep |
| J-9 | Med | The player-hurt vignette is boss-only. | Sweep |
| J-10 | Med | No low-HP warning. | Sweep |
| J-11 | Med | HitHeavy is unreachable for Reaper and Arcane cards. | Sweep |
| J-12 | Med | Pause, deck, shop-leave, reward-skip, quit-confirm and several choice paths are silent. | Sweep |
| J-13 | Med | Per-frame regex, LINQ and `new GUIStyle` allocations (card text, map, enemy art lookups). Estimate from code, not profiled. | Sweep |
| J-2 | Med | Procedural textures are built on first use inside a draw frame (possible hitch on first Dissipate or Gild). Estimate from code, not profiled. | Sweep |
| J-14 | Med | Gold and HP numbers snap instead of counting. | Sweep |
| J-16/17 | Low | Some shake/flicker paths ignore the screen-shake or reduce-flashing settings. | Sweep |
| J-18 | High | (Your request) Aiming an Attack and playing a card felt rough: the guide popped in, the arrow was a stiff line, target edges flickered, and a played card hung for a moment after release. | Fixed |
| J-19 | High | (Your request) The Gild coin was small and tucked in the bottom-left corner, easy to forget. | Fixed |

## Batch 5: Balance (after the rule fixes, because R-1 distorts every Act 3 number)

Method: every one of the 369 encounters was fought by a simple card-playing bot (greedy block/attack, 3 heroes x 4 seeds, deck and relics sized to how far into the act the fight happens). The bot is crude, so absolute win rates are not meaningful, only comparisons between encounters.

* Difficulty rises act to act and tier to tier as designed.
* Easy outliers inside Act 1 elites: Sunken Engine, Royal Auctioneer, Living Treasury (100% win, 28 to 49% HP lost) against the other Act 1 elites (33 to 58% win, 79 to 90% lost). Same for Pale Gardener and Fallen Comet in Act 2.
* Hard outliers in Act 3: High Confessor, Choir Eternal, Royal General, World Break neutral elite and the Black Cathedral and Throne formations (about 35% win against 56% for Fractured Realm).
* Act 3 formation first-turn spikes: A3-NO3 hits for 53 on turn one against a passive player; BC-D10 averages about 51 per turn.

Re-measured after Batch 1, then tuned. Every number change is listed in the batch notes.


---

## Batch 1 fix log (rules and card correctness)

All of R-1 to R-13 are fixed, with regression tests in the native harness (`b1.cs`, 65 new assertions). The fixes were mutation-checked: re-introducing R-1, R-3, R-6 or R-7 makes the new tests fail. One older harness expectation (Choking Procession 15) had been passing only because Vulnerable never wore off; it now expects the printed 10.

**Behaviour changes the player will feel (each is a gameplay change, listed so you can veto any):**

| Change | Direction |
|--------|-----------|
| Vulnerable on the player now wears off (one stack per enemy phase it was active for). A Vulnerable applied during an enemy phase still hits the next enemy attack, then ends. | Easier for the player (bug fix) |
| Crown of Sacrifice: the first card **each turn** is free (was: first card of the whole combat). | Stronger relic; matches its text |
| Opening Blow, Controlled Breathing, Gilded Toss: "first card this turn" works every turn. | Stronger cards; match their text |
| Controlled Breathing keeps its Block only if it really was the only card played that turn (checked when the turn ends). | Matches its text |
| Battle Rhythm and Silver Feather count cards per turn (were counting across turns). | Matches their text |
| No Mercy, Countercharge, Vengeful Sweep: Retaliate that triggered during the enemy phase counts on your next turn (was: could never be true). | Stronger cards; they now do what they say |
| Crown Breaker and Final Judgment add Strength once (was twice and three times). | Weaker; matches their text |
| Worn Whetstone: +6 on the first Attack of the combat, first hit only (was: every turn, every hit). | Weaker; matches its text |
| Duelist's Pin: +4 on the first hit of the first Attack each turn. Text simplified to "Your first Attack each turn deals +4 damage." (the "only Attack played" condition cannot be known when the hit lands). | Behaviour kept, once per Attack rather than per hit |
| Mirror Fragment: once each turn (was: once per combat). | Stronger; matches its text |
| Overhead Strike: deals its printed 25, then applies Vulnerable (was: Vulnerable first, so Heavy hit for 37). | Weaker; matches its text |
| Everlasting Ember now works: an enemy that dies with Burn passes half of it to a random living enemy. | Relic was a dead pick |
| Stolen Hourglass no longer breaks Twisted Fate. | Bug fix |
| Soul Drain needs a chosen target. | Bug fix |
| Sigil of Malice can be played when the sigil row is full (the row cycles, like every other Sigil card). | Bug fix |
| Curses, Status cards and Lingering cards no longer push the hand past 12 (they go to the discard pile). | Bug fix |
| Dead branches for relic ids that do not exist (`thorn`, `immortal_thread`) removed. | Cleanup |

**Measured effect (bot, same 369 encounters):** win rate moved by 1 to 5 points per act (for example Foundry formations 69.5 to 74.6, Black Cathedral formations 35.0 to 39.2). So the permanent-Vulnerable bug did *not* explain the Act 3 difficulty cliff; that is a separate balance question handled in Batch 5.


---

## Batch 2 fix log (menus, flow and input safety)

All of F-1 to F-11 are addressed. New file: `UI/GildedPolishFlow.cs` (shared UI scale, input guard, replace-run confirmation, damaged-save notice, sanctuary helper).

**Honest limit:** the native harness only compiles Combat, Core and Map, so none of this UI code was exercised by it. It is compile-checked in both configurations (with and without `DEVELOPMENT_BUILD`) and checked by reading, and the in-Unity verification suites were read for assumptions it could break (they set `screen` and the controller index directly, so the new guards are bypassed when `captureMode` is on). **It needs a play-through in Unity**, ideally with a mouse and a controller.

| Finding | Fix |
|---------|-----|
| F-1 | NEW RUN (hero confirm) and DAILY RUN now ask **"Replace your saved run?"** when a run exists, showing hero, act, room, HP and gold. The safe button (KEEP MY RUN) is the default focus; Esc, Backspace and B keep the run (they arrive as the shared "back" input); mouse and controller both work, and nothing underneath reacts while it is open. |
| F-2 | After every screen change a 0.2 s guard swallows pointer presses and controller A (0.35 s after the run-start hand-off). Combat, the main menu and the Playground are exempt. |
| F-3 | The controller focus index and its screen are reset on every screen change, so the highlight and the action agree on the first A press. |
| F-4 | In the shop, **B moves focus to LEAVE SHOP first**; a second B leaves (with the back sound). Esc still opens the pause menu. Unspent gold is no longer lost by one stray press. |
| F-5 | A saved resolution the monitor cannot show falls back to the native resolution. |
| F-6 | A save that cannot be read now shows a notice on the title screen ("your saved run could not be read, the files were kept") and the replace confirmation says so ("Replace the damaged save?"). |
| F-7 | The invisible shop controller slot is skipped when no shard is offered. |
| F-8 | The event result panel is its own screen, so the screen-change guard (F-2) now covers it: a double-click on a choice no longer presses RETURN TO THE MAP. |
| F-9 | Statistics BACK returns to Credits (where it was opened from). |
| F-10 | B / Esc on the run result screen returns to the main menu. |
| F-11 | The Sanctuary UPGRADE tile dims and says "Every card in your deck is already upgraded." when nothing can be upgraded, and plays the denied sound on click or A. |

Also: one shared `UiScale` formula (menus, combat and the meta overlay each had their own copy), and a dead duplicate Merchant controller branch was removed.


---

## Batch 3 fix log (UI layout and readability)

**Honest limit (same as Batch 2):** UI code is compile-checked in both configurations and checked by reading; the native harness cannot draw. The one piece of layout that is pure code (the hand fan) has harness tests (`b3.cs`: hands of 1 to 12 cards at four canvas widths clear the energy orb and the End Turn button) and a matching in-Unity verification. Everything else needs a look in Unity. Positions below are in the 1440 x 810 authoring canvas.

| Finding | Fix |
|---------|-----|
| UI-1 | **End Turn moved** from beside the Discard pile (where large hands slid under it) to the right-hand column above the Dissipate pile. The hand fan now reserves 200 px on each side (`HandLayout.SideReserve`), so hands of 6 or more cards are slightly tighter (span 806 px at 1440 instead of up to 980) but no card is ever covered by the energy orb or the End Turn button. Hands of 5 or fewer are unchanged. |
| UI-3 | Shop tooltips (cards, relics, shard, Sever a Thread, Restore HP) anchor to the item plus its price tag, so they open below the price or flip above the item and never cover it. |
| UI-4 | 40 hard-coded 8 to 11 px fonts raised to 11 or 12 px: FLOOR / ACT labels, deck count, pile names, ENERGY, RESONANCE, End Turn "incoming" line, colorblind status codes, enemy intent destination, map and archive captions, achievement toasts, Fate Debt text, tier labels. Rects were adjusted where a label was tight. Card text and fonts computed at run time were not touched. |
| UI-5 | **Relics now live inside the top bar** (between the ACT badge and the map button, 14 slots at 1440, "+N" chip beyond that) instead of a second row at y 67 that ran under page headings from about the 9th relic. The old second row is gone; every page heading is clear of it. |
| UI-6 | `DrawButtonFrame` has a real **disabled** look (dimmed frame, no hover light, the stray 50 px line is gone) and a separate **selected** state. The NORMAL / UPGRADE PREVIEW tabs now use "selected" instead of misusing "disabled". |
| UI-7 | TAKE RELIC is a framed button inside a proper panel (the panel was an unframed area and the button hung below it). |
| UI-8 | The scroll rail is now **draggable** (grab the thumb or click the track; 24 px wide hit area) on every scrolling list. Settings sliders have a 32 px tall hit area (was 20). Fate Debt arrows are 44 x 36 (was 36 x 30). |
| UI-9 | SELECT / DISCARD / DISSIPATE labels in the choose-a-card grid sit under the card instead of over its rules text. The upgrade comparison panel shrinks its text to fit instead of overflowing. Two unused methods (`DrawCombatRelics`, `DrawPageControls`) deleted. |

---

## Batch 4 fix log (feedback and juice, plus card feel and the Gild badge)

**Honest limit:** every change here is compile-checked in both configurations (default and DEVELOPMENT_BUILD) and the native harness still passes (107,862 checks, 0 failures), but the harness cannot draw or play sound. The sound, motion and layout changes need a look and a listen in Unity. **No new audio was made:** every new sound reuses an existing cue (the enemy death sound is the card-exhaust and block-break cues layered).

| Finding | Fix |
|---------|-----|
| J-3 | Refused actions now play the denied sound: an unaffordable shop price, a shard you cannot take or buy, an event choice you cannot afford, a shard that cannot activate, and End Turn while it is disabled. (`DeniedPress` in GildedPolishFlow.cs; it stays silent during the first 0.2 s after a screen change so a double-click is not punished.) |
| J-4 | Settings has sound: a hover tick when focus moves between rows, a confirm on toggles and page changes, a back sound on BACK, and a short preview when a volume slider changes (Effects previews a hit, Master and UI preview the confirm). The music no longer ducks while you are on the Audio page adjusting it. |
| J-5 | Combat numbers share persistent lanes (a 0.9 s memory), so numbers from different cards no longer start in the same slot and overlap. |
| J-6 | Every `DrawButtonFrame` button gets one hover tick when the pointer arrives and a pressed look (deeper bronze fill, no outer glow) while the mouse button is down. |
| J-7 | The player's own debuffs (Weak, Vulnerable, Frail, Marked, Curse, Status cards and similar) play the debuff cue instead of the Energy chime. A new check in the audio verification covers it. |
| J-8 | Enemies now have a death sound, layered from existing cues, for single enemies and for every enemy in a group. |
| J-9 | The hurt vignette now flashes for any hit on the player (bigger for heavy hits), not only boss hits. |
| J-10 | Below 30% HP the screen edge pulses red (steady under Reduce Motion, half strength under Reduce Flashing) and the top-bar HP number turns red. |
| J-11 | Enemy hits of 20 or more from Reaper and Arcane cards layer the heavy-hit sound under their own. |
| J-12 | Pause open and close, settings from the pause menu, save and quit, opening the deck, LEAVE SHOP, reward SKIP, the quit confirmation (QUIT and STAY), and controller back presses all have a sound. Leaving the pause menu no longer plays two sounds at once. |
| J-13 | Card rules text formatting (a regex per card per frame) is cached. **Other per-frame allocations (map, enemy art lookups, GUIStyle) were not profiled and are untouched.** |
| J-14 | Gold and HP in the top bar count to their new value instead of snapping (instant under Reduce Motion and at the start of a run). |
| J-16/17 | The enemy hit jitter obeys Reduce Motion. Only this one path was changed; other shake and flicker paths were not audited further. |
| J-2 | The combat textures (final VFX, energy orb, boss polish) are built when combat starts, not on the first frame that needs them. |
| J-18 | **Card aiming and play.** (1) The pull that carries an Attack out of the hand is eased, so a quick flick never makes the card or the guide jump; the card also leans slightly toward the cursor. (2) The target guide no longer appears the moment you press a card: it fades in as you pull, the four brackets close onto the target as it locks, and the dim, labels and damage preview ease in and out (the guide also fades after a release instead of vanishing). (3) The aim arrow is a soft dotted curve that flows toward the target, with a diamond head and a ring that tightens and brightens on lock; its tip glides onto the target instead of snapping. (4) A target you are locked on to holds for 22 px outside its edge while dragging, and the same rule decides the drop, so releasing where the guide says "RELEASE TO PLAY" always plays. A different enemy under the pointer still wins immediately. (5) A soft tick plays when a target locks. (6) A played card leaves your hand at speed and settles onto its impact point (it used to ease in slowly and seem to hang); its arc follows distance travelled; it gives a small pulse as it lands. Draws, discards and Dissipate motions are unchanged. Rules, targets and costs are unchanged. Code: GildedTargetingFeel.cs. New in-Unity checks cover the lock hold, the lock release, the exact edge without a drag and the flight curve. |
| J-19 | **Gild badge.** It moved from the bottom-left corner (under the shard sockets) to the right-hand column directly under End Turn, beside the Dissipate pile, so it sits where every turn ends. It is a minted coin set in a forged bezel on a nameplate that matches the End Turn plate: READY shows "GILD", the price and its key; ARMED shows "GILDED · NEXT CARD x2" with a molten ring; spent shows "NEXT TURN"; short of gold shows "NEED" with the price in red; during the enemy turn or a resolving card it dims but keeps its layout. State changes fade instead of flipping. While ready, the coin turns now and then and a warm ring pulses outward; at the start of each turn where Gild is affordable the bezel and plate flash once. Reduce Motion and Reduce Flashing are respected. The coin art, the price, the shortcut (G or LT) and the rules are unchanged. The coin-burst from the gold counter and the armed thread to the hand were re-aimed for the new position. Code: GildedGildBadge.cs; a new in-Unity check confirms the badge clears End Turn, all three piles, the energy orb and every hand size from 1 to 12. |


---

## Batch 5 fix log (balance)

**Honest limit:** the test player is a simple greedy bot (block when threatened, otherwise the best damage per energy; three heroes; deck and relics sized to the act). It never saves block for a telegraphed big hit, never plans a combo and plays Vanguard far better than Hexer or Reaper. So its win rate says nothing about how hard the game is for a person. It is only good for ranking one encounter against its siblings, which is how it was used here. Every elite and boss was fought 120 times (3 heroes x 40 seeds) for the numbers below; formations 90 times. Nothing here has been play-tested by a human. Every change is small, and the numbers below are exactly what changed. Rules tests that pinned the old numbers were updated (harness: 107,920 checks, 0 failures; both compile configurations clean).

**What counted as "clearly broken":** an elite that the bot beat 98 to 100% of the time while losing only 26 to 43% of its HP (its siblings: 33 to 94% wins, 46 to 90% lost); an Act 3 elite beaten about as rarely as a boss (18 to 29%); or one ordinary enemy that dragged every formation it appears in 25 to 47 points below the median of that tier.

### Elites that were too easy (raised)

| Enemy | Change | Bot win / HP lost, before to after |
|-------|--------|------------------------------------|
| The Sunken Engine (Act 1) | HP 124 to 132. Pressure Strike base 10 to 14 (still +2 per Pressure, so the strikes now show 16 / 20 / 22). Burst Valve 7 x 3 to 9 x 3. | 100 / 31 to 97 / 51 |
| The Royal Auctioneer (Act 1) | HP 94 to 118. Hammer Fall 12 to 16. Lot of Blades 19 to 24. | 100 / 26 to 97 / 49 |
| The Pale Gardener (Act 2) | Thorn Seed 20 to 28. Weeding Cut 13 to 19. | 100 / 34 to 89 / 57 |
| The Fallen Comet (Act 2) | Comet Strike 12 + 2 per Momentum to 16 + 2. Falling Arc 2 x (6 + Momentum) to 2 x (8 + Momentum). Impact 26 to 32. | 98 / 43 to 82 / 62 |

### Act 3 elites that were as lethal as bosses (lowered)

| Enemy | Change | Bot win / HP lost, before to after |
|-------|--------|------------------------------------|
| The Worldbreaker (neutral) | HP 252 to 224. Crushing March 20 to 18. World Break 42 to 38 (the on-screen text and tooltip say 38). | 18 / 94 to 42 / 88 |
| The Royal General | HP 208 to 184. General's Advance 18 to 16. Royal Execution 26 / 23 to 24 / 21 (tooltip updated). | 18 / 92 to 31 / 88 |
| The Choir Eternal | HP 176 to 158. Broken Choir 20 to 18. Blade Verse (Voice) 10 to 9. | 24 / 91 to 31 / 88 |
| The High Confessor | HP 198 to 178. Condemn Violence 22 to 20. Break the Wall 25 to 22. Punish Hesitation 19 to 17. | 29 / 92 to 42 / 88 |

### Ordinary enemies that made every formation they join brutal (lowered)

Each of these attacks every single turn or stacks pressure, and showed up in formations 25 to 47 points below their tier median. The change is the same in solo and group fights.

| Enemy | Change | Mean bot win over the formations it appears in |
|-------|--------|-----------------------------------------------|
| Crooked Oracle (Act 2 neutral) | HP 70 to 64. Full Measure 13 to 11. Left Unspent 19 to 17. | 37 to 49 (8 formations) |
| Crownless Duelist (Act 1) | HP 58 to 54. Relentless Advance 13 to 11. Measured Cut 10 to 9. | 46 to 56 (4) |
| Iron Wanderer (Act 3 neutral) | HP 96 to 88. Crushing Step 17 to 15. Breaker 24 to 21. | 16 to 24 (9) |
| Nameless Seer (Act 3 neutral) | HP 94 to 86. Veiled Strike 16 to 14. Sentence 25 to 22. | 21 to 32 (8) |
| Pale Chimera (Act 3 neutral) | HP 100 to 92. Rending Maul 20 to 18. | 23 to 30 (8) |
| Royal Adjudicator | HP 80 to 74. Judicial Strike 16 to 14. Royal Sentence 13 to 12. | 20 to 26 (10) |
| Crown Duelist | HP 88 to 82. Piercing Advance 21 to 18. Perfect Measure 16 to 14. | 24 to 34 (9) |

Group HP follows from these automatically (the existing 90 / 92 / 94% rule): for example Crooked Oracle 59 in a group, Iron Wanderer 83, Pale Chimera 86, Nameless Seer 81, Royal Adjudicator 70, Crown Duelist 77, Crownless Duelist 49.

### Measured on the whole game (bot, mean win rate)

Act 3 elites 31% to 39%, Act 3 Advanced formations 44% to 47%, Act 3 Standard 55% to 56%, Act 1 Dangerous 57% to 59%. Everything else moved by 1 point or less (the sample for these aggregates is small, so differences under about 5 points are noise).

### Looked at and deliberately left alone

* **Bosses.** The bot wins 5 to 31% against every boss, Act 1 as well as Act 3, so it cannot tell us whether they are fair. No boss number was changed.
* **Dangerous three-enemy formations.** Several still win 0 to 12% for the bot (for example A3-NO3, GT-D06, BC-D04, BC-D10, CF-D07, DQ-D09). Dangerous is meant to be risky, and each member of these formations is fine alone, so trimming them would be guessing. They are the first thing to try by hand.
* **GT-S10 (Royal Adjudicator + Crown Duelist) is still about 9%** even after both were trimmed. Both enemies attack every turn. If it feels unfair when you try it, lower one of the two again.
* **Blind Seer, Living Treasury, Wayfarer and Ferryman** are now the easiest elites of their acts (92 to 94% bot wins) but are not outliers any more, so they were not changed.
* **Hexer and Reaper win far less than Vanguard in the bot's hands** (for example 2 of 40 against the Royal General versus 28 of 40 for Vanguard, 7 of 40 for Reaper). That is mostly the bot's greedy play suiting Vanguard, not proof of a hero imbalance, but it is worth checking by hand with Hexer against the Royal General and Choir Eternal.
* Bot tooling (outside the game): probes for solo enemies, elites and bosses, formations and per-enemy move counts. They are not shipped.

## Batch 6 fix log (card-hover shake, tooltip cleanup)

### Hover shake (a regression from Batch 4, fixed)

Hovering a hand card made it shake violently, so its text could not be read. This was introduced in Batch 4, not in the original game. The cause: neighbouring cards step aside for the raised card (up to 18 px, based on the distance to the raised card's current position). While restructuring the raise and aim-lean logic, the raised card itself fell into that same branch. The push depends on which side of itself the card is on, so every frame it flipped direction: a bang-bang oscillation of 3 to 14 px. Fix (`GildedCombatPresentation.cs`): the raised card (hovered or selected) never takes part in the neighbour push; only its neighbours do. A numerical simulation of the old and new logic shows the settled position moving 3 to 14 px per frame before and 0 px after.

A new in-Unity check was added to `GildedCombatVerification.cs` (3, 5, 8 and 10 card hands): hover a card, wait, sample its position for 30 frames and require it to move less than 0.5 px. **This check could not be run here** (it needs the Unity editor); the fix itself was verified by simulation and by compiling.

### Tooltips: shorter and cleaner

* **Keyword help** (`RuleKeywords`, all 40 entries): one short line each, no more than 110 characters, numbers unchanged. The Fortify text still says "triggered effects" and the Resonance text still says "no stack cap" (both are checked by existing tests).
* **Card tooltip layout** (`GildedPolishedTooltips.cs`): no repeated card name and no divider line (the card is already on screen), entries are `TERM  one line` with a thin gap instead of a blank line, and the live-values block is headed `NOW`. A tooltip with no title is now supported. Attachment/badge tooltips keep their own heading.
* **Live values** (`CombatReadability.cs`): replaced a 6 to 10 line explanation ("current card base", "Previewed through the live combat resolver", and so on) with one formula line such as `6 base +2 Strength ×0.75 Weak ×1.5 Vulnerable = 10 damage`, then only the lines that matter (absorbed / HP, triggered damage or Block, cost change, condition active or not). The numbers still come from the same resolver; only the wording changed. `+ bonuses` stands in for relic, Shard and card-copy modifiers.
* **Source / Duration** lines on card badges, status effects and Aspect chips are now one dim line (`Source: X · Duration: Y`) instead of two lines under the text.
* **Enemy tooltips:** all 25 generic icon tips (`EnemyIconRules.Tip`) shortened; ten of the longest per-enemy rule lines trimmed (High Confessor and other Judgment lines, Choir Eternal, Husk, Oracle, Sovereign, Binder, Treasury, Duelist response); Seized Gold, Heat, Minion, pile, Energy, health, Block, floor, act, gold, deck, settings, map and Gild tooltips shortened. No rule or number changed, only wording. Tests that pin tooltip wording still pass.

### Not changed

* The remaining per-enemy rule lines (about 60) are one line each and many are pinned by tests, so they were left. If any still feel wordy in play, say which and they can be trimmed the same way.
