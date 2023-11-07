using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Fallout4;
using Mutagen.Bethesda.FormKeys.Fallout4;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.WPF.Reflection.Attributes;
using Noggog;

namespace ModReEvaluator {
	public class Program {
		static Lazy<ProgramSettings> Settings = null!;
		public static async Task<int> Main(string[] args) {
			return await SynthesisPipeline.Instance
				.AddPatch<IFallout4Mod, IFallout4ModGetter>(RunPatch)
				.SetAutogeneratedSettings(
					nickname: "Settings",
					path: "ModReEvaluator_Settings.json",
					out Settings)
				.SetTypicalOpen(GameRelease.Fallout4, "YourPatcher.esp")
				.Run(args);
		}

		public static void LogError (string message) {
			Console.WriteLine($"ERROR: {message}");
		}

		public static void LogWarning (string message) {
			if (Settings.Value.LogLevel <= (ProgramSettings.LogLevelEnum)1) Console.WriteLine($"WARNING: {message}");
		}

		public static void LogInfo (string message) {
			if (Settings.Value.LogLevel == 0) Console.WriteLine($"INFO: {message}");
		}

		public static (uint, float) RegisterComponent (IPatcherState<IFallout4Mod, IFallout4ModGetter> state, IComponentGetter componentItem, uint componentCount, List<MiscItemComponent> miscComponentsList, List<ConstructibleObjectComponent> cobjComponentsList) {
			var cmpoScrapItem = componentItem.ScrapItem?.TryResolve<IMiscItemGetter>(state.LinkCache);
			if (cmpoScrapItem == null) {
				LogWarning($"Component has no ScrapItem: {componentItem.FormKey.ModKey}:{componentItem.EditorID}");
				return (0, 0);
			}

			var cmpoScalar = componentItem.ModScrapScalar?.TryResolve<IGlobalGetter>(state.LinkCache);

			uint scaledComponentCount = componentCount;

			if (cmpoScalar != null) {
				if (cmpoScalar.Equals(Fallout4.Global.ModScrapScalar_None)) {
					scaledComponentCount = 0;
				} else if (cmpoScalar.Equals(Fallout4.Global.ModScrapScalar_SuperCommon)) {
					scaledComponentCount = (uint)Math.Floor(0.75 * scaledComponentCount);
				} else if (cmpoScalar.Equals(Fallout4.Global.ModScrapScalar_Uncommon)) {
					scaledComponentCount = (uint)Math.Floor(0.5 * scaledComponentCount);
				} else if (cmpoScalar.Equals(Fallout4.Global.ModScrapScalar_Rare)) {
					scaledComponentCount = (uint)Math.Floor(0.25 * scaledComponentCount);
				}
			}

			uint cmpoValue = (uint)cmpoScrapItem.Value;
			float cmpoWeight = cmpoScrapItem.Weight;

			if (scaledComponentCount > 0) {
				MiscItemComponent miscItemComponentEntry = new()
				{
					Component = componentItem.ToLink(),
					Count = scaledComponentCount
				};
				miscComponentsList.Add(miscItemComponentEntry);
			}

			ConstructibleObjectComponent cobjComponentEntry = new()
			{
				Component = componentItem.ToLink(),
				Count = componentCount
			};
			cobjComponentsList.Add(cobjComponentEntry);

			return (cmpoValue * componentCount, cmpoWeight * componentCount);
		}

		public static bool IsScrapScalarNone (IPatcherState<IFallout4Mod, IFallout4ModGetter> state, IComponentGetter component) {
			var scalarValue = component.ModScrapScalar?.TryResolve<IGlobalGetter>(state.LinkCache);
			if (scalarValue != null && scalarValue.Equals(Fallout4.Global.ModScrapScalar_None)) {
				return true;
			}
			return false;
		}

