using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;

using VRageMath;
using VRage.Game;
using VRage.Collections;
using Sandbox.ModAPI.Ingame;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game.EntityComponents;
using SpaceEngineers.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;

namespace LaserComm
{
    public sealed class Program : MyGridProgram
    {
        //=======================================================================
        //////////////////////////BEGIN//////////////////////////////////////////
        //=======================================================================


        // Constants

        #region constants
        private const string BLOCK_PREFIX = "[DOCK]";

        private const double FINAL_APPROACH_DIST = 100;

        private const double BEGIN_APPROACH_DIST = 200;

        private const double FINAL_APPROACH_PLANET = 100;

        private const double DEPARTURE_PLANET = 50;

        private const double DEPARTURE_DIST = 200;

        private const long SECONDS_TO_WAIT_FOR_RESPONSE = 25;

        private const long DOCKING_TIME_SECONDS = 20;

        private const double LANDING_GEAR_HEIGHT = 1.0;

        private const double CONNECTOR_DIST = 2.6;

        #endregion constants

        // Various blocks
        private IMyTextPanel lcdPanel;
        IMyTextPanel messageReceiver;
        private IMyProgrammableBlock WANProgram;
        private IMyShipConnector connector;
        private IMyRemoteControl remoteControl;
        private IMyDoor door;
        private IMyTimerBlock timer;
        private IEnumerator<bool> comm;
        private IEnumerator<bool> fly;
        private IMyLightingBlock landLight;
        private int mainGear;

        private List<IMyGyro> gyros = new List<IMyGyro>();
        private List<IMyLandingGear> gears = new List<IMyLandingGear>();


        // Track potential new blocks, autopilot status, ship/station/planet
        private bool all_blocks_found;
        private int num_blocks_found;

        private bool autopilot_en;

        //string location_name;

        private bool IS_BASE;
        private bool DOCK_LEFT;
        private bool IS_PLANET;

        // For spinner
        int counter;

        private struct Destination
        {
            public List<string> waypoints;
            public string name;

            public Destination(List<string> _waypoints, string _name)
            {
                name = _name;
                waypoints = _waypoints;
            }
        }

        private List<Destination> destinations = new List<Destination>();

        public Program()
        {
            init();
        }

