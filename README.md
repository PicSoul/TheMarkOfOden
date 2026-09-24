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

Each creature asks two questions about you, and never at the same time.

**Will it pick a fight with you?** It weighs your mark against its own nerve: its tier, its star
level, how many of its kind are standing beside it, whether it is night, and anything another mod has
done to make it deadlier. Outrank it and it **leaves you alone**: it won't attack you, and it won't run
from you either. It just gets on with its day. Don't outrank it and it behaves exactly as it does in the
base game.

**If a fight starts, is it losing?** Nothing runs at the sight of you. Hit a creature you outrank and it
fights back, but once it is badly hurt its nerve **breaks** and it runs. The further you outrank it,
the sooner that comes. A creature that matches or outranks you never breaks; it fights to the end.
Once a broken creature has run far enough it settles and leaves you alone. Hit it again and it breaks
again at once.

| What you see | What it means |
|---|---|
| **Normal** | You don't outrank it, so it behaves exactly as in the base game. |
| **Wary** | You outrank it. It will not start a fight with you, but it defends itself if you start one. |
| **Fleeing** | It was losing a fight to you and its nerve broke. |
| **Provoked** | You hit it, and it is fighting back. |

**Hunted animals fight to the end.** Anything you hunt for meat may leave you alone, but provoked it
never breaks, so a hunt is a fight and never a chase. Which creatures those are is worked out from their
own drop tables (anything that drops something edible, or something a cooking station turns into food),
so it covers creatures this mod has never heard of. That includes the dangerous ones: wolf, lox, serpent.
Juveniles are classed with whatever they grow into.

A few monsters happen to drop something a cooking station can use (the Seekers carry royal jelly, and the
Deep North Fuling carries meat) without being anything anyone hunts, so they are listed in
`Not hunted animals` and break like any other monster. `Never flee creatures` works the other way round.

Deer and hare are untouched: the game has them run from everything, and this mod leaves them to it.

**The last one standing breaks.** A creature's nerve is read as the fight goes on, so as its packmates
fall around it, it breaks sooner. Starred creatures are braver than their common kin.

**The night is still dangerous.** Between dusk and dawn every creature finds a little more nerve.
Something that would leave you alone by day may come for you after dark, and something that would break
early holds on longer. `Night courage` sets how much; 0 makes the night no different from the day.

**Raids still come for you.** Creatures spawned by an active raid ignore all of this, however fearsome
you are, so a raid is still a raid. Anything hunting you specifically is likewise undeterred.

**Boss fights stay boss fights.** Creatures near an alerted boss ignore all of this, so the adds a boss
summons keep coming instead of losing their nerve halfway through and handing you the fight.

**Taming is untouched.** Once an animal has eaten your food it is already being tamed, and from that
moment it behaves exactly as it does in the base game, so taming works the way you already know at any
mark.

**Their kind come with them.** Hit anything and others of its own kind close enough to hear join in, so a
pack does not stand and watch you battle one of its members. This is tracked per attacker, so your friend
picking a fight does not make them angry at you.

### Every creature's name

The config's creature lists want the name the game's files use, which is not always the one you see in
game. `picsoul.valheim.markofoden.creatures.txt`, beside the config file, lists every creature with the
name to use, grouped by biome. It is rewritten each time a world loads, so it includes creatures other
mods add. **It lists everything, spoilers included.** Creatures that exist in the game's files but that
nothing in normal play ever spawns are left out.

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
| green `▼ wary` | it will not start a fight with you |
| cyan `▼▼ fleeing` | it was losing a fight to you, and its nerve broke |
| amber `▲ provoked` | you hit it, so it is fighting back |
| amber `▲ unafraid` | your name means nothing to it, and it is armed |

The **word** says what the creature is about to do, so there is nothing to learn on your first
creature. The **arrow** points the way it is about to move: down for one keeping away from you, up for
one that will come at you. The **colour** is cool for the two that will not fight you and warm for the
two that will.

Every state is the same rung on all three, so there is no combination to decode and any one of them is
enough on its own — which is what makes it readable across a field, in a fight, or without colour
vision. Once the markers mean something to you, set `Nameplate labels` to `Marker` and the words go
away. All three are config strings, and the colours stay clear of vanilla's own meanings for red,
orange and yellow.

