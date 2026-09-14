# Gilded Fate gameplay calculation rules

This file records the replacement rules engine's deliberate stacking order. New cards, Relics, Bindings, Fateweaves, and Fate Shards should extend these layers instead of inventing a second damage or Block path.

## Attack damage — once per hit

1. Authored base value for the normal or upgraded card.
2. Permanent bonus on that individual saved card copy, then base-changing special modifications such as Perfected.
3. Flat per-hit combat additions: Serrated, Weighted, conditional card text, powers, stored next-Attack bonuses, Relics, and flat Fate Shard effects.
4. Percentage Fate Shard modifiers and cadence multipliers such as Rhythm.
5. Hand penalties such as Dread or Falter.
6. Strength.
7. Weak, then enemy Vulnerable.
8. Enemy-specific defenses such as Vault Mother wards.
9. Enemy Block, then HP loss.

Each multi-hit strike runs this order separately for each hit. Echo, Fateful, and Golden Echo repeat a card through a non-recursive play count; those copies cannot create another repeat of the same origin play.

Damage labelled as Burn, Retaliate, Consume, Chain Reaction, or another triggered source uses raw damage. Raw damage respects enemy Block but does not inherit Attack-only Strength, Weak, Vulnerable, or Attack Shard modifiers.

## Block — once per gain

1. Authored base Block for the normal or upgraded card.
2. Permanent Block bonus on that individual saved copy, then base-changing special modifications.
3. Fortify and flat card/Binding additions.
4. In-hand Frailty percentage reduction.
5. Relic and Fate Shard additions.
6. Final Block is added and recorded once for threshold triggers.

Threshold effects such as Crowned Bulwark and Counterweight read the actual final Block gained. Retaliate triggers only when an enemy hit absorbs at least one point of Block.

## Sigil and repeat safety

- The Hexer has exactly three ordered Sigil slots. Effects that do not ask for a choice use the leftmost Sigil.
- Ember activation applies Burn; Hex activation applies Marked; Echo activation arms the next card to repeat once. Each activation grants one Resonance unless card text adds more.
- Echo-generated plays, Golden Echo, and Fateful add finite copies to the current pending play. A repeated copy does not recursively schedule the same repeat trigger.
- Power-trigger chains have explicit once-per-turn or once-per-combat memory flags where their text requires one.

## Engine-scope interpretations

Current encounters have one enemy combatant. Text that affects all enemies therefore affects that combatant. Everlasting Ember has no second living target in the current encounter architecture and safely does nothing when the only enemy dies. The post-Act III Fateweave is treated as the final fate seal before victory; Acts I and II proceed to a newly generated map after their Fateweave.