        private void init()
        {
            #region initialization
            all_blocks_found = true;

            autopilot_en = true;

            //location_name = "UNKNOWN";

            // For spinner
            counter = 0;

            string parse = Me.CustomName.Replace(BLOCK_PREFIX, "");
            int id1 = Me.CustomName.IndexOf('[');
            int id2 = Me.CustomName.IndexOf(']');
            if (id1 > 0 && id2 > 0)
            {
                parse = parse.Substring(id1, id2 - id1);
            }
            else
            {
                parse = "";
            }

            IS_BASE = parse.Contains("BASE");

            DOCK_LEFT = parse.Contains("LEFT");

            IS_PLANET = parse.Contains("PLANET");

            if (IS_PLANET) IS_BASE = true;

            //int name_idx = parse.IndexOf("Name:");
            //if (name_idx > 0)
            //{
            //    location_name = parse.Substring(name_idx).Split(:)
            //}

            // Set all known blocks to null or clear lists
            lcdPanel = null;
            messageReceiver = null;
            WANProgram = null;
            connector = null;
            remoteControl = null;
            door = null;
            timer = null;
            landLight = null;
            mainGear = 0;

            gyros.Clear();
            destinations.Clear();
            gears.Clear();

            // Get all blocks
            List<IMyTerminalBlock> blks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(BLOCK_PREFIX, blks, hasPrefix);
            num_blocks_found = blks.Count;


            // Assign blocks to variables as appropriate
            foreach (var blk in blks)
            {
                // LCD panel for printing
                if (blk is IMyTextPanel)
                {
                    lcdPanel = blk as IMyTextPanel;
                }
                // Wico Area Network programmable block
                else if (blk is IMyProgrammableBlock && !blk.Equals(Me))
                {
                    WANProgram = blk as IMyProgrammableBlock;
                }
                // Autopilot
                else if (!IS_BASE && blk is IMyRemoteControl)
                {
                    remoteControl = blk as IMyRemoteControl;
                }
                /* Ship or station connector for docking
                 * Used to connect to station and for orientation info
                 */
                else if (!IS_PLANET && blk is IMyShipConnector)
                {
                    connector = blk as IMyShipConnector;
                }
                /* Door used for docking; used for orientation information
                 * since it's more obvious which way a door faces than a connector
                 */
                else if (!IS_PLANET && blk is IMyDoor)
                {
                    door = blk as IMyDoor;
                }
                // Gyros for ship orientation
                else if (!IS_BASE && blk is IMyGyro)
                {
                    IMyGyro g = blk as IMyGyro;
                    gyros.Add(g);

                }
                // Timer block so that we can orient ship properly - requires multiple calls/sec
                else if (!IS_BASE && blk is IMyTimerBlock)
                {
                    timer = blk as IMyTimerBlock;
                    timer.SetValueFloat("TriggerDelay", 1.0f);
                }
                // Light (interior or spotlight) determines where we will land
                else if (IS_BASE && IS_PLANET && blk is IMyLightingBlock)
                {
                    landLight = blk as IMyLightingBlock;
                }
                // Landing gear....
                else if (!IS_BASE && blk is IMyLandingGear)
                {
                    IMyLandingGear gear = blk as IMyLandingGear;
                    gears.Add(gear);
                    if (gear.CustomName.ToLower().Contains("main"))
                    {
                        mainGear = gears.Count - 1;
                    }
                }
            }

            // Make sure all gyros reset
            resetGyros();

            // Clear block list
            blks.Clear();

            // Get text panel blocks used by Wico Area Network for communication
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(blks, hasWANRPrefix);

            if (blks.Count == 0)
            {
                Echo("Error: Can't find message received text panel for Wico Area Network");
                all_blocks_found = false;
            }
            else
            {
                messageReceiver = blks[0] as IMyTextPanel;
                messageReceiver.WritePublicTitle("");
                messageReceiver.WritePrivateTitle("NAV");
            }

            if (WANProgram == null)
            {
                Echo("Error: Can't find programming block for Wico Area Network");
                all_blocks_found = false;
            }

            if (lcdPanel == null)
            {
                Echo("Error: Expect 1 LCD");
                all_blocks_found = false;
            }

            if (!IS_PLANET && connector == null)
            {
                Echo("Error: Can't find any connectors to use for docking");
                all_blocks_found = false;
            }

            if (!IS_BASE && remoteControl == null)
            {
                Echo("Error: Can't find any remote control blocks");
                all_blocks_found = false;
            }

            if (!IS_PLANET && door == null)
            {
                Echo("Error: Can't find door");
                all_blocks_found = false;
            }

            if (!IS_BASE && gyros.Count == 0)
            {
                Echo("Error: No gyros detected");
                all_blocks_found = false;
            }

            if (!IS_BASE && timer == null)
            {
                Echo("Error: No timer found");
                all_blocks_found = false;
            }
            if (IS_PLANET && landLight == null)
            {
                Echo("Error: No light for landing ship destination found");
                all_blocks_found = false;
            }
            if (!IS_BASE && gears.Count == 0)
            {
                Echo("Warning: no landing gear found.  You will not be able to land on planets");
            }

            // Init communicator state machine
            comm = communicate().GetEnumerator();

            // Clear autopilot state machine
            fly = null;
            #endregion
        }

