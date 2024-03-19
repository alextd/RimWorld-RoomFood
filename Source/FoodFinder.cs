using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Room_Food
{
	[HarmonyPatch(typeof(FoodUtility), nameof(FoodUtility.SpawnedFoodSearchInnerScan))]
	[HarmonyPriority(Priority.Last)] // Harmony Priority last means prefix goes first
	public static class FoodFinder
	{
		//private static Thing SpawnedFoodSearchInnerScan(Pawn eater, IntVec3 root, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
		public static void Postfix(ref Thing __result, Pawn eater, IntVec3 root, List<Thing> searchSet, PathEndMode peMode, TraverseParms traverseParams, float maxDistance = 9999f, Predicate<Thing> validator = null)
		{
			Log.Message($"Vanilla SpawnedFoodSearchInnerScan from <{root}> for {eater} was ({__result})"); ;
			if (FindRoomFood(__result, eater, root, peMode, traverseParams, maxDistance, validator) is Thing roomFood)
			{
				Log.Message($"SpawnedFoodSearchInnerScan for {eater} found Room Food ({roomFood})"); ;
				__result = roomFood;
			}
		}

		public static Thing FindRoomFood(Thing vanillaFoundFood, Pawn eater, IntVec3 root, PathEndMode peMode, TraverseParms traverseParams, float maxDistance, Predicate<Thing> foodValidator)
		{
			Pawn getter = traverseParams.pawn ?? eater;

			Log.Message($"FindRoomFood for {getter}:{eater}");

			if (!getter.IsFreeColonist || !eater.RaceProps.Humanlike)
				return null;


			//TODO Use districts : traversable area in a room?
			Room room = null;

			if (getter == eater)
			{
				float maxDistanceToTable = Mod.settings.maxDistanceToTable;

				// If the food vanilla found get is farther than the max distance to check for a table,
				// There could be a table on the way to that food.
				// We might as well look for tables in a larger range, up to that food.
				if (vanillaFoundFood != null)
				{
					float distToFoundFood = (root - vanillaFoundFood.Position).LengthHorizontal;
					if (maxDistanceToTable < distToFoundFood)
					{
						Log.Message($"Increasing distance {maxDistanceToTable} to {distToFoundFood} to match {vanillaFoundFood}");
	
						maxDistanceToTable = distToFoundFood;
					}
				}


				Predicate<Thing> tableValidator =
					(Thing t) => t is Building b
					&& b.def.surfaceType == SurfaceType.Eat
					&& b.Position.GetDangerFor(getter, t.Map) == Danger.None
					&& !b.GetRoom().IsHuge
					&& !b.GetRoom().IsPrisonCell // Free colonist can't be in a prison cell
					&& b.GetRoom().Regions.Any(r => !r.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree).NullOrEmpty());

				//Log.Message($"Buildings are: {getter.Map.listerBuildings.allBuildingsColonist.ToStringSafeEnumerable()}");

				Thing table = GenClosest.ClosestThingReachable(getter.Position, getter.Map,
					ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial),
					PathEndMode.OnCell, TraverseParms.For(getter), maxDistanceToTable, tableValidator);

				Log.Message($"Table is {table}");
				if (table != null)
					room = table.GetRoom();
			}
			else
				room = eater.GetRoom();//getter Serving food to eater

			Log.Message($"Room is {room}");

			if (room == null || room.IsHuge)
				return null;

			Log.Message($"{getter} finding food for {eater} in {room}");
			
			Predicate<Thing> searchValidator = delegate (Thing t)
			{
				return
					getter.Map.reachability.CanReach(root, t, peMode, traverseParams)
					//	&& t.Spawned	// We just got a list from listerThings, they're spawned.
					&& foodValidator(t);
			};

			var foods = room.Regions.SelectMany(region => region.ListerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree));

			Thing foundFood = GenClosest.ClosestThing_Global(eater.Position, foods, maxDistance, searchValidator,
				f => FoodUtility.FoodOptimality(eater, f, FoodUtility.GetFinalIngestibleDef(f), (root - f.Position).LengthManhattan));

			Log.Message($"Closest food is {foundFood}");

			return foundFood;
		}
	}
}