		public static void ProcessCobj(IPatcherState<IFallout4Mod, IFallout4ModGetter> state, IConstructibleObjectGetter cobjItem) {
				var omodItem = cobjItem.CreatedObject.TryResolve<IAObjectModificationGetter>(state.LinkCache);
				// nothing to do
				if (omodItem == null || (omodItem is not IWeaponModificationGetter && omodItem is not IArmorModificationGetter)) return;

				uint totalValue = 0;
				float totalWeight = 0;

				bool cobjNeedsFix = false;

				ExtendedList<ConstructibleObjectComponent> cobjComponentsList = new();
				ExtendedList<MiscItemComponent> miscComponentsList = new();

				bool shouldRemoveLooseMod = true;

				if (cobjItem.Components?.Count == 0) {
					cobjNeedsFix = true;
				}

				if (cobjItem.Components != null) foreach (var componentEntry in cobjItem.Components) {
					var cKey = componentEntry.Component?.TryResolve(state.LinkCache);
					var cCount = componentEntry.Count;

					if (cKey == null) {
						LogWarning($"Empty Component on {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}");
						cobjNeedsFix = true;
						continue;
					}

					if (cCount <= 0) {
						LogWarning($"Zero count on {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}");
						cobjNeedsFix = true;
						continue;
					}

					if (cKey is IComponentGetter cKeyComponent) {
						if (shouldRemoveLooseMod && !IsScrapScalarNone(state, cKeyComponent)) shouldRemoveLooseMod = false;

						var valueAndWeight = RegisterComponent(state, cKeyComponent, cCount, miscComponentsList, cobjComponentsList);
						totalValue += valueAndWeight.Item1;
						totalWeight += valueAndWeight.Item2;

					} else if (cKey is IMiscItemGetter cKeyMisc) {
						// allow only misc items without components.
						// only value and weight will be added.
						// loose mod will be forced on. cannot be added to scraplist.
						cobjNeedsFix = true;

						if (cKeyMisc.Components == null || cKeyMisc.Components.Count == 0) {
							if (shouldRemoveLooseMod) shouldRemoveLooseMod = false;

							LogInfo($"COBJ is using MiscItem as Component. COBJ: {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}, MISC: {cKey.FormKey.ModKey}:{cKey.EditorID}");

							totalValue += (uint)cKeyMisc.Value;
							totalWeight += cKeyMisc.Weight;

							ConstructibleObjectComponent cobjComponentEntry = new()
							{
								Component = cKeyMisc.ToLink(),
								Count = cCount
							};
							cobjComponentsList.Add(cobjComponentEntry);

							continue;
						}

						foreach (var miscComponentEntry in cKeyMisc.Components) {
							var miscComponentEntryComponent = miscComponentEntry.Component?.TryResolve<IComponentGetter>(state.LinkCache);
							if (miscComponentEntryComponent == null) {
								LogError($"{cKey.FormKey.ModKey}:{cKey.EditorID}");
								continue;
							}

							if (shouldRemoveLooseMod && !IsScrapScalarNone(state, miscComponentEntryComponent)) shouldRemoveLooseMod = false;

							var valueAndWeight = RegisterComponent(state, miscComponentEntryComponent, miscComponentEntry.Count * cCount, miscComponentsList, cobjComponentsList);
							totalValue += valueAndWeight.Item1;
							totalWeight += valueAndWeight.Item2;
						}

						LogWarning($"Converted scrappable MISC to base components. COBJ: {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}, MISC: {cKey.FormKey.ModKey}:{cKey.EditorID}");
					} else {
						cobjNeedsFix = true;
						LogWarning($"Removing invalid Component. COBJ: {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}, Form: {cKey.FormKey.ModKey}:{cKey.EditorID}");
					}
				}

				if (cobjNeedsFix) {
					var fixedCobj = state.PatchMod.ConstructibleObjects.GetOrAddAsOverride(cobjItem);
					if (cobjComponentsList.Count > 0) fixedCobj.Components = cobjComponentsList;
					else fixedCobj.Components?.Clear();
				}

				var objectMod = state.PatchMod.ObjectModifications.GetOrAddAsOverride(omodItem);

				var LooseMiscItem = omodItem.LooseMod.TryResolve<IMiscItemGetter>(state.LinkCache);

				if (LooseMiscItem != null) {
					if (!shouldRemoveLooseMod) {
						var fixedLooseMod = state.PatchMod.MiscItems.GetOrAddAsOverride(LooseMiscItem);
						if (Settings.Value.MakeLooseModsScrappable && miscComponentsList.Count > 0) fixedLooseMod.Components = miscComponentsList;
						else fixedLooseMod.Components?.Clear();

						fixedLooseMod.Value = (int)totalValue;
						fixedLooseMod.Weight = totalWeight;
					} else {
						LogInfo($"Removing loose mod. COBJ: {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}, OMOD: {omodItem.FormKey.ModKey}:{omodItem.EditorID} {omodItem.Name}");
						objectMod.LooseMod.Clear();

						if (LooseMiscItem.Value != 0 || LooseMiscItem.Weight != 0 || LooseMiscItem.Components != null) {
							var fixedLooseMod = state.PatchMod.MiscItems.GetOrAddAsOverride(LooseMiscItem);
							fixedLooseMod.Value = 0;
							fixedLooseMod.Weight = 0;
							fixedLooseMod.Components?.Clear();
							LogInfo($"Zeroed value, weight and components. MISC: {LooseMiscItem.FormKey.ModKey}:{LooseMiscItem.EditorID}");
						}
					}
				} else if (miscComponentsList.Count > 0) {
					LogWarning($"No loose mod on COBJ with components. COBJ: {cobjItem.FormKey.ModKey}:{cobjItem.EditorID}, OMOD: {omodItem.FormKey.ModKey}:{omodItem.EditorID}");
				}

				if (objectMod is WeaponModification weaponMod) {
					for (int i = weaponMod.Properties.Count - 1; i >= 0; i--) {
						var prop = weaponMod.Properties[i];

						if (prop.Property == Weapon.Property.Value || prop.Property == Weapon.Property.Weight) {
							weaponMod.Properties.RemoveAt(i);
						}
					}

					if (totalValue > 0) {
						ObjectModFloatProperty<Weapon.Property> newFloatProp = new()
						{
							FunctionType = ObjectModProperty.FloatFunctionType.Add,
							Property = Weapon.Property.Value,
							Value = totalValue
						};
						weaponMod.Properties.Add(newFloatProp);
					}

					if (totalWeight > 0) {
						ObjectModFloatProperty<Weapon.Property> newFloatProp = new()
						{
							FunctionType = ObjectModProperty.FloatFunctionType.Add,
							Property = Weapon.Property.Weight,
							Value = totalWeight
						};
						weaponMod.Properties.Add(newFloatProp);
					}
				} else if (objectMod is ArmorModification armorMod) { //eww
					for (int i = armorMod.Properties.Count - 1; i >= 0; i--) {
						var prop = armorMod.Properties[i];

						if (prop.Property == Armor.Property.Value || prop.Property == Armor.Property.Weight) {
							armorMod.Properties.RemoveAt(i);
						}
					}

					if (totalValue > 0) {
						ObjectModFloatProperty<Armor.Property> newFloatProp = new()
						{
							FunctionType = ObjectModProperty.FloatFunctionType.Add,
							Property = Armor.Property.Value,
							Value = totalValue
						};
						armorMod.Properties.Add(newFloatProp);
					}

					if (totalWeight > 0) {
						ObjectModFloatProperty<Armor.Property> newFloatProp = new()
						{
							FunctionType = ObjectModProperty.FloatFunctionType.Add,
							Property = Armor.Property.Weight,
							Value = totalWeight
						};
						armorMod.Properties.Add(newFloatProp);
					}
				}
		}

