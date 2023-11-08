using Mutagen.Bethesda;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.Synthesis;

namespace CleanESL {
	public class Program {
		public static async Task<int> Main (string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetTypicalOpen(GameRelease.Fallout4, "NULL.esp")
				.Run(args);
		}

		public static void RunPatch (IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			state.PatchMod.Clear();
			state.PatchMod.ModHeader.Flags |= Fallout4ModHeader.HeaderFlag.LightMaster;
		}
	}
}
