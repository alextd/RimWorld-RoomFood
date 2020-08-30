using System.Reflection;
using System.Linq;
using Verse;
using UnityEngine;
using HarmonyLib;
using RimWorld;

namespace Room_Food
{
	public class Mod : Verse.Mod
	{
		public Mod(ModContentPack content) : base(content)
		{
			// initialize settings
			// GetSettings<Settings>();
#if DEBUG
			Harmony.DEBUG = true;
#endif
			Harmony harmony = new Harmony("Uuugggg.rimworld.Room_Food.main");
			
			harmony.PatchAll();
		}

		// public override void DoSettingsWindowContents(Rect inRect)
		// {
			// base.DoSettingsWindowContents(inRect);
			// GetSettings<Settings>().DoWindowContents(inRect);
		// }

		// public override string SettingsCategory()
		// {
			// return "TD.RoomFood".Translate();
		// }
	}
}