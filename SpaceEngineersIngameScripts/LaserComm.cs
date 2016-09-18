﻿using System;
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

        private const string BLOCK_PREFIX = "[DOCK]";

        private bool DOCK_LEFT;

        private const float FINAL_APPROACH_DIST = 100;

        private const float BEGIN_APPROACH_DIST = 200;

        private const float DEPARTURE_DIST = 200;

        private const int SECONDS_TO_WAIT_FOR_RESPONSE = 15;

        private bool IS_BASE;

        // Examples:
        /*
        With one GPS:
        -------
        private List<string> GPS_COORDINATES = new List<string>
        {
            "GPS:Receiver Laser Antenna:4.41:3.8:-7.09:"
        };
        -------
        With two GPS:
        -------
        private List<string> GPS_COORDINATES = new List<string>
        {
            "GPS:Receiver Laser Antenna:4.41:3.8:-7.09:",
            "GPS:Bob's Antenna:44.31:-9.8:-12.88:"
        };
        -------
        and so on...
        */

        private IMyTextPanel debugPanel;
        IMyTextPanel messageReceiver;
        private IMyProgrammableBlock WANProgram;
        private IMyShipConnector connector;
        private IMyRemoteControl remoteControl;
        private IMyDoor door;
        private IMyTimerBlock timer;
        private IEnumerator<bool> comm;
        private IEnumerator<bool> fly;

        private List<IMyGyro> gyros = new List<IMyGyro>();

        private bool all_blocks_found;
        private int num_blocks_found;

        private bool autopilot_en;

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
            all_blocks_found = true;

            autopilot_en = true;

            IS_BASE = Me.CustomName.Contains("[BASE]") ? true : false;

            DOCK_LEFT = Me.CustomName.Contains("[LEFT]") ? true : false;

            List<IMyTerminalBlock> blks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(BLOCK_PREFIX, blks, hasPrefix);
            num_blocks_found = blks.Count;

            gyros.Clear();
            destinations.Clear();

            foreach (var blk in blks)
            {
                if (blk is IMyTextPanel)
                {
                    debugPanel = blk as IMyTextPanel;
                }
                else if (blk is IMyProgrammableBlock && !blk.Equals(Me))
                {
                    WANProgram = blk as IMyProgrammableBlock;
                }
                else if (!IS_BASE && blk is IMyRemoteControl)
                {
                    remoteControl = blk as IMyRemoteControl;
                }
                else if (blk is IMyShipConnector)
                {
                    connector = blk as IMyShipConnector;
                }
                else if (blk is IMyDoor)
                {
                    door = blk as IMyDoor;
                }
                else if (!IS_BASE && blk is IMyGyro)
                {
                    gyros.Add(blk as IMyGyro);
                }
                else if (!IS_BASE && blk is IMyTimerBlock)
                {
                    timer = blk as IMyTimerBlock;
                    timer.SetValueFloat("TriggerDelay", 1.0f);
                }
            }

            blks.Clear();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(blks, hasWANRPrefix);

            if (blks.Count == 0)
            {
                Echo("Can't find message received text panel for Wico Area Network");
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
                Echo("Can't find programming block for Wico Area Network");
                all_blocks_found = false;
            }

            if (debugPanel == null)
            {
                Echo("Expect 1 debug LCD");
                all_blocks_found = false;
            }

            if (connector == null)
            {
                Echo("Can't find any connectors to use for docking");
                all_blocks_found = false;
            }

            if (!IS_BASE && remoteControl == null)
            {
                Echo("Can't find any remote control blocks");
                all_blocks_found = false;
            }

            if (door == null)
            {
                Echo("Can't find door");
                all_blocks_found = false;
            }

            if (!IS_BASE && gyros.Count == 0)
            {
                Echo("No gyros detected");
                all_blocks_found = false;
            }

            if (!IS_BASE && timer == null)
            {
                Echo("No timer found");
                all_blocks_found = false;
            }

            comm = communicate().GetEnumerator();
        }

        public void Main(string args)
        {
            List<IMyTerminalBlock> blks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(BLOCK_PREFIX, blks);
            if (!all_blocks_found || blks.Count != num_blocks_found)
            {
                init();
                return;
            }

            // Run state machine
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
                        Echo("Runing base...");
                    }
                    else
                    {
                        Echo("Running ship...");
                    }
                    Echo("Current time: " + DateTime.Now.ToLongTimeString());
                    return;
                }
            }

            // NOTE: base code never gets here

            if (destinations.Count == 0)
            {
                Echo("Error: no destinations received");
                init();
                
                return;
            }

            debugPanel.WritePublicText("Destinations:\n");

            foreach (var dst in destinations)
            {
                debugPanel.WritePublicText("Name: " + dst.name + "\n", true);
                debugPanel.WritePublicText("Locations:\n", true);
                foreach (var pt in dst.waypoints)
                {
                    debugPanel.WritePublicText(pt, true);
                }
            }

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
                    Echo("Autopilot running...");
                    return;
                }
            }

            Echo("DONE!");
        }

        public IEnumerable<bool> autopilot()
        {
            remoteControl.ClearWaypoints();
            remoteControl.ApplyAction("CollisionAvoidance_Off");
            remoteControl.ApplyAction("DockingMode_Off");

            Echo("Setting destination...");

            foreach (var gyro in gyros)
            {
                gyro.SetValueBool("Override", false);
                gyro.SetValueFloat("Power", 1f);
            }

            yield return true;

            // For each destination base...
            foreach (var location in destinations)
            {

                // Add all waypoints except destination and departure
                List<Vector3D> departurePoints = new List<Vector3D>();
                List<Vector3D> approachPoints = new List<Vector3D>();
                Vector3D destination = new Vector3D();

                Vector3D orientForward = new Vector3D();
                Vector3D orientUp = new Vector3D();

                foreach (var pt in location.waypoints)
                {
                    Echo("Adding point " + pt);
                    Vector3D vec;
                    string ptName = convertToVector(pt, out vec);
                    string ptName2 = ptName.ToLower();

                    // Orientation: save as is
                    if (ptName2.Contains("orientation"))
                    {
                        if (ptName2.Contains("forward"))
                        {
                            orientForward = vec;
                        }
                        else if (ptName2.Contains("up"))
                        {
                            orientUp = vec;
                        }
                    }
                    // Otherwise, it is an actual GPS coordinate
                    else
                    {

                        if (ptName2.Contains("approach"))
                        {
                            approachPoints.Add(vec);
                        }
                        else if (ptName2.Contains("destination"))
                        {
                            destination = vec;
                        }
                        else if (ptName2.Contains("departure"))
                        {
                            departurePoints.Add(vec);
                        }
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

                Echo("Enabled docking mode; close to destination");
                remoteControl.ApplyAction("DockingMode_On");

                bool doneRotating = false;

                while (!doneRotating)
                {
                    Echo("Rotating");

                    QuaternionD target = QuaternionD.CreateFromForwardUp(orientForward, orientUp);
                    QuaternionD current = QuaternionD.CreateFromRotationMatrix(remoteControl.WorldMatrix.GetOrientation());
                    QuaternionD rotation = target / current;
                    Vector3D axis;
                    double angle;
                    rotation.GetAxisAngle(out axis, out angle);

                    foreach (var gyro in gyros)
                    {
                        MatrixD worldToGyro = MatrixD.Invert(gyro.WorldMatrix.GetOrientation());
                        // Test: divide by 2 to reduce magnitude
                        Vector3D localAxis = Vector3D.Transform(axis, worldToGyro)/2.0;

                        double value = Math.Log(angle + 1, 2);
                        if (value < 0.001)
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
                        gyro.SetValue("Pitch", (float)localAxis.X);
                        // Yaw and roll used to be negative it seems
                        gyro.SetValue("Yaw", (float)-localAxis.Y);
                        gyro.SetValue("Roll", (float)-localAxis.Z);
                    }

                    if (doneRotating)
                    {
                        foreach (var gyro in gyros)
                        {
                            gyro.SetValueBool("Override", false);
                        }
                    }
                    timer.ApplyAction("TriggerNow");

                    yield return true;

                }
                Echo("Done rotating");

                // Calculate offset
                Vector3D offset = remoteControl.GetPosition() - connector.GetPosition();

                for (int i = 1; i < approachPoints.Count; i++)
                {
                    remoteControl.AddWaypoint(approachPoints[i] + offset, "Final approach");
                }

                remoteControl.SetAutoPilotEnabled(true);

                // Wait for autopilot to arrive
                while (remoteControl.IsAutoPilotEnabled)
                {
                    yield return true;
                }

                remoteControl.ClearWaypoints();

                doneRotating = false;

                while (!doneRotating)
                {
                    Echo("Rotating");

                    QuaternionD target = QuaternionD.CreateFromForwardUp(orientForward, orientUp);
                    QuaternionD current = QuaternionD.CreateFromRotationMatrix(remoteControl.WorldMatrix.GetOrientation());
                    QuaternionD rotation = target / current;
                    Vector3D axis;
                    double angle;
                    rotation.GetAxisAngle(out axis, out angle);

                    foreach (var gyro in gyros)
                    {
                        MatrixD worldToGyro = MatrixD.Invert(gyro.WorldMatrix.GetOrientation());
                        Vector3D localAxis = Vector3D.Transform(axis, worldToGyro);

                        double value = Math.Log(angle + 1, 2);
                        if (value < 0.001)
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
                        gyro.SetValue("Pitch", (float)localAxis.X);
                        // Yaw and roll used to be negative it seems
                        gyro.SetValue("Yaw", (float)localAxis.Y);
                        gyro.SetValue("Roll", (float)localAxis.Z);
                    }

                    if (doneRotating)
                    {
                        foreach (var gyro in gyros)
                        {
                            gyro.SetValueBool("Override", false);
                        }
                    }
                    timer.ApplyAction("TriggerNow");

                    yield return true;

                }
                Echo("Done rotating");

                // Calculate offset
                offset = remoteControl.GetPosition() - connector.GetPosition();

                remoteControl.AddWaypoint(destination + offset, "Destination");

                remoteControl.SetAutoPilotEnabled(true);

                // Wait for autopilot to arrive
                while (remoteControl.IsAutoPilotEnabled)
                {
                    yield return true;
                }

                remoteControl.ClearWaypoints();
                
                remoteControl.ApplyAction("DockingMode_Off");

                Echo("Arrived at destination");
                autopilot_en = false;

                connector.ApplyAction("Lock");

                // TODO: add docking procedure
                yield return false;
            }


        }

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
                if (!WANProgram.TryRun("SEND 5 NAV - B REQUEST_NAV"))
                {
                    Echo("Error sending message");
                    yield return false;
                }

                // Keep processing new messages until we timeout waiting for any more potential responses
                // This is because we don't know how many locations may send in responses
                int sec = DateTime.Now.Second;
                while (DateTime.Now.Second - sec < SECONDS_TO_WAIT_FOR_RESPONSE)
                {
                    
                    // Now check text panel for response
                    while (!messageReceiver.GetPrivateText().Contains("RESPONSE_NAV"))
                    {
                        
                        // timeout
                        if (DateTime.Now.Second - sec > SECONDS_TO_WAIT_FOR_RESPONSE)
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
                    string[] uniqueResponses = response.Split(';');

                    foreach (var resp in uniqueResponses)
                    {
                        string[] responseSplit = resp.Split('|');

                        List<string> wpts = new List<string>();
                        for (int i = 1; i < responseSplit.Length; i++)
                        {
                            wpts.Add(responseSplit[i]);
                        }

                        destinations.Add(new Destination(wpts, "NAME_HERE"));
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
                    if (!WANProgram.TryRun("SEND 5 NAV - B " + response))
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
            // Final destionation: 2.5m in front of connector

            Vector3D finalPt = connector.GetPosition() + 2.5 * connector.WorldMatrix.GetOrientation().Forward;
            Vector3D finalApproach = DOCK_LEFT ? finalPt + FINAL_APPROACH_DIST * door.WorldMatrix.GetOrientation().Left : finalPt + FINAL_APPROACH_DIST * door.WorldMatrix.GetOrientation().Right;
            Vector3D beginApproach = DOCK_LEFT ? finalPt + BEGIN_APPROACH_DIST * door.WorldMatrix.GetOrientation().Left : finalPt + BEGIN_APPROACH_DIST * door.WorldMatrix.GetOrientation().Right;
            Vector3D departure = finalPt + DEPARTURE_DIST * connector.WorldMatrix.GetOrientation().Forward;

            //Vector3D orientation = door.WorldMatrix.GetOrientation().Down;
            MatrixD orient = door.WorldMatrix.GetOrientation();
            Vector3D forward = DOCK_LEFT ? orient.Left : orient.Right;
            forward.Normalize();
            Vector3D up = orient.Up;
            up.Normalize();

            return new List<string> { convertToGPS("Begin Approach", beginApproach), convertToGPS("Final Approach", finalApproach), convertToGPS("Destination", finalPt), convertToGPS("Departure", departure), convertToGPS("Orientation Forward", forward, '4'), convertToGPS("Orientation Up", up, '4') };
        }

        private string parseLaserName(string info)
        {
            string[] splt = info.Split(new String[] { "Rotating towards " }, StringSplitOptions.None);

            if (splt.Length != 2)
            {
                splt = info.Split(new String[] { "Connected to " }, StringSplitOptions.None);
            }

            if (splt.Length != 2)
            {
                splt = info.Split(new String[] { "Trying to establish connection to " }, StringSplitOptions.None);
            }

            if (splt.Length == 2)
            {
                return splt[1];
            }
            else
            {
                return "";
            }
        }

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================
    }
}