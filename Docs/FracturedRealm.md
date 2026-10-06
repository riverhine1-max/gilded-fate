# Fractured Realm (Act 3, Theme 2)

Identity: FRACTURE. Echoes, repeats, splits, mirrors, loops and futures that branch.

- Content: `Core/FracturedRealmContent.cs` (15 enemies, 3 elites, boss The Unmade, 30 formations FR-S01..D10).
- AI: `Combat/FractureCombat.cs`, chained after the Cathedral hooks. No off-turn interrupts. An Echo is a delayed repeat resolved on the enemy's own turn, its value fixed when created and saved.
- Rules in force: max 4 enemies in combat (summons are not chosen without a free spot); minions stand in front of their owner; size and hitbox follow importance. Act 3 rolls Vault, Black Cathedral or Fractured Realm.
- Hooks for VFX: echo_created, echo_resolved, action_repeated, splitling_split, split_echo_spawn/death, mirror_buff_copied, rift_binder_duplicate, phase_beast_form:X, oracle_pair_reveal, rift_colossus_stage:N, split_sovereign_threshold:X, fragment_spawn/death, fracture_command, loopkeeper_replay_start/finish, the_unmade_phase_1_to_2 / 2_to_3, unmade_pair_reveal.
- Art: `fractured_realm_*.tga` in `Resources/Art/Enemies` (The Unmade has `_phase2`, `_phase3`).
- Tests: engine suite (68,938 checks) covers each enemy, thresholds, pairs, reload, previews and simulated runs.
