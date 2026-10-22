using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ABXenotypeYautja
{
    /// <summary>
    /// HediffComp that provides a player-accessible Gizmo button to self-remove an installed gauntlet or module.
    /// When clicked, it removes the Hediff from the pawn and spawns the original ThingDef on the ground at the pawn's feet.
    /// Also detaches and drops any installed modules if a base gauntlet is removed.
    /// </summary>
    public class HediffComp_RemoveGauntlet : HediffComp
    {
        public HediffCompProperties_RemoveGauntlet Props =>
            (HediffCompProperties_RemoveGauntlet)props;

        /// <summary>
        /// Generates the removal Gizmo button when a player-controlled colonist is selected.
        /// </summary>
        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || Pawn.Dead || !Pawn.IsColonistPlayerControlled)
                yield break;

            string label = Props.removeLabel;
            if (parent.Part != null)
            {
                label = $"{Props.removeLabel} ({parent.Part.Label})";
            }

            ThingDef dropDef = Props.thingDefToSpawn ?? parent.def.spawnThingOnRemoved;

            Command_Action removeAction = new Command_Action
            {
                defaultLabel = label,
                defaultDesc = Props.removeDesc,
                action = HandleRemovalClick
            };

            // Resolve icon: custom path -> spawned item icon -> fallback
            if (!string.IsNullOrEmpty(Props.uiIconPath))
            {
                removeAction.icon = ContentFinder<Texture2D>.Get(Props.uiIconPath, false);
            }
            else if (dropDef?.uiIcon != null)
            {
                removeAction.icon = dropDef.uiIcon;
                removeAction.defaultIconColor = dropDef.uiIconColor;
            }

            yield return removeAction;
        }

        private void HandleRemovalClick()
        {
            if (Props.requireConfirmation)
            {
                string confirmText = "ABX_ConfirmRemoveGauntlet".Translate(Pawn.LabelShortCap, parent.LabelCap);
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    confirmText,
                    RemoveGauntlet,
                    destructive: true
                ));
            }
            else
            {
                RemoveGauntlet();
            }
        }

        /// <summary>
        /// Executes the removal of the gauntlet, drops the item into the world,
        /// and cleanly detaches any secondary modules installed on the same limb.
        /// </summary>
        public void RemoveGauntlet()
        {
            Pawn pawn = Pawn;
            if (pawn == null)
                return;

            ThingDef thingToSpawn = Props.thingDefToSpawn ?? parent.def.spawnThingOnRemoved;

            // 1. Spawn the main gauntlet item
            if (thingToSpawn != null)
            {
                SpawnItem(pawn, thingToSpawn);
            }

            // 2. If this is a base gauntlet on a specific limb, also eject any attached modules on the same limb
            if (parent.Part != null)
            {
                List<Hediff> attachedModules = pawn.health.hediffSet.hediffs
                    .Where(h => h != parent &&
                                h.Part == parent.Part &&
                                h.def.GetModExtension<DefExtension_GauntletModuleHediff>()?.isModule == true)
                    .ToList();

                foreach (Hediff module in attachedModules)
                {
                    ThingDef moduleDrop = module.def.spawnThingOnRemoved;
                    if (moduleDrop != null)
                    {
                        SpawnItem(pawn, moduleDrop);
                    }
                    pawn.health.RemoveHediff(module);
                }
            }

            // 3. Remove this Hediff
            pawn.health.RemoveHediff(parent);

            // 4. Notify player
            Messages.Message("ABX_RemovedGauntlet".Translate(pawn.LabelShortCap, parent.LabelCap),
                pawn, MessageTypeDefOf.NeutralEvent);
        }

        private void SpawnItem(Pawn pawn, ThingDef def)
        {
            if (pawn == null || def == null)
                return;

            Thing item = ThingMaker.MakeThing(def);
            if (pawn.Map != null && pawn.Position.IsValid)
            {
                GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            }
            else if (pawn.inventory != null)
            {
                pawn.inventory.innerContainer.TryAdd(item);
            }
        }
    }
}
