# The Mark of Oden

### Dynamic Enemy Aggression & Fear Evaluation for Valheim

Ever wonder why a puny Greyling thinks it's a good idea to attack a Viking who has put down Eikthyr,
the Elder and Bonemass — and four hundred of that Greyling's own kin? The Mark of Oden restores common
sense to the creatures of the Tenth Realm.

## What marks you

Not your gear. A veteran who has slain every Forsaken is still a veteran in rags, and a fresh
character in borrowed plate is still fresh. Creatures judge you on two things:

- **Bosses you personally helped kill.** Participation counts, not just the killing blow — Valheim
  itself tracks everyone who fought a boss, and this mod uses that, so the player who spent the fight
  healing is marked alongside the one who landed the last hit. Credit is per character, so your
  friend's Eikthyr kill does not make *you* frightening.
- **How many of *that species* you have personally killed.** Greylings that have watched five hundred
  of their kin die at your hands will break long before a Greyling that has never met you.

**Your existing character already counts.** Both come from the kill history Valheim keeps for your
character, so everything you have ever killed — bosses included — is there the moment you install
this. It is credited by participation, so it does not matter who picked up the trophy. Nothing to
set up, no new character needed.

## How creatures react

Each creature weighs your mark against its own nerve — its tier, its star level, and how many friends
are standing next to it.

| Reaction | What it does |
|---|---|
| **Normal** | Vanilla. It has no idea who you are. |
| **Cautious** | Stops treating you as prey. Won't attack, won't run, just gets on with its day. |
| **Afraid** | Turns and runs. |
| **Terrified** | Runs — and when cornered, or when you are right on top of it, stops and cowers. |

**Raids still come for you.** Creatures spawned by an active raid ignore fear entirely, however
fearsome you are, so a raid is still a raid. Anything hunting you specifically is likewise undeterred.

**Boss fights stay boss fights.** Creatures near an alerted boss ignore fear, so the adds a boss summons
keep coming instead of losing their nerve halfway through and handing you the fight.

**Pack courage.** Creatures in a mob hold their ground. Thin the mob out and the last one standing
breaks. Starred creatures are braver than their common kin.

**Food animals lose interest but never run.** Anything you hunt for meat can stop caring about you, but
never breaks and flees however fearsome you are. One that bolts turns hunting into a chase; one that
charges a Viking who has killed every boss looks absurd. Standing there ignoring you is the only reading
that is neither — and they still defend themselves if you hit them.

Which creatures those are is worked out from their own drop tables — anything weak enough to be prey
that drops something edible, or something a cooking station turns into food — so it covers creatures
this mod has never heard of rather than relying on a list that goes stale. Juveniles are classed with
whatever they grow into, so piglets behave like the boars beside them rather than being judged on their
own empty drop table. Wolves, lox and serpents drop meat but are far too dangerous to qualify, so they
still lose their nerve. `moo dump` prints what it found, and `Never flee creatures` adds to it.

**Taming is untouched.** Wolves, lox and other hostile tameables do fear you, as they should. But once
an animal has eaten your food it is already being tamed, and from that moment it behaves exactly as it
does in the base game — so taming works the way you already know, at any mark.

**Cornered creatures fight back, and their kind come with them.** Hit anything and it defends itself for
a few seconds, however frightened it was — so hunting for meat and hides never turns into a chase. Others of its own kind close enough to hear join
the fight, so a pack does not stand and watch you battle one of its members. They stay in it for as
long as they are actually fighting you, and go back to being afraid once they lose track of you. This is tracked per attacker, so your friend picking a fight
does not make them angry at you.

## Multiplayer

Fully supported, including dedicated servers. Each player's mark travels with them, so creatures
correctly fear the veteran standing next to the newcomer and attack the newcomer — the case most fear
mods get wrong. Settings are server-enforced and take effect without a restart.

**Install it on every client.** Creature AI runs on whichever player's game owns that creature, so the
mod has to be there to change it. A dedicated server runs no creature AI and does not need it to work.

**Install it on the server too if you can.** Then the server owns the settings and everyone plays by the
same rules; without it each client uses its own config, and a player could tune their own fear. Settings
arrive without a restart.

**A server running this mod requires it.** Players without it are turned away, and so are players whose
version does not match the server's, so everyone updates together. That is deliberate rather than
strict for its own sake: fear is decided on whichever machine owns the creature, so on a server where
installs differ the same Greydwarf would behave differently depending on who happened to be simulating
it at the time.

A server that does not run the mod enforces nothing, and players who have it will still see it work.

## Seeing it

A frightened creature's name plate changes: the name is tinted and gains a marker.

