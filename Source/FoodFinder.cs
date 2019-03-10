using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Harmony;
using RimWorld;
using Verse;
using Verse.AI;

namespace Room_Food
{
	[HarmonyPatch(typeof(FoodUtility), "BestFoodSourceOnMap")]
	[HarmonyPriority(Priority.Last)] // Harmony Priority last means prefix goes first
	//public static Thing BestFoodSourceOnMap(Pawn getter, Pawn eater, bool desperate, out ThingDef foodDef, FoodPreferability maxPref, bool allowPlant, bool allowDrug, bool allowCorpse, bool allowDispenserFull, bool allowDispenserEmpty, bool allowForbidden, bool allowSociallyImproper, bool allowHarvest)
	static class FoodFinder
	{
		public static bool Prefix(ref Thing __result, Pawn getter, Pawn eater, ref ThingDef foodDef, FoodPreferability maxPref, bool allowDrug, bool allowDispenserFull, bool allowDispenserEmpty, bool allowForbidden)
		{
			if (FindRoomFood(getter, eater, maxPref, allowDrug, allowDispenserFull, allowDispenserEmpty, allowForbidden) is Thing food)
			{
				foodDef = FoodUtility.GetFinalIngestibleDef(food);
				__result = food;
				return false;
			}
			return true;
		}

		public static Thing FindRoomFood(Pawn getter, Pawn eater, FoodPreferability maxPref, bool allowDrug, bool allowDispenserFull, bool allowDispenserEmpty, bool allowForbidden)
		{
			if (!getter.IsFreeColonist || !eater.RaceProps.Humanlike)
				return null;

			Room room = null;

			if (getter == eater)
			{
				float searchRadius = 9999;
				Predicate<Thing> tableValidator =
					(Thing t) => t is Building b
					&& b.def.surfaceType == SurfaceType.Eat
					&& b.Position.GetDangerFor(getter, t.Map) == Danger.None
					&& !b.GetRoom().IsHuge
					&& !b.GetRoom().isPrisonCell // Free colonist can't be in a prison cell
					&& b.GetRoom().Regions.Any(r => !r.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).NullOrEmpty());

				Log.Message($"Buildings are: {getter.Map.listerBuildings.allBuildingsColonist.ToStringSafeEnumerable()}");
				Thing table = GenClosest.ClosestThingReachable(getter.Position, getter.Map,
					ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
					PathEndMode.OnCell, TraverseParms.For(getter), searchRadius, tableValidator);

				if (table != null)
					room = table.GetRoom();
			}
			else room = eater.GetRoom();

			if (room == null || room.IsHuge)
				return null;

			Log.Message($"{getter} finding food for {eater} in {room}");
			
			FoodPreferability minPref = eater.NonHumanlikeOrWildMan() ? FoodPreferability.NeverForNutrition
				: eater.needs.food.CurCategory >= HungerCategory.UrgentlyHungry ? FoodPreferability.RawBad : FoodPreferability.MealAwful;
			Log.Message($"{eater} is {eater.needs.food.CurCategory}, prefers{minPref}");

			//Some of these are pointless but hey.
			bool getterCanManipulate = getter.RaceProps.ToolUser && getter.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);
			Predicate<Thing> foodValidator = delegate (Thing t)
			{
				if (t is Building_NutrientPasteDispenser n)
					return allowDispenserFull
						&& n.DispensableDef.ingestible.preferability >= minPref
						&& n.DispensableDef.ingestible.preferability <= maxPref
						&& getterCanManipulate
						&& !getter.IsWildMan()
						&& t.Faction == getter.Faction
						&& n.powerComp.PowerOn
						&& (allowDispenserEmpty || n.HasEnoughFeedstockInHoppers())
						&& t.InteractionCell.Standable(t.Map)
						&& getter.Map.reachability.CanReachNonLocal(getter.Position, new TargetInfo(t.InteractionCell, t.Map), PathEndMode.OnCell, TraverseParms.For(getter, Danger.Some));

				return (allowForbidden || !t.IsForbidden(getter))
				&& t.IngestibleNow && t.def.IsNutritionGivingIngestible
				&& t.def.ingestible.preferability >= minPref
				&& t.def.ingestible.preferability <= maxPref
				&& !(t is Corpse)
				&& (allowDrug || !t.def.IsDrug)
				&& !t.IsNotFresh()
				&& !t.IsDessicated()
				&& eater.WillEat(t, getter)
				&& getter.AnimalAwareOf(t)
				&& getter.CanReserve(t);
			};

			List<Thing> foods = new List<Thing>();
			foreach (Region region in room.Regions)
				foods.AddRange(region.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
					.Where(t => foodValidator(t)));

			Thing foundFood = GenClosest.ClosestThing_Global(eater.Position, foods, 99999f, null, f => FoodUtility.FoodOptimality(eater, f, FoodUtility.GetFinalIngestibleDef(f), 0f));

			Log.Message($"Closest food is {foundFood}");
			return foundFood;
		}
	}
}
