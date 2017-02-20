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

        private const string TAG = "AUTO";

        private const bool ENABLE_AIR_VENT_CONTROL = true;
        private const bool ENABLE_AUTO_DOOR = true;

        private const double doorSensorDistanceFrontRear = 1; // in meters
        private const double TIME_DELAY_MS = 80;
        private const double ABORT_SPEED = 3;

        private const float oxygenLevelHigh = 0.99f;
        private const float oxygenLevelLow = 0.9f;

        private List<IMySensorBlock> sensors;
        private IMyTextPanel lcd;
        private List<IMyAirtightDoorBase> doors;

        private bool setupComplete;
        private List<IMyTerminalBlock> allBlocks;

        private List<IMyAirVent> vents;

        private IMyShipController controller;

        private DateTime lastRun;

        private List<IMyTerminalBlock> allBlocksTemp;

        private int counter;

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

            if (doors.Count == 0 && ENABLE_AUTO_DOOR)
            {
                Echo("No doors detected!");
                return false;
            }

            vents = new List<IMyAirVent>();
            grouped.GetBlocksOfType<IMyAirVent>(vents);
            if (vents.Count == 0 && ENABLE_AIR_VENT_CONTROL)
            {
                Echo("No air vents specified");
                return false;
            }

            var controllers = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(controllers);
            if (controllers.Count == 0)
            {
                Echo("No remote control or cockpit detected");
                return false;
            }
            else
            {
                controller = controllers[0];
            }

            allBlocksTemp = new List<IMyTerminalBlock>();

            lastRun = DateTime.Now;
            setupComplete = true;
            return true;
        }


        public void Main(string args)
        {
            // Only run code if > 66 ms has passed since last run(~15 fps)
            if (DateTime.Now - lastRun > TimeSpan.FromMilliseconds(TIME_DELAY_MS))
            {
                allBlocksTemp.Clear();
                var grouped = GridTerminalSystem.GetBlockGroupWithName(TAG);
                if (grouped != null)
                    grouped.GetBlocks(allBlocksTemp);

                if (!setupComplete || !allBlocksTemp.Equals(allBlocks))
                {
                    if (!setup())
                    {
                        return;
                    }
                }

                if (ENABLE_AUTO_DOOR) doorCheck();
                if (ENABLE_AIR_VENT_CONTROL) airVentCheck();

                Turn();

                lastRun = DateTime.Now;

                //Echo("Last run took " + Runtime.LastRunTimeMs.ToString("F3") + " ms.");
                //Echo("IC: " + Runtime.CurrentInstructionCount + "/" + Runtime.MaxInstructionCount);
                //Echo("MC: " + Runtime.CurrentMethodCallCount + "/" + Runtime.MaxMethodCallCount);
            }

        }


        private void doorCheck()
        {
            if (controller.GetShipSpeed() > ABORT_SPEED)
            {
                Echo("Autodoor disabled: ship is moving");
                return;
            }

            // For each door, calculate appropriate bounding box where player must stand
            foreach (var d in doors)
            {

                var bb = d.WorldAABB;
                var forwardDir = bb.Matrix.Forward;
                var backDir = bb.Matrix.Backward;
                var ptForward = d.GetPosition() + doorSensorDistanceFrontRear * forwardDir;
                var ptBackward = d.GetPosition() + doorSensorDistanceFrontRear * backDir;

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
                    d.ApplyAction("Open_On");
                }
                else
                {
                    d.ApplyAction("Open_Off");
                }

            }
        }

        private void airVentCheck()
        {
            foreach (var vent in vents)
            {
                if (!vent.GetValueBool("Depressurize"))
                {
                    float oxygenLevel = vent.GetOxygenLevel();
                    if (oxygenLevel > oxygenLevelHigh)
                        vent.ApplyAction("OnOff_Off");
                    else if (oxygenLevel < oxygenLevelLow)
                        vent.ApplyAction("OnOff_On");
                }
                else if (!vent.Enabled)
                {
                    vent.ApplyAction("OnOff_On");
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


        private void echoPropertiesAndActions(IMyTerminalBlock blk)
        {
            var props = new List<ITerminalProperty>();
            blk.GetProperties(props);

            foreach (var p in props)
            {
                Echo("ID: " + p.Id + " | Type: " + p.TypeName);
            }

            var actions = new List<ITerminalAction>();
            blk.GetActions(actions);

            foreach (var a in actions)
            {
                Echo("ID: " + a.Id + " | Name: " + a.Name);
            }

        }

        // Spinner from http://stackoverflow.com/a/1925137
        public void Turn()
        {
            counter++;
            Echo("Running...");
            switch (counter % 4)
            {
                case 0: Echo("/"); break;
                case 1: Echo("--"); break;
                case 2: Echo("\\"); break;
                case 3: Echo("|"); break;
            }
        }

        public void Save()
        { }

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================
    }
}