| Plate | Meaning |
|---|---|
| unchanged | it has no particular opinion of you |
| green `<` | cautious — it will not start a fight |
| green `<<` | afraid — it runs |
| green `<<<` | terrified — it runs, and cowers when cornered |
| amber `!` | you hit it, so it is fighting back for a few seconds |

Green means it is backing away from you and the more arrows the further; amber means it will still
fight.

 The colour says which of the two situations you are in and the marker says how far along, so
there is no combination to decode. Both are config strings if you want different ones, and this stays
clear of vanilla's own meanings for red, orange and yellow.

The label is the creature's opinion of you, not a statement that it has noticed you — something that
has not seen or heard you yet still shows how it would feel.

Vanilla only draws name plates within 10m, which is late to learn that something is afraid of you.
`Nameplate distance` in the config raises that if you want to read the mood of a forest before walking
into it. It is off by default because it widens every name plate, not just frightened ones.

## Compatibility

Nothing here is a hard dependency, and every patch is a postfix — this mod never suppresses another
mod's work or rewrites a game method's body. Two things are worth knowing about.

**Mods that change creature AI.** Anything that rewrites how creatures pick or chase targets overlaps
with what this does, and the result depends on which mod acts last. That is not really avoidable for a
mod whose whole purpose is changing how creatures treat you.

**Creature Level and Loot Control.** Supported, with no setup. A creature it has infused with an
element or given a special ability is genuinely deadlier than its plain version, so it gets extra
courage and holds its nerve longer — see `Infusion courage` and `Special effect courage`. It also raises
creature levels well past vanilla's three stars, and since stars add courage, `Star courage cap` stops
that outweighing everything else; raise it if you want high levels to make creatures braver.

One thing to set by hand: CLLC also controls the name plate distance, the same field as this mod's
`Nameplate distance`, and whichever applies last wins. Leave this mod's at `0` and use CLLC's own range
setting. The log says so if both are set.

**Mods that add creatures.** These are handled without any work on your part. A creature this mod has
never seen is rated from its own data — faction and health for how dangerous it thinks it is, its drop
table for whether you hunt it, its breeding and grow-up links for its young — and the table is built
after every mod has registered its prefabs, so load order does not matter. Run `moo dump` to see what
was worked out and override anything that looks wrong in `Creature tier overrides`.

A modded boss grants a mark if its prefab sets a boss order, which is the same value the base game uses
to rank its own bosses. One that leaves it unset is treated as an ordinary creature and grants nothing.

**BetterUI.** Its `useCustomAlertedStatus` setting (on by default) hides the vanilla alert icons and
colours the creature's name by alert state instead. This mod colours the same name by fear, and an
inline colour tag wins, so the two would fight over it. Pick one:

- **Set BetterUI's `useCustomAlertedStatus` to `false`.** The vanilla `!` and `?` icons come back and
  carry alert state, and the name carries fear. Recommended: two separate signals, neither hidden.
- **Or set this mod's `Colour names` to `false`.** BetterUI keeps the name's colour for alert state and
  this mod shows only its marker.

Either way you can read both things at once. With both features on you get whichever wins per creature,
which is the one combination worth avoiding. The mod checks BetterUI's setting on startup and says so in
the log if they actually clash.

## Console commands

Type `moo` in the console for a summary. Useful ones:

- `moo status` — your mark tier, boss credits and most-killed species
- `moo why` — the full arithmetic for the nearest creature: threat, courage, pack bonus, verdict
- `moo dump` — print the resolved creature tier table, including modded and Deep North creatures
- `moo bosses` — print the boss keys this world uses, for filling in the config
- `moo reset` — work your mark out again from scratch, if it ever looks wrong
- `moo tier <0-8>` — force a mark tier for testing (`moo tier -1` returns to your real one)

None of these are cheats and none of them need `devcommands`, so using them will not mark your
character. `moo reset` is open to everyone: it only recalculates your own mark from what your character
has actually done, and cannot raise it beyond that. `moo tier` can, so on a server it is limited to the
admin.

## Configuration

Everything is tunable in the config file: the thresholds for each reaction, pack courage, cower
behaviour, notoriety milestones, per-creature tier overrides, and a fearless list. Creature tiers are
worked out from the prefabs themselves, so modded creatures get a sensible tier automatically — use
`moo dump` to see what was guessed and override anything that looks wrong.

`Inherit world progress` is off by default. Turn it on if you would rather everyone on the server
benefit from the world's boss kills, the way older fear mods worked.

## Credits

Inspired by Revel's FleeOnSight and tulivu's FearMe.

Uses [ServerSync](https://github.com/blaxxun-boop/ServerSync) by blaxxun for server-authoritative
config, vendored as a source file; its licence is in `third-party/`. Everything else is MIT.