An unmarked plate is not the absence of an opinion, it is the opinion "nothing here has changed", and
that reads as safe. It is safe on a deer, which has no attack and runs from you in the base game
anyway. It is not safe on a lox, which is equally food and equally unmoved by you and will kill you
for walking up to it. So a creature that is unafraid *and* armed says `unafraid` rather than nothing.
By default that covers what you would kill for meat, which is what you approach on purpose; `Mark
unafraid creatures` widens it to everything that can fight, or turns it off. Creatures with no attack
at all are never marked, whatever it is set to, so a forest of deer stays quiet.

Whether a creature counts as armed is read from the creature itself rather than a list of names, so
anything another mod adds is judged by the same rule. `moo tiers` shows it as `ARMED` or `HARMLESS`.

The label is the creature's opinion of you, not a statement that it has noticed you — something that
has not seen or heard you yet still shows how it would feel.

Vanilla only draws name plates within 10m, which is late to learn that something is afraid of you.
`Nameplate distance` in the config raises that if you want to read the mood of a forest before walking
into it. It is off by default because it widens every name plate, not just frightened ones.

## In-Game Standings & Stats Viewer

Check your progress, boss statuses, and species fear standing at any time:
- **Hotkey**: Press **`F4`** (configurable) to toggle the Standings & Lore panel.
- **Inventory Button**: Click the **"Mark of Oden"** button embedded directly in your inventory screen.
- **Strict Spoiler Protection**: Only biomes your character has personally explored are visible. Unvisited biomes, future bosses, and undiscovered creatures remain concealed to preserve discovery.
- **Live Metrics**: Shows your current Mark tier, lifetime kills per species, notoriety progress, and live dispositions (`Wary`, `Unafraid`, or `Harmless`), with how early each would break if you picked a fight.

## Progression Notification Popups

Whenever you slay a boss or reach a species notoriety threshold (e.g. 25, 100, 400 kills), an animated Norse HUD banner appears displaying the trophy artwork, new fear reactions, and an authentic fanfare cue.

## Compatibility

Nothing here is a hard dependency, and every patch is a postfix — this mod never suppresses another
mod's work or rewrites a game method's body. A few specifics are worth knowing.

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
- `moo bosses` — every boss, its rank, and whether this character has killed it
- `moo creatures` — a maintenance scan that follows every way the game spawns a creature and lists any that
  nothing spawns. Takes a minute or two; the report lands in `BepInEx\MarkOfOden-creature-scan.txt`
- `moo optout` — turn your own mark off, so nothing fears you; `moo optin` turns it back on
- `moo reset` — work your mark out again from scratch, if it ever looks wrong
- `moo tier <0-8>` — force a mark tier for testing (`moo tier -1` returns to your real one)

None of these are cheats and none of them need `devcommands`, so using them will not mark your
character. `moo optout` and `moo reset` are open to everyone: opting out only makes you less
frightening, and resetting only recalculates your own mark from what your character has actually done.
Neither can raise it. `moo tier` can, so on a server it is limited to the admin.

Opting out is remembered by the character, so it survives logging out, and your mark is kept for
whenever you opt back in. On a shared world it only affects you: other players still get the full mod.

## Configuration

Everything is tunable in the config file: when a creature leaves you alone, how much bolder creatures
are at night, how early a losing creature breaks, pack courage, notoriety milestones, how far a cry for
help carries, per-creature tier overrides, and which creatures count as hunted animals. Turn
`Creatures can break` off and everything that fights you fights to the end. Creature tiers are worked out from the prefabs themselves, so modded creatures get
a sensible tier automatically — use `moo dump` to see what was guessed and override anything that looks
wrong.

`Inherit world progress` is off by default. Turn it on if you would rather everyone on the server
benefit from the world's boss kills, the way older fear mods worked.

## Credits

Inspired by Revel's FleeOnSight and tulivu's FearMe.

Uses [ServerSync](https://github.com/blaxxun-boop/ServerSync) by blaxxun for server-authoritative
config, vendored as a source file; its licence is in `third-party/`. Everything else is MIT.
