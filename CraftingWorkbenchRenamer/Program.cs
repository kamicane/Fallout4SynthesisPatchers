using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Strings;

namespace CraftingWorkbenchRenamer {
	public class Program {
		public static async Task<int> Main (string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetTypicalOpen(GameRelease.Fallout4, "SYN_CraftingWorkbenchRenamer.esp")
				.Run(args);
		}

		public static void RunPatch (IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			var replacedName = "Crafting Workbench";

			var furnItems = state.LoadOrder.PriorityOrder.Furniture().WinningOverrides();

			foreach (var furnItem in furnItems) {
				if (furnItem.Name == null || furnItem.BenchType != Furniture.BenchTypes.Alchemy) continue;
				if (furnItem.Name.TryLookup(Language.English, out var englishName)) {
					if (englishName.Contains("Chem Station") || englishName.Contains("Chemistry Station")) {
						var newName = englishName.Replace("Chem Station", replacedName).Replace("Chemistry Station", replacedName);
						var fixedStation = state.PatchMod.Furniture.GetOrAddAsOverride(furnItem);
						fixedStation.Name = newName;
						Console.WriteLine($"{englishName} => {newName}");
					}
				}
			}
		}
	}
}