        public void Main(string args)
        {
            #region autoGridBlockAdd
            // Scan for any new blocks on any run
            List<IMyTerminalBlock> blks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(BLOCK_PREFIX, blks);

            // However, don't add any new blocks if the connector is connected otherwise we'll get stuff from the connected ship/station
            bool connLock = false;
            try
            {
                connLock = connector.IsLocked;
            }

            catch { }

            if (!connLock && (!all_blocks_found || blks.Count != num_blocks_found))
            {
                init();
            }

            #endregion

            // Run communicator state machine
            if (comm != null)
            {
                if (!comm.MoveNext() || !comm.Current)
                {
                    comm = null;
                }
                else
                {
                    if (IS_BASE)
                    {
                        Echo("Running base...");
                        Turn();
                    }
                    else
                    {
                        Echo("Running ship: communication...");
                        Turn();
                    }
                    return;
                }
            }


            // NOTE: base code never (should) get here

            if (destinations.Count == 0)
            {
                Echo("Error: no destinations received");
                init();

                return;
            }

            #region debugPanelWrite
            lcdPanel.WritePublicText("Destinations:\n");

            foreach (var dst in destinations)
            {
                lcdPanel.WritePublicText("Name: " + dst.name + "\n", true);
                lcdPanel.WritePublicText("Locations:\n", true);
                foreach (var pt in dst.waypoints)
                {
                    lcdPanel.WritePublicText(pt + "\n", true);
                }
            }
            #endregion

            if (args.Contains("STOP"))
            {
                autopilot_en = false;
            }
            else if (args.Contains("START"))
            {
                autopilot_en = true;
            }

            // Create autopilot instance if appropriate
            if (!IS_BASE && comm == null && fly == null && autopilot_en && destinations.Count > 0)
            {
                fly = autopilot().GetEnumerator();
            }


            if (fly != null)
            {
                if (!fly.MoveNext() || !fly.Current)
                {
                    fly = null;
                }
                else
                {
                    Echo("Running ship: autopilot...");
                    Turn();
                    return;
                }
            }


        }