		public static void RunPatch(IPatcherState<IFallout4Mod, IFallout4ModGetter> state) {
			// Add NotJunkJetAmmo keyword to component scrap if missing

			var componentItems = state.LoadOrder.PriorityOrder.Component().WinningOverrides();

			foreach (var componentItem in componentItems) {
				var miscItem = componentItem.ScrapItem?.TryResolve<IMiscItemGetter>(state.LinkCache);
				if (miscItem == null) continue;

				if (!miscItem.HasKeyword(Fallout4.Keyword.NotJunkJetAmmo)) {
					var miscItemOverride = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					miscItemOverride.Keywords ??= new();
					miscItemOverride.Keywords.Add(Fallout4.Keyword.NotJunkJetAmmo);
				}
			}

			// Sanity Pass
			// minimum weight and minimum value checks

			var miscItems = state.LoadOrder.PriorityOrder.MiscItem().WinningOverrides();

			foreach (var miscItem in miscItems) {
				if (miscItem.Components == null || miscItem.Components.Count == 0) continue;

				ExtendedList<MiscItemComponent> miscComponentsList = new();
				bool miscNeedsFix = false;

				int minValue = 0;
				float minWeight = 0;

				foreach (var componentEntry in miscItem.Components) {
					var component = componentEntry.Component?.TryResolve<IComponentGetter>(state.LinkCache);
					if (component == null) {
						LogWarning($"Invalid Component on Misc: {miscItem.FormKey.ModKey}:{miscItem.EditorID} ({miscItem.Name})");
						miscNeedsFix = true;
						continue;
					}
					var scrapItem = component.ScrapItem?.TryResolve<IMiscItemGetter>(state.LinkCache);

					if (scrapItem == null) {
						LogWarning($"Component with no ScrapItem on Misc: {miscItem.FormKey.ModKey}:{miscItem.EditorID} ({miscItem.Name})");
						miscNeedsFix = true;
						continue;
					}

					var cCount = componentEntry.Count;

					MiscItemComponent miscItemComponentEntry = new()
					{
						Component = component.ToLink(),
						Count = cCount
					};
					miscComponentsList.Add(miscItemComponentEntry);

					minValue += (int)(scrapItem.Value * cCount);
					minWeight += scrapItem.Weight * cCount;
				}

				int miscValue = miscItem.Value;
				float miscWeight = miscItem.Weight;

				if (miscItem.Value < minValue) {
					Console.ForegroundColor = ConsoleColor.Yellow;
					LogInfo($"Recalculated value on MISC: {miscItem.FormKey.ModKey}:{miscItem.EditorID} ({miscItem.Name})");
					miscNeedsFix = true;
					miscValue = minValue;
				}

				bool isShipment = false;
				if (miscItem.EditorID != null && miscItem.EditorID.StartsWith("shipment_")) isShipment = true;

				if (isShipment && miscItem.Weight != 0) {
					miscNeedsFix = true;
					miscWeight = 0;
				}

				if (!isShipment && miscItem.Weight < minWeight) {
					LogInfo($"Recalculated weight on Misc: {miscItem.FormKey.ModKey}:{miscItem.EditorID} ({miscItem.Name})");
					miscNeedsFix = true;
					miscWeight = minWeight;
				}

				if (miscNeedsFix) {
					var fixedMisc = state.PatchMod.MiscItems.GetOrAddAsOverride(miscItem);
					fixedMisc.Components = miscComponentsList;
					fixedMisc.Value = miscValue;
					fixedMisc.Weight = miscWeight;
				}
			}

			// Process COBJs

			var cobjItems = state.LoadOrder.PriorityOrder.ConstructibleObject().WinningOverrides();
			foreach (var cobjItem in cobjItems) {
				if (Settings.Value.COBJExcludeList.Contains(cobjItem)) continue;
				ProcessCobj(state, cobjItem);
			}
		}
	}

	public class ProgramSettings {
		[MaintainOrder]
		[SettingName("Make Loose Mods Scrappable (Warning: read docs)")]
		public bool MakeLooseModsScrappable = false;
		// [MaintainOrder]
		// [SettingName("Remove All Loose Mods (Starfield Style)")]
		// public bool RemoveAllLooseMods = false;

		// [MaintainOrder]
		// [SettingName("Scrap Scalar Values")]
		// public Dictionary<string, float> ScrapScalarValues = new()
		// {
		//	{ "ModScrapScalar_Full", 1.0f }
		//	{ "ModScrapScalar_SuperCommon", 0.75f },
		//	{ "ModScrapScalar_Uncommon", 0.5f },
		//	{ "ModScrapScalar_Rare", 0.25f }
		//	{ "ModScrapScalar_None", 0.0f },
		// };

		[MaintainOrder]
		[SettingName("Log Level")]
		public LogLevelEnum LogLevel = LogLevelEnum.Warning;

		[MaintainOrder]
		[SettingName("Excluded Recipes")]
		public List<IFormLinkGetter<IConstructibleObjectGetter>> COBJExcludeList = new()
		{

		};

		public enum LogLevelEnum
    {
        Info,
        Warning,
        Error
    }
	}
}