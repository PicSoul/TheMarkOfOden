# Changelog

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