        public IEnumerable<bool> autopilot()
        {
            #region autopilot
            // Clear all waypoints, turn off collision avoidance and docking mode
            remoteControl.ClearWaypoints();
            //remoteControl.ApplyAction("CollisionAvoidance_On");
            remoteControl.ApplyAction("CollisionAvoidance_Off");
            remoteControl.ApplyAction("DockingMode_Off");

            Echo("Setting destination...");

            // Make sure all gyros set to no override
            resetGyros();

            // For each destination base...
            foreach (var location in destinations)
            {

                List<Vector3D> departurePoints = new List<Vector3D>();
                List<Vector3D> approachPoints = new List<Vector3D>();
                Vector3D destination = new Vector3D();

                if (location.name.ToLower().Contains("planet"))
                {
                    #region PLANET_AUTOPILOT
                    // Check landing gear existence
                    if (gears.Count == 0)
                    {
                        Echo("Error: can't go to a planet without landing gear");
                        Echo("Skipping destination");
                        // Pause
                        for (int i = 0; i < 10; i++)
                        {
                            yield return true;
                        }
                        continue;
                    }

                    Vector3D fwdOrient = new Vector3D();

                    // Go through list of received points
                    foreach (var pt in location.waypoints)
                    {
                        Echo("Adding point " + pt);

                        // Interpret GPS points
                        Vector3D vec;
                        string ptName = convertToVector(pt, out vec);
                        ptName = ptName.ToLower();

                        // Case 1: orientation: Up for base = forward for us
                        if (ptName.Contains("orientation") && ptName.Contains("up"))
                        {
                            fwdOrient = vec;
                        }

                        // Other cases
                        else if (ptName.Contains("approach"))
                        {
                            approachPoints.Add(vec);
                        }
                        else if (ptName.Contains("destination"))
                        {
                            destination = vec;
                        }
                        else if (ptName.Contains("departure"))
                        {
                            departurePoints.Add(vec);
                        }
                    }


                    if (approachPoints.Count == 0)
                    {
                        Echo("Error: no approach points");
                        yield return false;
                    }

                    remoteControl.AddWaypoint(approachPoints[0], "Approach");

                    Echo("Approach set; enabling autopilot");

                    remoteControl.SetAutoPilotEnabled(true);

                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    remoteControl.ClearWaypoints();

                    // We're close, enable docking mode
                    Echo("Enabled docking mode; close to destination");
                    remoteControl.ApplyAction("DockingMode_On");

                    // Add final approach waypoints, go
                    for (int i = 1; i < approachPoints.Count; i++)
                    {
                        remoteControl.AddWaypoint(approachPoints[i], "Final approach " + i.ToString());
                    }

                    remoteControl.SetAutoPilotEnabled(true);

                    // Wait for autopilot to arrive
                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    Echo("Arrived at final approach");
                    // We've arrived at the final approach point
                    remoteControl.ClearWaypoints();

                    // Calculate basic remote control -> landing gear offset
                    Vector3D offset = remoteControl.GetPosition() - gears[mainGear].GetPosition();
                    // Take into consideration the fact that the landing gear is big
                    Vector3D grav = remoteControl.GetNaturalGravity();
                    grav.Normalize();
                    offset += grav * LANDING_GEAR_HEIGHT;

                    // Add destination point + offset to account for remote control positioning
                    remoteControl.AddWaypoint(destination + offset, "Destination");

                    remoteControl.SetAutoPilotEnabled(true);

                    // Wait for autopilot to arrive
                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    remoteControl.ApplyAction("DockingMode_Off");
                    remoteControl.ClearWaypoints();

                    // We're here, do a final orientation
                    while (!rotate(fwdOrient, remoteControl.WorldMatrix.GetOrientation().Forward))
                    {
                        yield return true;
                    }

                    Echo("Arrived at destination");

                    foreach (var g in gears)
                    {
                        g.ApplyAction("Lock");
                    }

                    // Wait certain amount of time before undocking
                    long startTick = DateTime.Now.Ticks;

                    while (DateTime.Now.Ticks - startTick < 10000000L * DOCKING_TIME_SECONDS)
                    {
                        Echo("Waiting to leave");
                        yield return true;
                    }

                    // Recalculate offset; depending where the remote control block is, not having a good offset might mean a collision

                    offset = (remoteControl.GetPosition() - gears[mainGear].GetPosition()) + grav * LANDING_GEAR_HEIGHT;

                    Echo("Adding departure points");

                    for (int i = 0; i < departurePoints.Count; i++)
                    {
                        remoteControl.AddWaypoint(departurePoints[i] + offset, "Departure " + (i + 1).ToString());
                    }

                    Echo("Unlocking gears");

                    foreach (var g in gears)
                    {
                        g.ApplyAction("Unlock");
                    }

                    // Cheap way of waiting 2 seconds for docking stuff to retract
                    for (int k = 0; k < 2; k++)
                    {
                        yield return true;
                    }
                    

                    

                    Echo("Autopilot enabled");
                    remoteControl.SetAutoPilotEnabled(true);

                    // Wait a bit for the autopilot to take, otherwise it goes haywire
                    yield return true;

                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    // We've departed; clear waypoints
                    remoteControl.ClearWaypoints();
                    #endregion
                }
                else
                {
                    #region NORMAL_AUTOPILOT
                    Vector3D downOrient = new Vector3D();
                    Vector3D fwdOrient = new Vector3D();

                    // Go through list of received points
                    foreach (var pt in location.waypoints)
                    {
                        Echo("Adding point " + pt);

                        // Interpret GPS points
                        Vector3D vec;
                        string ptName = convertToVector(pt, out vec);
                        ptName = ptName.ToLower();

                        // Case 1: orientation
                        if (ptName.Contains("orientation"))
                        {
                            if (ptName.Contains("down"))
                            {
                                downOrient = vec;
                            }
                            else if (ptName.Contains("forward"))
                            {
                                fwdOrient = vec;
                            }
                        }

                        // Other cases
                        else if (ptName.Contains("approach"))
                        {
                            approachPoints.Add(vec);
                        }
                        else if (ptName.Contains("destination"))
                        {
                            destination = vec;
                        }
                        else if (ptName.Contains("departure"))
                        {
                            departurePoints.Add(vec);
                        }
                    }


                    if (approachPoints.Count == 0)
                    {
                        Echo("Error: no approach points");
                        yield return false;
                    }

                    remoteControl.AddWaypoint(approachPoints[0], "Approach 1");

                    Echo("Approach set; enabling autopilot");

                    remoteControl.SetAutoPilotEnabled(true);

                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    remoteControl.ClearWaypoints();

                    //Have now reached first approach point: orient

                    while (!rotate(downOrient, remoteControl.WorldMatrix.GetOrientation().Down))
                    {
                        yield return true;
                    }
                    while (!rotate(fwdOrient, remoteControl.WorldMatrix.GetOrientation().Forward))
                    {
                        yield return true;
                    }

                    // We're close, enable docking mode
                    Echo("Enabled docking mode; close to destination");
                    remoteControl.ApplyAction("DockingMode_On");
                    //remoteControl.ApplyAction("CollisionAvoidance_Off");

                    // Calculate offset now that we are properly rotated
                    Vector3D offset = remoteControl.GetPosition() - connector.GetPosition();

                    // Add final approach waypoints, go
                    for (int i = 1; i < approachPoints.Count; i++)
                    {
                        remoteControl.AddWaypoint(approachPoints[i] + offset, "Final approach " + i.ToString());
                    }

                    remoteControl.SetAutoPilotEnabled(true);

                    // Wait for autopilot to arrive
                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    // We've arrived at the final destination
                    remoteControl.ClearWaypoints();

                    // Orient again
                    while (!rotate(downOrient, remoteControl.WorldMatrix.GetOrientation().Down))
                    {
                        yield return true;
                    }
                    while (!rotate(fwdOrient, remoteControl.WorldMatrix.GetOrientation().Forward))
                    {
                        yield return true;
                    }

                    // Re-calculate offset
                    offset = remoteControl.GetPosition() - connector.GetPosition();

                    // Add destination point, go
                    remoteControl.AddWaypoint(destination + offset, "Destination");

                    remoteControl.SetAutoPilotEnabled(true);

                    // Wait for autopilot to arrive
                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        // If connector becomes connected, disable autopilot
                        if (connector.IsConnected)
                        {
                            remoteControl.SetAutoPilotEnabled(false);
                        }
                        else
                        {
                            yield return true;
                        }

                    }

                    remoteControl.ClearWaypoints();

                    remoteControl.ApplyAction("DockingMode_Off");

                    // We're here, do a final orientation to make sure connector lines up if we're not already connected
                    while (!rotate(downOrient, remoteControl.WorldMatrix.GetOrientation().Down))
                    {
                        // If connector becomes connected, stop
                        if (connector.IsConnected)
                        {
                            resetGyros();
                            break;
                        }
                        else
                        {
                            yield return true;
                        }
                    }
                    while (!rotate(fwdOrient, remoteControl.WorldMatrix.GetOrientation().Forward))
                    {
                        // If connector becomes connected, stop
                        if (connector.IsConnected)
                        {
                            resetGyros();
                            break;
                        }
                        else
                        {
                            yield return true;
                        }
                    }

                    Echo("Arrived at destination");

                    connector.ApplyAction("Lock");

                    // Wait certain amount of time before undocking
                    long startTick = DateTime.Now.Ticks;

                    while (DateTime.Now.Ticks - startTick < 10000000L * DOCKING_TIME_SECONDS)
                    {
                        Echo("Waiting to undock");
                        yield return true;
                    }

                    // Recalculate offset; depending where the remote control block is, not having a good offset might mean a collision

                    offset = remoteControl.GetPosition() - connector.GetPosition();

                    connector.ApplyAction("Unlock");

                    for (int i = 0; i < departurePoints.Count; i++)
                    {
                        remoteControl.AddWaypoint(departurePoints[i] + offset, "Departure " + (i + 1).ToString());
                    }

                    remoteControl.SetAutoPilotEnabled(true);

                    while (remoteControl.IsAutoPilotEnabled)
                    {
                        yield return true;
                    }

                    // We've departed; clear waypoints and renable collision avoidance
                    remoteControl.ClearWaypoints();
                    //remoteControl.ApplyAction("CollisionAvoidance_On");

                    #endregion
                }

            }

