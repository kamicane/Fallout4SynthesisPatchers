using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Fallout4;
using YamlDotNet.Serialization;

namespace BulletDamage {
	public class Program {
		public static async Task<int> Main(string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetTypicalOpen(GameRelease.Fallout4, "YourPatcher.esp")
				.Run(args);
		}

		public static void RunPatch(IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			Type type = typeof(Program);
			string? namespaceString = type.Namespace;

			string configText = File.ReadAllText(state.RetrieveConfigFile($"{namespaceString}.yml"));

			var yamlDeserializer = new DeserializerBuilder().Build();
			Dictionary<string, int>? items = yamlDeserializer.Deserialize<Dictionary<string, int>>(configText);

			if (items == null) {
				throw new Exception($"ERROR: Cannot read {namespaceString}.yml");
			}

			List<Mutagen.Bethesda.Plugins.FormKey> ammoItems = new();

			foreach (var item in items) {
				var editorID = item.Key;
				var ammoDamage = item.Value;
				// Console.WriteLine($"{key}: {value}");

				IAmmunitionGetter? ammoItem;

				try {
					ammoItem = state.LinkCache.Resolve<IAmmunitionGetter>(editorID);
				} catch (Exception) {
					Console.WriteLine($"Misc item not found: {editorID}");
					continue;
				}

				ammoItems.Add(ammoItem.FormKey);

				var oAmmoItem = state.PatchMod.Ammunitions.GetOrAddAsOverride(ammoItem);
				oAmmoItem.Damage = ammoDamage;
			}

			var weapItems = state.LoadOrder.PriorityOrder.Weapon().WinningOverrides();

			foreach (var weapItem in weapItems) {
				var weapAmmo = weapItem.Ammo.TryResolve(state.LinkCache);
				if (weapAmmo == null) continue;
				if (ammoItems.Contains(weapAmmo.FormKey)) {
					ushort oldBaseDamage = weapItem.BaseDamage;
					ushort newBaseDamage = (ushort)Math.Floor(weapItem.BaseDamage * 0.1);
					if (newBaseDamage != weapItem.BaseDamage) {
						var oWeapItem = state.PatchMod.Weapons.GetOrAddAsOverride(weapItem);
						oWeapItem.BaseDamage = newBaseDamage;
						Console.WriteLine($"{oWeapItem.EditorID}: {oldBaseDamage} -> {(int)(newBaseDamage + weapAmmo.Damage)}");
					}
				}
			}
		}
	}
}
