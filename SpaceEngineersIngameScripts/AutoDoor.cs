using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace SpaceEngineers
{
    public class Program : MyGridProgram
    {
        //=======================================================================
        //////////////////////////BEGIN//////////////////////////////////////////
        //=======================================================================

        private const string TAG = "AutoDoor";

        private const double doorSensorDistance = 1; // in meters
        private const double TIME_DELAY_MS = 66;
        private const double ABORT_SPEED = 15;

        private List<IMySensorBlock> sensors;
        private IMyTextPanel lcd;
        private List<IMyAirtightDoorBase> doors;

        private bool setupComplete;
        private List<IMyTerminalBlock> allBlocks;

        private DateTime lastRun;

        public Program()
        {
            setup();
        }

        private bool setup()
        {
            setupComplete = false;

            var grouped = GridTerminalSystem.GetBlockGroupWithName(TAG);
            if (grouped == null)
            {
                Echo("Group " + TAG + " does not exist.");
                return false;
            }

            allBlocks = new List<IMyTerminalBlock>();
            grouped.GetBlocks(allBlocks);

            sensors = new List<IMySensorBlock>();
            grouped.GetBlocksOfType<IMySensorBlock>(sensors);

            if (sensors.Count == 0)
            {
                Echo("No sensors detected!");
                return false;
            }

            var lcds = new List<IMyTextPanel>();
            grouped.GetBlocksOfType<IMyTextPanel>(lcds);
            if (lcds.Count == 0)
            {
                lcd = null;
            }
            else
            {
                lcd = lcds[0];
            }

            if (lcd != null)
                lcd.ShowPublicTextOnScreen();

            doors = new List<IMyAirtightDoorBase>();
            grouped.GetBlocksOfType<IMyAirtightDoorBase>(doors);

            if (doors.Count == 0)
            {
                Echo("No doors detected!");
                return false;
            }

            lastRun = DateTime.Now;
            setupComplete = true;
            return true;
        }


        public void Main(string args)
        {
            // Only run code if > 66 ms has passed since last run(~15 fps)
            if (DateTime.Now - lastRun > TimeSpan.FromMilliseconds(TIME_DELAY_MS))
            {
                var allBlocksNow = new List<IMyTerminalBlock>();
                var grouped = GridTerminalSystem.GetBlockGroupWithName(TAG);
                if (grouped != null)
                    grouped.GetBlocks(allBlocksNow);

                if (!setupComplete || !allBlocksNow.Equals(allBlocks))
                {
                    if (!setup())
                    {
                        return;
                    }
                }

                doorCheck();
                printDetectedObjects();

                lastRun = DateTime.Now;

                Echo("Last run took " + Runtime.LastRunTimeMs + " ms.");
            }

        }


        private void doorCheck()
        {

            Echo("Checking against " + doors.Count + " doors.");

            // For each door, calculate appropriate bounding box where player must stand
            foreach (var d in doors)
            {

                var bb = d.WorldAABB;
                var forwardDir = bb.Matrix.Forward;
                var backDir = bb.Matrix.Backward;
                var ptForward = d.GetPosition() + doorSensorDistance * forwardDir;
                var ptBackward = d.GetPosition() + doorSensorDistance * backDir;

                var expandedBB = bb.Include(ptForward).Include(ptBackward);

                bool openDoor = false;

                // Check each door against all sensors
                foreach (var sensor in sensors)
                {
                    var objects = new List<MyDetectedEntityInfo>();
                    sensor.DetectedEntities(objects);

                    // Process each detected object, check against door list and make sure object is a human
                    foreach (var obj in objects)
                    {
                        if (obj.Velocity.Length() > ABORT_SPEED)
                        {
                            Echo("Disabling autodoor as player speed > " + ABORT_SPEED);
                            return;
                        }

                        if (obj.Type == MyDetectedEntityType.CharacterHuman && obj.Relationship != MyRelationsBetweenPlayerAndBlock.Enemies)
                        {
                            if (expandedBB.Contains(obj.Position) == ContainmentType.Contains)
                            {
                                openDoor = true;
                            }
                        }
                    }
                }

                if (openDoor)
                {
                    Echo("Door " + d.CustomName + ": Open");
                    d.ApplyAction("Open_On");
                }
                else
                {
                    Echo("Door " + d.CustomName + ": Closed");
                    d.ApplyAction("Open_Off");
                }

            }
        }

        private void printDetectedObjects()
        {
            var det = new List<MyDetectedEntityInfo>();
            foreach (IMySensorBlock sensor in sensors)
            {
                det.Clear();
                sensor.DetectedEntities(det);
                if (lcd != null)
                lcd.WritePublicText("Detected " + det.Count + " entities.\n", false);

                foreach (var d in det)
                {

                    if (lcd != null)
                    {
                        
                        lcd.WritePublicText("ID: " + d.EntityId + "\n", true);
                        lcd.WritePublicText("Name: " + d.Name + "\n", true);
                        lcd.WritePublicText("Type: " + d.Type + "\n", true);
                        lcd.WritePublicText("Time(ms): " + d.TimeStamp + "\n", true);
                        lcd.WritePublicText("Orientation: " + d.Orientation + "\n", true);
                        lcd.WritePublicText("BB: " + d.BoundingBox + "\n", true);
                        lcd.WritePublicText("Vel: " + d.Velocity + "\n", true);

                        lcd.WritePublicText("Pos: " + convertToGPS("Player", d.Position) + "\n", true);
                        lcd.WritePublicText("Relation: " + d.Relationship + "\n", true);
                    }

                }
            }

        }


        private string convertToGPS(string name, Vector3D vec, char precision = '2')
        {
            //GPS:[DOCK] Laser Antenna:-163.48:-14.56:-40.83:
            return "GPS:" + name + ":" + vec.X.ToString("F" + precision) + ":" + vec.Y.ToString("F" + precision) + ":" + vec.Z.ToString("F" + precision) + ":";
        }


        public void Save()
        { }

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================
    }
}