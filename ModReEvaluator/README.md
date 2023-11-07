# ModReEvaluator

ModReEvaluator recalculates all values and weights of weapon and armor mods and their resulting loose mod, based on crafting components used by the Constructible Object. It can also add components to loose mods, scaled with ModScrapScalar keywords to make them scrappable.

ModReEvaluator should be generated from scratch on every run, please use [CleanESL](http://cleanesl) as a first step, or manually delete the resulting ESP before re-running. By design, ModReEvaluator does not generate any new forms, and is safe to remove at any time.

## Options

You can make loose mods scrappable (more on that later), and ignore selected recipes. When a recipe is ignored, no calculations will be made, and unless the resulting loose mod (if any) does not respect basic physics (read sanity pass below) it will be left untouched.

## First Sanity Pass

As a first sanity pass, ModReEvaluator checks minimum value and weight on every misc item with components. If the value or weight of the misc item is **LESS THAN** the value or weight of all the produced components, it will be set to that minimum. This prevents **GAINING** weight or value when scrapping an item. Selling a Desk Fan should be at least as profitable as selling the components it produces when scrapping. Component weight and value should be either equal (for lossless scrapping) or less (for lossy scrapping). It makes logical sense too: a toy soldier that produces 1 wood when scrapped might be more valuable when sold directly (because of the work that was put into it) or weigh more (because it contains oil, which cannot be retrieved) but it can never weigh less or have less value than the 1 wood it produces when scrapped.

Additionally, every "component" that is not a real CMPO on misc items will just be nuked from existence.

This first pass ensure the following logic works correctly.

## Base Logic

ModReEvaluator makes all values and weights of all weapon and armor modifications and their resulting loose mod consistent.

Example: if a recipe for a scope is 2 steel and 1 glass, the value and weight that the Object Modification adds to the weapon will be that of 2 steel and 1 glass. The value and weight of the resulting loose mod will also be set to that of 2 steel and 1 glass. This way the value and weight is kept consistent between components used / mod is attached / mod is unattached. When attaching or unattaching a mod no value or weight should be lost or gained. Selling a weapon with an attached mod or selling the weapon and the mod separately should yield the same amount of caps.

Existing value, weight and component list on loose mods will be completely discarded.

## Scrappable Loose Mods

Optionally ModReEvaluator can make loose mods scrappable by following the component ScrapScalar values. The component count of the crafting recipe is multiplied by the values below, then Floored.

* ModScrapScalar_Full = 1
* ModScrapScalar_SuperCommon = 0.75
* ModScrapScalar_Uncommon = 0.5
* ModScrapScalar_Rare = 0.25
* ModScrapScalar_None = 0

In the example above of 2 steel (supercommon) and 1 glass (uncommon), the loose mod will yield just one steel.

When a mod is made scrappable only the components are set to their scalar values, the full weight and value will always be that of the full crafting components.

Keep in mind that by making loose mods scrappable you tell the game that is safe to automatically use up those for resources when crafting anything. If that's not what you want please keep this option disabled or store loose mods in a separate container than your inventory or the workbench.

## Loose Nuker

When the weapon/armor mod is crafted with only ModScrapScalar_None components (such as c_oil or c_adhesive), and a loose mod is present, the loose mod will be unlinked, making it non-attachable. However, the value and weight that the Object Modification adds to the weapon will be that of the full crafting components. This is to prevent the generation of loose mods that make no sense, such as detachable paints.

When the crafting recipe is free (no components) and a loose mod is present, the loose mod will be unlinked, as it would have a value and weight of zero, making it useless.

When a loose mod is unlinked, the value and weight will be set to zero, so if you have some in your inventory already they are now useless. This ensure mods that you crafted for free or with non-retrievable components have no value or weight, even retroactively.

Many mod-authors make cosmetic recipes free, but add a small value to the created misc item. This would allow anyone to create infinite caps by detaching the misc mod created with a free recipe, putting it in a container, and recrafting it indefinitely.

Unfortunately, when the recipe is not free and the loose mod is not present (due to lazy mod-making), this patcher will not generate one. A warning will be issued and you can make a patch with xEdit or whatever that adds a dummy loose mod. No need to calculate any value, just re-run ModReEvaluator after.

## Component Police

ModReEvaluator also removes all weapon and armor mod recipe components that are not either true components or base, unscrappable misc items. Ingestibles, Ammo and other wacky "components" will simply be removed from the recipe. Unscrappable misc items will be kept in the recipe, their value and weight will be added to the loose mod (but not to the component list if you use the scrappable mods option).

If the Constructible object contains a scrappable misc item (a misc item that has components) the recipe will simply be converted to that of the base components. For example, if a weapon mod requires 2 Duct Tapes, MiscReEvaluator converts those 2 Duct Tapes to 2 adhesives and 2 cloths (1 adhesive and 1 cloth for each Duct Tape).

While it might seem cute and "immersive" to add scrappable misc items as crafting components it is inherently wrong and incompatible with how the base game treats scrappable items. Scrappable items are automatically scrapped when crafting, and to keep those around for crafting purposes one would need remove those items from the inventory, and put them in a container that is not the workbench, only to retrieve them when needed, which makes the whole process completely stupid.
