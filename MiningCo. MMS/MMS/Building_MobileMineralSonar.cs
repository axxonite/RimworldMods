﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;   // Always needed
using RimWorld;      // RimWorld specific functions are found here
using Verse;         // RimWorld universal objects are here
//using Verse.AI;    // Needed when you do something with the AI
//using Verse.Sound; // Needed when you do something with the Sound

namespace MobileMineralSonar
{
    /// <summary>
    /// Mobile mineral sonar class.
    /// </summary>
    /// <author>Rikiki</author>
    /// <permission>Use this code as you want, just remember to add a link to the corresponding Ludeon forum mod release thread.
    /// Remember learning is always better than just copy/paste...</permission>
    [StaticConstructorOnStartup]
    class Building_MobileMineralSonar : Building
    {
        // ===================== Variables =====================
        public const int baseMaxScanRange = 3000;
        public const int enhancedMaxScanRange = 5000;
        public static int maxScanRange = baseMaxScanRange;

        public const float baseDetectionChance = 1.0f;
        public const float enhancedDetectionChance = 1.0f;
        public static float detectionChance = baseDetectionChance;

        public int scanRange = 1;
        public int scanProgress = 0;
        private const int scanProgressThresholdPerCell = 1000;
        public float satelliteDishRotation = 0;

        public List<ThingDef> detectedDefList = null;

        public const int flashPeriodInSeconds = 5;
        public int nextFlashTick = 0;

        // Components references.
        public CompPowerTrader powerComp;

        // Textures.
        public static Material scanRange10 = MaterialPool.MatFrom("Effects/ScanRange10");
        public static Material scanRange20 = MaterialPool.MatFrom("Effects/ScanRange20");
        public static Material scanRange30 = MaterialPool.MatFrom("Effects/ScanRange30");
        public static Material scanRange40 = MaterialPool.MatFrom("Effects/ScanRange40");
        public static Material scanRange50 = MaterialPool.MatFrom("Effects/ScanRange50");
        public static Material satelliteDish = MaterialPool.MatFrom("Things/Building/SatelliteDish");
        public static Material scanRayDynamic = MaterialPool.MatFrom("Effects/ScanRay50x50", ShaderDatabase.MetaOverlay);
        public static Material scanSpot = MaterialPool.MatFrom("Effects/ScanSpot", ShaderDatabase.Transparent);
        public Material scanRangeDynamic;
        public Matrix4x4 scanRangeMatrix10 = default(Matrix4x4);
        public Matrix4x4 scanRangeMatrix20 = default(Matrix4x4);
        public Matrix4x4 scanRangeMatrix30 = default(Matrix4x4);
        public Matrix4x4 scanRangeMatrix40 = default(Matrix4x4);
        public Matrix4x4 scanRangeMatrix50 = default(Matrix4x4);
        public Matrix4x4 scanRangeDynamicMatrix = default(Matrix4x4);
        public Matrix4x4 scanRayDynamicMatrix = default(Matrix4x4);
        public Matrix4x4 satelliteDishMatrix = default(Matrix4x4);
        public Matrix4x4 scanSpotMatrix = default(Matrix4x4);
        public Vector3 scanRangeScale10 = new Vector3(20f, 1f, 20f);
        public Vector3 scanRangeScale20 = new Vector3(40f, 1f, 40f);
        public Vector3 scanRangeScale30 = new Vector3(60f, 1f, 60f);
        public Vector3 scanRangeScale40 = new Vector3(80f, 1f, 80f);
        public Vector3 scanRangeScale50 = new Vector3(100f, 1f, 100f);
        public Vector3 scanRangeDynamicScale = new Vector3(1f, 1f, 1f);
        public Vector3 scanRayDynamicScale = new Vector3(1f, 1f, 1f);
        public Vector3 satelliteDishScale = new Vector3(2f, 1f, 2f);
        public Vector3 scanSpotScale = new Vector3(1f, 1f, 1f);

        // ===================== Static functions =====================
        public static void Notify_EnhancedScanResearchCompleted()
        {
            maxScanRange = enhancedMaxScanRange;
            detectionChance = enhancedDetectionChance;
        }

        // ===================== Setup Work =====================
        /// <summary>
        /// Initialize instance variables.
        /// </summary>
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            
            detectedDefList = new List<ThingDef>();
            foreach (ThingDef metallicDef in ((ThingDef_MobileMineralSonar)this.def).scannedThingDefs)
            {
                detectedDefList.Add(metallicDef);
            }

            // Components initialization.
            powerComp = base.GetComp<CompPowerTrader>();
            powerComp.powerOutputInt = 0;

