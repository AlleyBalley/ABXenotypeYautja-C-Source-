using Verse;

namespace ABXenotypeYautja
{
    /// <summary>
    /// Configuration properties for HediffComp_RemoveGauntlet.
    /// Added to gauntlet and module HediffDefs to enable a self-removal Gizmo button.
    /// </summary>
    public class HediffCompProperties_RemoveGauntlet : HediffCompProperties
    {
        /// <summary>
        /// The ThingDef of the item to spawn on the ground when the gauntlet/module is removed.
        /// If omitted or null, falls back automatically to the HediffDef's spawnThingOnRemoved.
        /// </summary>
        public ThingDef thingDefToSpawn;

        /// <summary>
        /// Label displayed on the UI Gizmo button.
        /// </summary>
        public string removeLabel = "Remove Gauntlet";

        /// <summary>
        /// Detailed description tooltip for the UI Gizmo button.
        /// </summary>
        public string removeDesc = "Remove this gauntlet and drop the item on the ground.";

        /// <summary>
        /// Optional path to a custom icon for the removal button.
        /// If null, falls back to the spawned item's icon or default command textures.
        /// </summary>
        public string uiIconPath;

        /// <summary>
        /// If true, prompts a confirmation dialog before removing the gauntlet.
        /// </summary>
        public bool requireConfirmation = true;

        public HediffCompProperties_RemoveGauntlet()
        {
            compClass = typeof(HediffComp_RemoveGauntlet);
        }
    }
}
