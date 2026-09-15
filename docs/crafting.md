# Crafting

The client's crafting window (`craftingwindownew.py`) opens on a Kraftwerks station and has five
pages: fabrication, salvage, extraction, integration, upgrade. Fabrication - a schematic plus
components makes an item - is implemented. The other four are Crafting v2, which needs per-item
modules the server does not model yet; they answer with a failure the window can show and a
system message.

The roadmap and the analysis behind it are in Rasa\CRAFTING.md.

## Stations

`kraftwerks` rows (39 seeded from the client's CRAFTING_STATION markers) become usable dynamic
objects of class 9595. Using one sends `Use`, which opens the window, followed by
`CraftingStatus` with the player's jobs at that station. `.kraftwerks` lists, creates, moves and
deletes stations (GameMaster).

## Recipes

`recipe` (id = the schematic's item template, `energy_cost` in credits, `kraftwerks_seconds`,
`result_template_id`, `result_amount`, `min_level`) and `recipe_input` (`recipe_id`,
`input_class_id`, `quantity`) hold the client's `generated.shared.recipe` data: 160 recipes,
353 ingredients. An ingredient names an entity class, so any item template of that class
counts, and stacks are pooled. `RecipeManager` loads them once.

## Fabrication

`RequestCraftItemNew(station, schematicEntityId, page 1)` - or the older
`RequestCraftItem(station, schematicTemplateId)` - goes through the shared rules of the
client's `shared/crafting.py`, which the original server ran too: the station must be within 5 m,
the schematic in the player's inventory, the player at the recipe's level, holding every
ingredient in the stated quantity and able to pay the cost, with no job of theirs still running
at that station (and fewer than 301 waiting). Then the ingredients are removed (smallest stacks
first), the credits taken, and a job with the recipe's time is put on the station for that
player. The schematic is not consumed; the rules never touch it.

`CraftingStatus` is sent with the job and `CraftingSuccess` follows. The client does not count
the job's time down; it hides the window while a job has time left, and the server sends a fresh
`CraftingStatus` when the job finishes (from the dynamic-object worker) so the Take button
appears when the station is used again. `RequestRetrieveFinishedCraftItem` /
`RequestRetrieveAllFinishedItems` create the result - `CreateFromTemplateId` with the player's
family name as crafter, in stacks of the class's stack size - and add it to the inventory. What
does not fit stays on the job with an inventory-full message, to be taken again after making
room.

Jobs live in memory, keyed by station and character: a restart loses jobs in progress and
finished items not yet taken. Persisting them is a later step.

## Testing

Give yourself a schematic and its inputs, stand at a station and use it:

```
.giveitem 42227          Schematic: Laser Pistol (100 credits, 10 Spore Micromech, 8 s)
.giveitem 638 10         Spore Micromech
.giveitem 641            Schematic: Standard Grade Cartridge Ammunition (500 Scrap Metal, 5 s)
.giveitem 1765 500       Scrap Metal
.giveitem 45072          Schematic: Class I Basic Med Pack (10 Consumer Grade Micromech, 40 High Grade Pharmaceuticals)
.giveitem 45107 10
.giveitem 110908 40
```

`world/teleport_kraftwerks.txt` in the Rasa folder lists a station on every map that has one.