            yield return false;

            #endregion
        }

        // Thanks to albmar @ http://forum.keenswh.com/threads/how-can-i-roll-my-ship-to-align-its-floor-with-the-floor-of-a-station.7382390/#post-1286963408
        // So I did take a university level analytical mechanics course at one point but it turns out I forgot everything so thanks for the save
        private bool rotate(Vector3D destOrient, Vector3D curOrient)
        {
            bool doneRotating = false;

            Echo("Rotating");

            // To be sure, we divide by the length of both vectors
            Vector3D axis = Vector3D.Cross(destOrient, curOrient) / (destOrient.Length() * curOrient.Length());
            // the Normalize method normalizes the axis and returns the length it had before
            double angle = Math.Asin(axis.Normalize());

            foreach (var gyro in gyros)
            {
                MatrixD worldToGyro = MatrixD.Invert(gyro.WorldMatrix.GetOrientation());
                Vector3D localAxis = Vector3D.Transform(axis, worldToGyro);

                // Stop if we reached threshold
                double value = Math.Log(angle + 1, 2);
                if (value < 0.0001)
                {
                    localAxis *= 0;
                    doneRotating = true;
                }
                else
                {
                    localAxis *= value;
                }

                gyro.SetValueBool("Override", true);
                gyro.SetValueFloat("Power", 1f);
                // Seems at some point KSH changed the signs on pitch/yaw/roll
                gyro.SetValue("Pitch", (float)-localAxis.X);
                gyro.SetValue("Yaw", (float)localAxis.Y);
                gyro.SetValue("Roll", (float)localAxis.Z);
            }

            if (doneRotating)
            {
                resetGyros();
            }

            timer.ApplyAction("TriggerNow");

            return doneRotating;

        }

