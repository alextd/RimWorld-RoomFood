using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using RimWorld;

namespace Room_Food
{
	public class Settings : ModSettings
	{
		public int maxDistanceToTable = 100;

		public void DoWindowContents(Rect wrect)
		{
			var options = new Listing_Standard();
			options.Begin(wrect);

			maxDistanceToTable = (int)options.SliderLabeled($"Distance to look for a table: {maxDistanceToTable}", maxDistanceToTable, 1, 200);
			options.Gap();

			options.End();
		}
		
		public override void ExposeData()
		{
			Scribe_Values.Look(ref maxDistanceToTable, "maxDistanceToTable", 100);
		}
	}
}