            //scanRangeMatrix10.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanRangeScale10);
            //scanRangeMatrix20.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanRangeScale20);
            scanRangeMatrix30.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanRangeScale30);
            //scanRangeMatrix40.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanRangeScale40);
            scanRangeMatrix50.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanRangeScale50);
            satelliteDishMatrix.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.Building) + Altitudes.AltIncVect, satelliteDishRotation.ToQuat(), satelliteDishScale);
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            base.DeSpawn(mode);
            this.scanRange = 1;
            this.scanProgress = 0;
            this.satelliteDishRotation = 0f;
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look<int>(ref scanRange, "scanRange", 1);
            Scribe_Values.Look<int>(ref scanProgress, "scanProgress", 1);
            Scribe_Values.Look<float>(ref satelliteDishRotation, "satelliteDishRotation", 0f);;
            Scribe_Values.Look<int>(ref nextFlashTick, "nextFlashTick", 0);
        }
        
        // ===================== Main Work Function =====================
        /// <summary>
        /// Main function:
        /// - update the scan progress,
        /// - throw a flash to help locate the MMS in the wild every few seconds.
        /// </summary>
        public override void Tick()
        {
            base.Tick();            
            PerformScanUpdate();

            if (Settings.periodicLightIsEnabled)
            {
                if (Find.TickManager.TicksGame >= this.nextFlashTick)
                {
                    this.nextFlashTick = Find.TickManager.TicksGame + flashPeriodInSeconds * GenTicks.TicksPerRealSecond * (int)Find.TickManager.CurTimeSpeed;
                    ThrowFlash();
                }
            }
        }
                
        /// <summary>
        /// Perform the scan update and update the satellite dish rotation.
        /// </summary>
        public void PerformScanUpdate()
        {
            // Updates the satellite dish rotation.
            if (powerComp.PowerOn)
            {
                satelliteDishRotation = (satelliteDishRotation + 1f) % 360f;
            }
            else
            {
                satelliteDishRotation = (satelliteDishRotation + 0.2f) % 360f;
            }

            if (scanRange == maxScanRange)
            {
                powerComp.powerOutputInt = 0;
                return;
            }

            if ((Find.TickManager.TicksGame % 60) == 0) //GenTicks.TicksPerRealSecond) == 0)
            {
                // Increment the scan progress according to the available power input.
                if (powerComp.PowerOn)
                {
                    scanProgress += 500 * (int)GenTicks.TicksPerRealSecond;
                }
                else
                {
                    scanProgress += 500 * (int)GenTicks.TicksPerRealSecond;
                }
                if (true) //scanProgress >= (this.scanRange * scanProgressThresholdPerCell))
                {
                    foreach (ThingDef detectedDef in detectedDefList)
                    {
                        UnfogSomeRandomThingAtScanRange(detectedDef);
                    }

                    // Reset the scan progress and increase the next scan duration.
                    scanRange++;
                    scanProgress = 0;
                }
            }
        }

        /// <summary>
        /// Unfog some of the things of type thingDefParameter at scanRange.
        /// </summary>
        public void UnfogSomeRandomThingAtScanRange(ThingDef thingDefParameter)
        {
            // Get the mineral blocks at current scan range.
            IEnumerable<Thing> thingsInTheArea = this.Map.listerThings.ThingsOfDef(thingDefParameter);
            if (thingsInTheArea != null)
            {
                IEnumerable<Thing> thingsAtScanRange = thingsInTheArea; //.Where(thing => thing.Position.InHorDistOf(this.Position, scanRange)
                    //&& (thing.Position.InHorDistOf(this.Position, scanRange - 1) == false));
                // Remove the fog on those mineral blocks.
                foreach (Thing thing in thingsAtScanRange)
                {
                    // Chance to unfog a thing.
                    float detectionThreshold = detectionChance + detectionChance * (1 - (float)scanRange / (float)enhancedMaxScanRange);
                    if (Rand.Range(0f, 1f) <= detectionThreshold)
                    {
                        this.Map.fogGrid.Unfog(thing.Position);
                    }
                }
            }
        }

        // ===================== Draw =====================
        public void ThrowFlash()
        {
            if (!this.Position.ShouldSpawnMotesAt(this.Map) || this.Map.moteCounter.SaturatedLowPriority)
            {
                return;
            }
            FleckMaker.Static(this.Position.ToVector3Shifted(), this.Map, FleckDefOf.ExplosionFlash, 5f);
        }

        public override void Draw()
        {
            base.Draw();
            DrawSatelliteDish();

            if (Find.Selector.IsSelected(this) == true)
            {
                DrawMaxScanRange();
                DrawDynamicScanRangeAndScanRay();
                foreach (ThingDef detectedDef in detectedDefList)
                {
                    DrawScanSpotOnThingsWithinScanRange(detectedDef);
                }
            }
        }

        /// <summary>
        /// Draw the satellite dish.
        /// </summary>
        public void DrawSatelliteDish()
        {
            satelliteDishMatrix.SetTRS(base.DrawPos + Altitudes.AltIncVect, satelliteDishRotation.ToQuat(), satelliteDishScale);
            Graphics.DrawMesh(MeshPool.plane10, satelliteDishMatrix, satelliteDish, 0);
        }

        /// <summary>
        /// Draw the max scan range.
        /// </summary>
        public void DrawMaxScanRange()
        {
            if (maxScanRange == baseMaxScanRange)
            {
                Graphics.DrawMesh(MeshPool.plane10, scanRangeMatrix30, scanRange30, 0);
            }
            else
            {
                Graphics.DrawMesh(MeshPool.plane10, scanRangeMatrix50, scanRange50, 0);
            }
        }

        /// <summary>
        /// Draw the dynamic scan range and scan ray.
        /// </summary>
        public void DrawDynamicScanRangeAndScanRay()
        {
            if (scanRange <= 10)
            {
                scanRangeDynamic = scanRange10;
            }
            else if (scanRange <= 20)
            {
                scanRangeDynamic = scanRange20;
            }
            else if (scanRange <= 30)
            {
                scanRangeDynamic = scanRange30;
            }
            else if (scanRange <= 40)
            {
                scanRangeDynamic = scanRange40;
            }
            else
            {
                scanRangeDynamic = scanRange50;
            }
            scanRangeDynamicScale = new Vector3(2f * scanRange, 1f, 2f * scanRange);
            scanRangeDynamicMatrix.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanRangeDynamicScale);
            Graphics.DrawMesh(MeshPool.plane10, scanRangeDynamicMatrix, scanRangeDynamic, 0);

            scanRayDynamicScale = new Vector3(2f * scanRange, 1f, 2f * scanRange);
            scanRayDynamicMatrix.SetTRS(this.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, satelliteDishRotation.ToQuat(), scanRayDynamicScale);
            Graphics.DrawMesh(MeshPool.plane10, scanRayDynamicMatrix, scanRayDynamic, 0);
        }

        /// <summary>
        /// Draw the scan spots on things of def thingDefParameter within scan range.
        /// </summary>
        public void DrawScanSpotOnThingsWithinScanRange(ThingDef thingDefParameter)
        {
            float scanSpotDrawingIntensity = 0f;

            // Get the things within current scan range.
            IEnumerable<Thing> thingsInTheArea = this.Map.listerThings.ThingsOfDef(thingDefParameter);
            if (thingsInTheArea != null)
            {
                thingsInTheArea = thingsInTheArea.Where(thing => thing.Position.InHorDistOf(this.Position, scanRange));
                foreach (Thing thing in thingsInTheArea)
                {
                    if (this.Map.fogGrid.IsFogged(thing.Position) == false)
                    {
                        // Set spot intensity proportional to the dynamic scan ray rotation.
                        Vector3 sonarToMineralVector = thing.Position.ToVector3Shifted() - this.Position.ToVector3Shifted();
                        float orientation = sonarToMineralVector.AngleFlat();
                        scanSpotDrawingIntensity = 1f - (((satelliteDishRotation - orientation + 360) % 360f) / 360f);
                        scanSpotMatrix.SetTRS(thing.Position.ToVector3ShiftedWithAltitude(AltitudeLayer.FogOfWar) + Altitudes.AltIncVect, (0f).ToQuat(), scanSpotScale);
                        Graphics.DrawMesh(MeshPool.plane10, scanSpotMatrix, FadedMaterialPool.FadedVersionOf(scanSpot, scanSpotDrawingIntensity), 0);
                    }
                }
            }
        }

        // ===================== Inspect panel =====================
        /// <summary>
        /// Build the string giving some basic information that is shown when the mobile mineral sonar is selected.
        /// </summary>
        public override string GetInspectString()
        {
            StringBuilder stringBuilder = new StringBuilder();

            stringBuilder.Append(base.GetInspectString());
            stringBuilder.AppendLine();
            stringBuilder.Append("Current/max scan range: " + this.scanRange.ToString() + " / " + maxScanRange.ToString());
            if (this.scanRange < maxScanRange)
            {
                float scanProgressInPercent = ((float)this.scanProgress / (float)(this.scanRange * scanProgressThresholdPerCell)) * 100;
                stringBuilder.AppendLine();
                stringBuilder.Append("Scan progress: " + ((int)scanProgressInPercent).ToString() + " %");
            }

            return stringBuilder.ToString();
        }
    }
}