        // Set override on all gyros to off
        private void resetGyros()
        {
            foreach (var gyro in gyros)
            {
                gyro.SetValueFloat("Power", 1f);
                gyro.SetValueBool("Override", false);
            }
        }

        // Communication state machine
        public IEnumerable<bool> communicate()
        {
            // If we're a ship, send out requests for nav data
            if (!IS_BASE)
            {
                #region SHIP_COMM

                // Clear out message received area
                messageReceiver.WritePrivateText("");

                // Try sending message until PB accepts execution
                Echo("Sending NAV request");
                if (!WANProgram.TryRun("SEND 2 NAV - B REQUEST_NAV"))
                {
                    Echo("Error sending message");
                    yield return false;
                }

                // Keep processing new messages until we timeout waiting for any more potential responses
                // This is because we don't know how many locations may send in responses
                long ticks = DateTime.Now.Ticks;
                while (DateTime.Now.Ticks - ticks < 10000000L * SECONDS_TO_WAIT_FOR_RESPONSE)
                {

                    // Now check text panel for response
                    while (!messageReceiver.GetPrivateText().Contains("RESPONSE_NAV"))
                    {

                        // timeout
                        if (DateTime.Now.Ticks - ticks > 10000000 * SECONDS_TO_WAIT_FOR_RESPONSE)
                        {
                            Echo("Timed out waiting for (any more) responses");
                            yield return false;
                        }
                        else
                        {
                            Echo("Waiting for responses...");
                            yield return true;
                        }
                    }

                    // Now we have our response
                    string response = messageReceiver.GetPrivateText();
                    messageReceiver.WritePrivateText("");

                    Echo("Processing responses");
                    string[] uniqueResponses = response.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);

                    foreach (var resp in uniqueResponses)
                    {
                        string[] responseSplit = resp.Split('|');

                        List<string> wpts = new List<string>();
                        for (int i = 2; i < responseSplit.Length; i++)
                        {
                            wpts.Add(responseSplit[i]);
                        }

                        Destination dest = new Destination(wpts, responseSplit[1]);
                        bool exists = false;

                        foreach (var d in destinations)
                        {
                            if (d.name.Equals(dest.name))
                            {
                                exists = true;
                            }
                        }

                        if (!exists)
                        {
                            destinations.Add(dest);
                        }

                    }

                    yield return true;
                }

                #endregion
            }

