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
	//public static Thing BestFoodSourceOnMap(Pawn getter, Pawn eater, bool desperate, out ThingDef foodDef, FoodPreferability maxPref, bool allowPlant, bool allowDrug, bool allowCorpse, bool allowDispenserFull, bool allowDispenserEmpty, bool allowForbidden, bool allowSociallyImproper, bool allowHarvest)
	static class FoodFinder
	{
		public static bool Prefix(ref Thing __result, Pawn getter, Pawn eater, bool desperate, ref ThingDef foodDef, FoodPreferability maxPref, bool allowDrug, bool allowForbidden, bool allowCorpse, bool allowSociallyImproper)
		{
			if(getter.IsFreeColonist && eater.RaceProps.Humanlike)
			{
				Room room = null;

				if (getter == eater)
				{
					float searchRadius = 9999;
					Predicate<Thing> validator =
						(Thing t) => t is Building b
						&& b.def.surfaceType == SurfaceType.Eat
						&& b.Position.GetDangerFor(getter, t.Map) == Danger.None
						&& !b.GetRoom().isPrisonCell // Free colonist can't be in a prison cell
						&& b.GetRoom().Regions.Any(r => !r.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).NullOrEmpty());

					Thing table = GenClosest.ClosestThingReachable(getter.Position, getter.Map,
						ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
						PathEndMode.OnCell, TraverseParms.For(getter), searchRadius, validator);

					if (table != null)
						room = table.GetRoom();
				}
				else room = eater.GetRoom();
				if (room == null || room.IsHuge) return true;

				Log.Message(getter + " finding food for " + eater + " in " + room);


				FoodPreferability minPref = eater.NonHumanlikeOrWildMan() ? minPref = FoodPreferability.NeverForNutrition
					: desperate ? FoodPreferability.DesperateOnly
					: eater.needs.food.CurCategory <= HungerCategory.UrgentlyHungry ? FoodPreferability.RawBad : FoodPreferability.MealAwful;

				//Some of these are pointless but hey.
				Predicate<Thing> foodValidator = t =>
				(allowForbidden || !t.IsForbidden(getter))
				&& t.IngestibleNow && t.def.IsNutritionGivingIngestible
				&& t.def.ingestible.preferability >= minPref
				&& t.def.ingestible.preferability <= maxPref
				&& (allowCorpse || !(t is Corpse))
				&& (allowDrug || !t.def.IsDrug)
				&& (desperate || !t.IsNotFresh())
				&& !t.IsDessicated()
				&& eater.RaceProps.WillAutomaticallyEat(t)
				&& (allowSociallyImproper || (t.IsSociallyProper(getter) && t.IsSociallyProper(eater, eater.IsPrisonerOfColony, !getter.RaceProps.Animal)))
				&& getter.AnimalAwareOf(t)
				&& getter.CanReserve(t);

				List<Thing> foods = new List<Thing>();
				foreach (Region region in room.Regions)
					foods.AddRange(region.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
						.Where(t => foodValidator(t)));

				Thing foundFood = GenClosest.ClosestThing_Global(eater.Position, foods, 99999f, null, f => FoodUtility.FoodOptimality(eater, f, FoodUtility.GetFinalIngestibleDef(f), 0f));
				if(foundFood != null)
				{
					Log.Message("Closest food is " + foundFood);
					__result = foundFood;
					foodDef = FoodUtility.GetFinalIngestibleDef(foundFood);
					return false;
				}
			}
			return true;
		}
	}
}
