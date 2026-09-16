# Changelog

## 0.1.0

- First build for Valheim 1.0 (Deep North).
- Mark tier earned from bosses you personally helped kill, per character.
- Per-species notoriety from your own kill counts.
- Cautious / Afraid / Terrified reactions, with a cower state when cornered.
- Pack courage: creatures in a mob hold their ground, stragglers break.
- Raid creatures and anything hunting you ignore fear entirely.
- Full multiplayer support: marks are published per player, config is server-enforced.

## 0.1.1

- Fear now reaches creatures that already had you targeted, not just new ones (vanilla keeps an acquired target when FindEnemy returns nothing).
- Boss credit for existing characters is worked out from Valheim's own kill history instead of boss trophies, so it no longer depends on who looted.
- Anything you hit defends itself, at every fear level, instead of having to be chased down.
- Retaliation is tracked per attacking player.

## 0.1.2

- Frightened creatures now show their state on the vanilla name plate: tinted name plus a symbol.
- Optional Nameplate distance setting, since vanilla only shows name plates within 10m.

## 0.1.3

- Reworked the name plate colours: cool shades mean a creature will not fight you, warm means it will, avoiding vanilla's meanings for red, orange and yellow.
- A creature fighting back because you hit it is now marked, so an unmarked plate always means it has no interest in you.
- Colours and markers are both configurable.

## 0.1.4

- Name plates now use one escalating marker for the degree and colour only to separate backing off from fighting back, instead of varying both.
- Renamed the two display settings, because BepInEx keeps a key already present in your config file and older installs would otherwise have kept the previous markers.

## 0.1.5

- Added a Colour names setting, so the marker can be shown without tinting the name. An inline colour tag overrides whatever colour another mod set on the text, and BetterUI already colours names by alert state.
- The log points this out when BetterUI is installed.

## 0.1.6

- The BetterUI name colour notice now reads that mod's own setting, so it only appears when the two features actually clash.
- Documented the BetterUI interaction and both ways to resolve it.

## 0.1.7

- Food animals (boar, deer, hare, hens) ignore fear by default: hunting should not become a chase, and taming only progresses while an animal is not alerted.
- moo dump now shows each creature's faction and whether it is on the fearless list.

## 0.1.8

- An animal that has eaten your food is exempt from fear until it is hungry again, so taming behaves exactly as it does in the base game. Hostile tameables such as wolves still fear you in the wild.

## 0.1.9

- Added Never flee creatures: they may lose interest in you but never break and run. Food animals moved onto it, instead of being exempt from fear altogether, so they neither chase-flee nor charge a late-game player.

## 0.1.10

- Creatures hunted for food are now identified from their own drop tables rather than a fixed list, so necks and anything else edible are covered, including creatures added later.
- Food animal max tier keeps dangerous meat sources such as wolf, lox and serpent in the fear system.

## 0.1.11

- Food detection now counts drops that have to be cooked before they feed anyone, so necks and anything else with a cook-only drop are covered.

## 0.1.12

- Juveniles inherit their adult form's classification, read from the game's own grow-up link, so piglets no longer bolt from a player the boars beside them are ignoring.

## 0.1.13

- Creature tiers are keyed by prefab rather than by creature name. The Deep North reskins Greydwarves under the same name, and sharing a key meant the Black Forest original was scored with its far deadlier cousin's courage, so it never backed down.
- moo dump shows every tier in use for a shared name, so a collision like that is visible.

## 0.1.14

- Moose and seal never flee by default. Players hunt both, but every Deep North creature is rated dangerous by the tier heuristic, so neither is detected automatically.
- moo dump now shows each creature's health, its prefab names for writing config entries, and flags creatures that drop food but were excluded by the tier gate.

## 0.1.15

- Config settings whose defaults change are now carried forward on update, but only where the value was still the old default. BepInEx keeps whatever is already in your file, so a changed default previously reached nobody who had run the mod before.
- Seeker broods were rated as brave as full Seekers despite having 20 health; corrected.

## 0.1.16

- Young creatures inherit their adult form's classification through breeding links as well as grow-up links. A moose calf knows what it becomes, but a seal pup does not and is only reachable from the seal that breeds it.

## 0.1.17

- Creatures near an alerted boss ignore fear, so summoned adds keep fighting. They were covered by no other exemption: the ability bosses summon with alerts what it spawns but never marks it as hunting the player, and adds belong to no raid.

## 0.1.18

- The creature table is rebuilt once loading is complete. Mod frameworks register their creatures from a postfix on the same method this mod used, with no guaranteed order between them, so whether modded creatures were known came down to plugin load order.
- The log reports how many creatures were rated by heuristic rather than from the built-in table.

## 0.1.19

- Added Star courage cap. Level mods raise creatures far past vanilla's three stars, and uncapped, stars alone would outweigh everything else and quietly switch fear off on those servers.
- The log warns when Creature Level and Loot Control is installed and this mod's name plate distance is also set, since both assign the same field.

## 0.1.20

- Creatures that another mod has infused or given a special ability now get extra courage, so an empowered creature no longer flees like the ordinary version of itself. Read from the creature's own saved data, so it needs no dependency and works for creatures owned by another player.

## 0.1.21

- A server no longer rejects clients running a different version of this mod. The check was pinned to the exact current version, so every release would have locked out anyone who had not updated yet. It now only requires a version new enough to agree on what crosses the wire.

## 0.1.22

- A server running this mod now requires it of connecting clients, and requires their version to match its own.