            // Case: base communication
            else
            {
                #region BASE_COMM
                messageReceiver.WritePrivateText("");
                while (true)
                {
                    // Wait for message
                    while (!messageReceiver.GetPrivateText().Contains("REQUEST_NAV"))
                    {
                        yield return true;
                    }

                    // Clear message
                    messageReceiver.WritePrivateText("");

                    // Compose response
                    string response = "RESPONSE_NAV";

                    foreach (var vec in getWaypoints())
                    {
                        response = response + '|' + vec;
                    }
                    response += ';';

                    // Send response
                    if (!WANProgram.TryRun("SEND 2 NAV - B " + response))
                    {
                        yield return false;
                    }
                }
                #endregion
            }



        }

        public void Save()
        { }

        private bool hasPrefix(IMyTerminalBlock blk)
        {
            return blk.CustomName.StartsWith(BLOCK_PREFIX);
        }

        private bool hasWANRPrefix(IMyTerminalBlock blk)
        {
            return blk.CustomName.Contains("[WANR]");
        }

        private string convertToGPS(string name, Vector3D vec, char precision = '2')
        {
            //GPS:[DOCK] Laser Antenna:-163.48:-14.56:-40.83:
            return "GPS:" + name + ":" + vec.X.ToString("F" + precision) + ":" + vec.Y.ToString("F" + precision) + ":" + vec.Z.ToString("F" + precision) + ":";
        }

        private string convertToVector(string gps, out Vector3D vec)
        {
            string[] splits = gps.Split(':');
            vec = new Vector3D(Double.Parse(splits[2]), Double.Parse(splits[3]), Double.Parse(splits[4]));
            return splits[1];
        }

        private void printPropertiesAndActions(IMyTerminalBlock block)
        {

            List<ITerminalProperty> prop = new List<ITerminalProperty>();
            block.GetProperties(prop);

            Echo("Properties:");
            foreach (ITerminalProperty p in prop)
            {
                Echo("ID: " + p.Id);
                Echo("Type: " + p.TypeName);
            }

            List<ITerminalAction> acts = new List<ITerminalAction>();
            block.GetActions(acts);

            Echo("Actions:");
            foreach (ITerminalAction a in acts)
            {
                //Echo("Icon: " + a.Icon);
                Echo("ID: " + a.Id);
                Echo("Name: " + a.Name);
            }
        }

        private List<string> getWaypoints()
        {

            if (!IS_PLANET)
            {
                #region SPACE_WAYPOiNTS
                // Final destionation: 2.5m in front of connector
                // Add +0.1 so that we don't hit connector itself

                // Set final point in front of connector
                Vector3D finalPt = connector.GetPosition() + 2.6 * connector.WorldMatrix.GetOrientation().Forward;
                // Add a small offset to that the autopilot will try and go slightly past the connector
                finalPt += 0.2 * (DOCK_LEFT ? door.WorldMatrix.GetOrientation().Right : door.WorldMatrix.GetOrientation().Left);

                Vector3D finalApproach = DOCK_LEFT ? finalPt + FINAL_APPROACH_DIST * door.WorldMatrix.GetOrientation().Left : finalPt + FINAL_APPROACH_DIST * door.WorldMatrix.GetOrientation().Right;
                Vector3D beginApproach = DOCK_LEFT ? finalPt + BEGIN_APPROACH_DIST * door.WorldMatrix.GetOrientation().Left : finalPt + BEGIN_APPROACH_DIST * door.WorldMatrix.GetOrientation().Right;
                Vector3D departure = finalPt + DEPARTURE_DIST * (DOCK_LEFT ? door.WorldMatrix.GetOrientation().Right : door.WorldMatrix.GetOrientation().Left);

                Vector3D down = door.WorldMatrix.GetOrientation().Down;
                Vector3D shipFwd = DOCK_LEFT ? door.WorldMatrix.GetOrientation().Right : door.WorldMatrix.GetOrientation().Left;

                return new List<string> { "Space " + Runtime.LastRunTimeMs.ToString(), convertToGPS("Begin Approach", beginApproach), convertToGPS("Final Approach", finalApproach), convertToGPS("Destination", finalPt), convertToGPS("Departure", departure), convertToGPS("Orientation Down", down, '4'), convertToGPS("Orientation Forward", shipFwd, '4') };
                #endregion
            }
            else
            {
                #region PLANET_WAYPOINTS
                Vector3D finalPt = landLight.GetPosition() + CONNECTOR_DIST * landLight.WorldMatrix.GetOrientation().Forward;

                Vector3D approach = finalPt + FINAL_APPROACH_PLANET * landLight.WorldMatrix.GetOrientation().Forward + FINAL_APPROACH_PLANET / 2.0 * landLight.WorldMatrix.GetOrientation().Down;
                Vector3D departure = finalPt + DEPARTURE_PLANET * landLight.WorldMatrix.GetOrientation().Forward + DEPARTURE_PLANET / 2.0 * landLight.WorldMatrix.GetOrientation().Down;

                Vector3D upOrient = landLight.WorldMatrix.GetOrientation().Up;
                // Note: departure = approach
                return new List<string> { "Planet " + Runtime.LastRunTimeMs.ToString(), convertToGPS("Approach", approach), convertToGPS("Destination", finalPt), convertToGPS("Departure", departure), convertToGPS("Orientation Up", upOrient, '4') };
                #endregion
            }

        }

        // Spinner from http://stackoverflow.com/a/1925137
        public void Turn()
        {
            counter++;
            switch (counter % 4)
            {
                case 0: Echo("/"); break;
                case 1: Echo("-"); break;
                case 2: Echo("\\"); break;
                case 3: Echo("|"); break;
            }
        }

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================

    }
}