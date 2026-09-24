# Changelog

## 0.4.0

### Changed

- **Nothing runs at the sight of you any more.** Your standing now decides only whether a creature
  picks a fight: outrank it and it leaves you alone, neither attacking nor running. Running only
  happens in a fight. Hit a creature you outrank and it fights back, and once it is badly enough hurt
  its nerve breaks and it runs. The further you outrank it, the sooner that comes: at 15% health one
  step past wary, 10% sooner for every step beyond, up to half its health. A creature that matches or
  outranks you never breaks. Its nerve is read as the fight goes on, so it breaks sooner as its pack
  falls around it. Once it has run far enough it settles and leaves you alone.
- **Every hunted animal fights to the end.** Anything hunted for meat - boar and neck, and wolves, lox
  and serpents too - may leave you alone, but provoked it never breaks, so hunting stays a fight rather
  than a chase. Deer and hare are unchanged: the game has them run from everything, and this mod leaves
  them to it.
- **Seekers and the Deep North Fuling are not hunted animals**, even though they drop something a
  cooking station can use. They break like any other monster. `Not hunted animals` holds the list.
- **Nights are more dangerous.** Every creature gains courage between dusk and dawn, so something that
  leaves you alone by day may come for you after dark, and one that would break early holds on longer.
- **Creatures from other mods are rated by where they spawn.** A creature with no hand-tuned tier used
  to be judged by its faction and health alone, so a creature mod that puts the same shark in the
  Meadows and the Mistlands got the same tier for both. Where the world spawns a creature now counts
  double, and it also decides which biome the creature list and the standings viewer file it under.
- The name plate reads wary, fleeing, provoked or unafraid. "Cornered" is renamed "provoked", since
  that is what it always meant; a config still on the old wording is updated automatically.

### Added

- **Every creature's name, in one file.** `picsoul.valheim.markofoden.creatures.txt` beside the config
  lists every creature with the name the config's lists want, grouped by biome and tagged hunted,
  harmless or boss. It is rewritten each time a world loads, so it includes creatures other mods add.
  It lists everything, spoilers included, except creatures nothing in normal play ever spawns.
- New settings: `Night courage`; `Not hunted animals`; and a Morale section with `Creatures can break`,
  `Break health`, `Break step`, `Break health cap` and `Run time`.
- `moo why` shows where a creature would break if you provoked it, and whether night is making it
  bolder. `moo dump` shows where each creature spawns and how its tier was worked out.
- `moo creatures`, a maintenance scan that follows every way the game puts a creature in the world
  (world spawns, raids, every location and dungeon room, boss altars, breeding, eggs, anything a
  creature or weapon summons) and reports what none of them reach.

### Removed

- `Afraid threshold`, `Terrified threshold`, the whole Cower section and `Food animal max tier`, along
  with the states they controlled. BepInEx leaves settings a mod no longer uses in an existing config
  file; these do nothing now and can be deleted.

### Fixed

- Trolls, Stone Golems and Krigen were treated as having no attack, because their weapons come as one
  of several whole kits and that was the one place the check did not look. The standings viewer said a
  troll "has no way to fight", and none of them were ever marked unafraid on their name plate.
- The standings viewer worked its verdict out for itself and skipped the hunted-animal rule, so it
  could say a creature would do something it would not. It now uses the same rules the creatures do,
  shows where each would break, and says when the night is making them bolder.
- The standings viewer put some creatures under the wrong biome, which could show a Deep North creature
  in an earlier biome's tab. Where each creature belongs now comes from where the game actually spawns
  it.
- The viewer's "Harmless Prey" label never appeared: it compared names that were never spelled the
  same way. It now asks whether a creature has any attack at all.

## 0.2.0

### Added

- **A standings and stats viewer**, on `F4` or from the "Mark of Oden" tab in the inventory. Biome
  tabs, boss statuses and a fear table per species, with a search across all of it. A biome your
  character has not visited shows nothing at all, so the window cannot spoil what is out there.
- **Progression banners** on a boss kill or a species milestone, with the trophy artwork and a
  fanfare. Both the banner and its sound can be turned off, and how long it stays is yours to set.

### Fixed

- The progression banner appeared in the middle of the screen rather than near the top, which put
  a panel across the fight that earned it. Its own anchor was right; what it was anchored inside
  was a hundred pixels tall and pinned to the centre, so "near the top" meant near the top of that.
  `Popup screen height` now decides where it sits, and is read fresh for each banner.
- The banner no longer fills the log with font warnings. Each of its labels woke before it had been
  given a font, so TextMeshPro reached for one the game does not ship and said so every time.

## 0.1.0

First build, for Valheim 1.0 (Deep North). Everything below works in single player and has been played
rather than only compiled; multiplayer is what this build exists to test.

- Creatures weigh what you have done against their own nerve, and either ignore you, flee, or break
  entirely. Your mark comes from bosses you personally helped kill and from how many of a species you
  have killed, not from the gear you are wearing.
- An existing character keeps its history: both are read from records the game already keeps per
  character, so nothing has to be earned again.
- Full multiplayer support. Each player's mark travels with them, so creatures fear the veteran and
  attack the newcomer standing beside him.
- Creatures in a mob hold their ground; the last one standing breaks. Starred creatures are braver.
- Raids, boss fights and anything hunting you ignore fear entirely.
- Anything you hit defends itself, and its own kind nearby join in rather than watching a fight happen
  beside them. They stay in the fight until it is over rather than on a timer, and go back to being
  afraid once they lose you. Hunting never becomes a chase. Food animals never flee at all,
  and taming behaves exactly as it does in the base game.
- Frightened creatures are marked on their name plate.
- Any player can turn the mod off for themselves with `moo optout`, and back on with `moo optin`, on a
  server where everyone else keeps it.
- Creature tiers, which creatures are hunted for food, and how young creatures relate to adults are all
  worked out from game data, so creatures added by other mods are covered without any configuration.
- Works alongside Creature Level and Loot Control: creatures it has infused or empowered hold their
  nerve for longer.
