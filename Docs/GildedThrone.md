# Gilded Throne (Act 3, Theme 3)

Identity: ROYAL ORDER, expressed per enemy (Minions, allies, isolation, Reserve, stance, countdown). It is not one universal status, and nothing intercepts player attacks.

- Content: `Core/GildedThroneContent.cs` (8 normals, Royal Guard / Blade / Shield Minions, 3 elites, boss The Sovereign, 30 formations GT-S01..D10).
- AI: `Combat/ThroneCombat.cs`, chained after the Fracture hooks.
- Reserve (Treasury Beast 0-4, Treasury Warden 0-6) is palace-owned and never touches player Gold. Siege, stances and the Duelist response are deterministic and always shown.
- Sovereign thresholds are proportional (288/430, 144/430); Phase 1 Minions withdraw at Phase 2 and give it nothing.
- Hooks: royal_order_support, crownshield_protection, commander_summon, royal_command, royal_guard_death, treasury_reserve_gain/spend, golden_stampede, duelist_response:X, thronebreaker_siege:N, thronebreaker_charge, royal_general_formation/reinforcement, treasury_warden_royal_barrage/emergency, crown_duelmaster_stance:X, sovereign_phase_1_to_2 / 2_to_3, royal_minion_withdrawn, end_of_the_crown.
- Rules in force: max 4 enemy bodies (overrides the prompt's 6), minions stand in front, size and hitbox follow importance.
