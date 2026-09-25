# NhxQualityPack

A private mod for personal use with friends - not published on Thunderstore.

A collection of quality-of-life features. Automatically store items from your inventory into nearby chests you have access to, within a configurable radius. Also sorts your inventory and any chest you open, lets you lock item types so they're never auto-deposited, swaps hotbar rows, and pulls building/crafting materials and fuel from nearby chests.

This mod replaces **NSimpleDeposit** - remove it when installing this one. Item locks made with NSimpleDeposit carry over; config settings do not (the config file is now `NhxQualityPack.cfg`).

The quick stack/sort/lock features were originally forked from Hex_Viking's **HexQuickStackStorage** (itself inspired by Goldenrevolver's **Quick Stack Store Sort Trash**), then diverged enough - dropped the trash/delete feature, added item locking, and access to any chest you have rights to rather than only ones you built - to become their own mod.

## Instructions

### Quick Stack

Click the **Q** button in your inventory or use the configured Quick Stack keyboard shortcut (default `P`).

Items will only be moved if:

- The item is not equipped
- The item is not in your hotbar
- The item is not marked as **locked**
- The item is inside the normal player inventory
- A nearby chest you have access to already contains that item type
- The chest has at least some room for the item
- Nobody else currently has that chest open

Items will not be stored in empty chests or chests that do not already contain that item.

If a chest only has room for part of a stack (e.g. topping off an existing stack of 35/50), that partial amount is deposited and the rest stays in your inventory, rather than skipping the item entirely.

When triggered via the keyboard shortcut, a message is shown summarizing what happened (the **Q** button doesn't show this, since you can already see the result in the inventory UI):

- `All items deposited` - everything that could be moved was moved.
- `x/y items deposited` - only part of what you carried was moved. `y` is every item you held that was worth considering, including ones that don't yet exist in any nearby chest; `x` is how many of those actually got deposited.
- `No room for items` - some of your items already exist in a nearby chest, but none of those chests had space.
- `No matching containers` - none of your items exist in any nearby chest yet. Quick Stack only tops off an item type a chest already holds; it never seeds a chest with a type it doesn't have.
- `No available containers` - no accessible chest was found within the search radius.
- Nothing is shown when there's nothing in your inventory worth considering at all (e.g. it's empty, or everything left is equipped or locked).

### Sort

Click the **S** button to sort your inventory.

If a chest is currently open, the chest will also be sorted, and any player-built chest you have access to is also sorted automatically the moment you open it.

In your own inventory, the hotbar, equipped items, and locked items will not be moved. Chests are sorted completely: locks only apply to your own inventory, so locked item types inside a chest are sorted like everything else.

Partial stacks of the same item type and quality are combined into as few stacks as possible first (e.g. two stacks of 10 Wood become one stack of 20), freeing up slots before the remaining items are placed. Equipped items, hotbar items, and locked item types are never topped up or drained by this - they stay exactly as they are.

### Lock Items

Hold **Alt** and **Right Click** an item to mark that item type as locked.

Locked items will have a blue border around them.

Locked items are:

- Skipped by Quick Stack, so they will never be automatically moved into a chest
- Left in place when sorting your inventory, just like equipped and hotbar items (locks don't apply to chests being sorted)
- Left in place when a fireplace, cooking station, smelter/kiln, or shield generator automatically pulls fuel or ore from your inventory (see [Build & Craft From Containers](#build--craft-from-containers)) - the same item type is still pulled from nearby chests as normal

Alt + Right Click the item again to remove the lock.

Once an item type is marked as locked, newly picked up items of that same type will also be locked.

Lock selections are saved per character (stored in that character's own save data, alongside things like skills and known recipes), so they follow that character into any world but are not shared with your other characters.

### Hotbar Swap

Press the hotbar swap shortcut (default backtick) to swap your hotbar (the first inventory row) with the second inventory row. This lets you keep two hotbar setups, such as tools and weapons, and switch between them without opening the inventory. Press it again to swap back.

- Only the first two rows are swapped; the rest of your inventory is untouched
- Locked items are not left behind: they move with their row like any other item
- Equipped items stay equipped and simply move with their row
- A `Hotbar swapped` message is shown so an accidental key press is noticeable
- The shortcut is ignored while typing in a text field

### Build & Craft From Containers

While building pieces or crafting at a workbench/station, materials missing from your own inventory are automatically pulled from nearby chests you have access to - the same range and access rules as Quick Stack apply (see [Container Access](#container-access)).

Feeding fuel, ore, or ammo also pulls from nearby chests when your own inventory doesn't have any:

- Fireplaces (campfires, hearths, bonfires) - fuel
- Smelters, charcoal kilns, and blast furnaces - ore and fuel
- Battering rams - wood (any of the wood types it burns)
- Cooking stations (cooking station, iron cooking station, stone oven, and similar) - fuel only (food to cook still has to come from your inventory)
- Shield generators - fuel (any of the bone types it accepts)
- Turrets - ammunition

For fireplace fuel, shield generator fuel, smelter/cooking station fuel and ore, and battering ram wood specifically, a [locked](#lock-items) item type in your own inventory is never spent automatically - only nearby chests are used to top it up. This does not apply to building pieces or manual crafting, which use locked inventory items like any other.

If a fuel station can't be refuelled because no usable fuel was found, a message explains why: `Inventory items locked` when the only fuel available is a locked item type in your own inventory, or `Unable to find fuel` when there is none in your inventory or in nearby chests. This applies to fireplaces/lights, smelters/kilns/blast furnaces, cooking stations, and shield generators. Adding ore to a smelter/kiln or wood to a battering ram shows `Inventory items locked` the same way when the only accepted items left are locked ones in your inventory.

The crafting/building requirement panel always shows the combined inventory + nearby-container amount (e.g. `200/4` if you need 4 wood and have 200 spread across your inventory and nearby chests combined), instead of vanilla's plain required-amount display. It's highlighted when nearby containers are what's making up the difference.

If another installed mod that also pulls from nearby containers has already paid for a build or craft, this mod leaves it alone instead of paying a second time. Running two mods that do the same job is still best avoided, since they can disagree about what's available.

Holding the fill-all modifier key (default **Left Shift**) while interacting with a fireplace/light, a smelter/kiln, a battering ram, a cooking station's fuel switch, or a shield generator fills it to capacity in one go - fuel or ore is pulled from your inventory first, then nearby containers, instead of adding one unit per interaction. A hover-text hint appears on these objects showing the key when using it would do something.

### World Chat

Normal chat messages are sent to every player in the world instead of only players nearby. This is on by default and can be turned off with the `World Chat` config setting.

- Messages look like the game's normal chat: normal case and colour, and no marker on the map. `/s` shouts are sent the same way, so they look like any other message
- The floating text follows the sender's head, or sits at the edge of the screen when they're out of view, without drifting upwards
- The sender's name is added in front of the floating text when they're further away than normal chat reaches (15 m) or out of view, so it's clear who said it. If they're too far away to be loaded, the text stays where they were standing when they spoke
- A map ping and a chat message from the same player both stay up, instead of one replacing the other
- Chat messages in the chat window start with the local time they arrived, in 24-hour format (e.g. `[14:05]`)
- A short sound plays when another player's chat message appears in the chat window (your own messages are silent). It follows the game's sound effects volume, messages shown together play it only once, and it can be turned off with the `Chat Sound Alert` config setting
- Chat messages that arrive while you're respawning after a death, loading into the world, or watching the arrival intro are kept and shown in the chat window once you're back, instead of being lost (vanilla discards them). Up to 50 messages are kept. Nearby-only chat from players who have World Chat turned off can't be kept this way.
- Messages that arrive while the screen is dark - lying dead, going through a portal, or sleeping - show up as usual, and the chat window pops open again once you can see, so they aren't missed behind the loading screen
- Whispers (`/w`) are unchanged
- Players without this mod still receive world chat messages as normal chat, but for them the floating text doesn't follow the sender or show their name

## Container Access

Quick Stack and chest sorting work with any player-built container you currently have access to, not only ones you personally built.

A container inside another player's ward that you aren't permitted in is skipped, the same way opening it by hand would be blocked. A container in your own ward, in a ward you're permitted in, or with no ward at all, is fair game. A chest someone has explicitly set to Private is also skipped unless you're its creator, again matching normal chest rules.

A chest another player currently has open is left alone entirely for that pass, rather than claiming it out from under them.

Naturally generated/world containers (e.g. dungeon loot) are always ignored.

## Multiplayer

- Client-side mod
- Does not have server sync
- Access to a container is checked using the same ward and privacy rules the base game uses
- Writing to a container claims network ownership of it first (mirroring how the game's own "Take All" does it), so items don't get silently lost when writing into a chest another peer currently owns

## Configuration

Configuration options include:

- Search radius (shared by Quick Stack and Build & Craft From Containers)
- Quick Stack keyboard shortcut
- Fill-all modifier key (default Left Shift)
- Hotbar swap keyboard shortcut (default backtick)
- World chat on/off (default on)
- Chat sound alert on/off (default on, needs World Chat on)
