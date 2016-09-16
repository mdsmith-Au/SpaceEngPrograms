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

        private const string BLOCK_PREFIX = "[DOCK]";

        private bool DOCK_LEFT = true;

        private float FINAL_APPROACH_DIST = 100;

        private float BEGIN_APPROACH_DIST = 200;

        private bool IS_BASE = false;

        // GPS Coordinates for laser antennas
        private List<string> GPS_COORDINATES = new List<string>
        {
            "GPS:Receiver Laser Antenna:4.41:3.8:-7.09:"
        };
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

        private const int LASER_TIMEOUT_SEC = 10;

        private List<IMyLaserAntenna> lasers = new List<IMyLaserAntenna>();
        private Dictionary<long, string> oldLaserNames = new Dictionary<long, string>();
        private IMyTextPanel debugPanel;
        private IMyShipConnector connector;
        private IMyRemoteControl remoteControl;
        private IEnumerator<bool> comm;

        private struct Destination
        {
            public List<Vector3D> approachPoints;
            public List<string> vectorNames;
            public string name;

            public Destination(List<Vector3D> _approachPoints, List<string> _vectorNames, string _name)
            {
                name = _name;
                approachPoints = _approachPoints;
                vectorNames = _vectorNames;
            }
        }

        private List<Destination> destinations = new List<Destination>();

        public Program()
        {
            IS_BASE = Me.CustomName.Contains("[BASE]") ? true : false;

            List<IMyTerminalBlock> blks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(BLOCK_PREFIX, blks, hasPrefix);

            foreach (var blk in blks)
            {
                if (blk is IMyLaserAntenna)
                {
                    lasers.Add(blk as IMyLaserAntenna);
                }
                else if (blk is IMyTextPanel)
                {
                    debugPanel = blk as IMyTextPanel;
                }
                else if (!IS_BASE && blk is IMyRemoteControl)
                {
                    remoteControl = blk as IMyRemoteControl;
                }
                else if (IS_BASE && blk is IMyShipConnector )
                {
                    connector = blk as IMyShipConnector;
                }
            }

            if (lasers.Count < 1)
            {
                throw new Exception("Must have at least 1 laser");
            }

            if (debugPanel == null)
            {
                throw new Exception("Expect 1 debug LCD");
            }

            if (IS_BASE && connector == null)
            {
                throw new Exception("Can't find any connectors to use for docking");
            }

            comm = communicate().GetEnumerator();

        }

        public void Main(string args)
        {
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


            Echo("Done with state machine");

            debugPanel.WritePublicText("Destinations:\n");

            foreach (var dst in destinations)
            {
                debugPanel.WritePublicText("Name: " + dst.name + "\n", true);
                debugPanel.WritePublicText("Locations:\n", true);
                foreach (var vec in dst.approachPoints)
                {
                    debugPanel.WritePublicText(convertToGPS("", vec) + "\n", true);
                }
            }

            if (destinations.Count != GPS_COORDINATES.Count)
            {
                Echo("Destinations found not equal to number of GPS coordinates; retrying communication...");
                comm = communicate().GetEnumerator();
            }
        }


        public IEnumerable<bool> communicate()
        {
            // If we're a ship, send out requests for nav data
            if (!IS_BASE)
            {
                #region SHIP_COMM
                // We need to initialize lasers and get destination info
                // Iterate over each base (GPS coord) we know about
                foreach (var gps in GPS_COORDINATES)
                {
                    oldLaserNames.Clear();
                    // Use all lasers
                    foreach (var lsr in lasers)
                    {
                        // Set laser coordiantes, connect
                        lsr.SetTargetCoords(gps);
                        lsr.Connect();
                        Echo("Attempting connection with laser ");
                        Echo(lsr.CustomName);

                        // Wait for connection
                        int st = DateTime.Now.Second;
                        bool timeout = false;
                        while (!lsr.DetailedInfo.Contains("Connected to"))
                        {
                            if (DateTime.Now.Second - st > LASER_TIMEOUT_SEC)
                            {
                                timeout = true;
                                break;
                            }
                            else
                            {
                                yield return true;
                            }
                        }

                        // If we timed out: never connected so try next laser
                        if (timeout)
                        {
                            Echo("Timed out waiting for connection to laser");
                            continue;
                        }
                        // Store old laser name
                        oldLaserNames[lsr.GetId()] = lsr.CustomName;

                        // Set name = set request
                        lsr.SetCustomName(BLOCK_PREFIX + "REQUEST NAV");
                        lsr.ApplyAction("OnOff_Off");
                        lsr.ApplyAction("OnOff_On");

                        Echo("Connection established; requesting navigation data...");

                        // Wait to hear back from other ship
                        st = DateTime.Now.Second;
                        timeout = false;

                        while (!parseLaserName(lsr.DetailedInfo).Contains("RESPONSE NAV"))
                        {
                            if (DateTime.Now.Second - st > LASER_TIMEOUT_SEC)
                            {
                                timeout = true;
                                break;
                            }
                            else
                            {
                                yield return true;
                            }
                        }

                        if (timeout)
                        {
                            Echo("Timed out waiting for nav data");
                            lsr.SetCustomName(oldLaserNames[lsr.GetId()]);
                            oldLaserNames.Remove(lsr.GetId());
                            // Try with next laser
                            continue;
                        }


                        // We got a response - set laset back to original name and then process it
                        lsr.SetCustomName(oldLaserNames[lsr.GetId()]);
                        oldLaserNames.Remove(lsr.GetId());

                        // Also, reconnect laser to let other laser know about new name
                        lsr.ApplyAction("OnOff_Off");
                        lsr.ApplyAction("OnOff_On");

                        string response = parseLaserName(lsr.DetailedInfo);

                        string[] coords = response.Replace(BLOCK_PREFIX, "").Split('|');
                        List<Vector3D> approachPos = new List<Vector3D>();
                        List<string> names = new List<string>();
                        for (int i = 1; i < coords.Length; i++)
                        {
                            Vector3D vec;
                            string nm = convertToVector(coords[i], out vec);
                            approachPos.Add(vec);
                            names.Add(nm);
                        }

                        Vector3D v;
                        string name = convertToVector(gps, out v);
                        destinations.Add(new Destination(approachPos, names, name));
                        

                        // We got our response for this GPS, move on to next one
                        break;
                    }


                }
                #endregion
            }

            // Case: base communication
            else
            {

                while (true)
                {
                    // Calculate approach vector - we update in loop in case our base moves at some point
                    List<Vector3D> wpts = getWaypoints();

                    // Check lasers for incoming messages
                    foreach (var lsr in lasers)
                    {
                        // If connected to something, check for message
                        if (lsr.DetailedInfo.Contains("Rotating towards") || lsr.DetailedInfo.Contains("Connected to") )
                        {
                            string msg = lsr.DetailedInfo.Split(new String[] { "Connected to " }, StringSplitOptions.None)[1];
                            if (msg.Contains("REQUEST NAV"))
                            {
                                oldLaserNames[lsr.GetId()] = lsr.CustomName;
                                string response = BLOCK_PREFIX + "RESPONSE NAV";

                                for (int i = 0; i < wpts.Count; i++)
                                {
                                    string nm;
                                    switch (i)
                                    {
                                        case 0:
                                            nm = "Begin Approach";
                                            break;
                                        case 1:
                                            nm = "Final Approach";
                                            break;
                                        case 2:
                                            nm = "Destination";
                                            break;
                                        default:
                                            nm = "Unknown";
                                            break;
                                    }

                                    response = response + "|" + convertToGPS(nm, wpts[i]);
                                }

                                lsr.SetCustomName(response);

                                // Send (= on/off to update detailed info field)
                                lsr.ApplyAction("OnOff_Off");
                                lsr.ApplyAction("OnOff_On");

                                // Wait timeout amount before assuming data received and going back to default name
                                int sc = DateTime.Now.Second;
                                while (DateTime.Now.Second - sc < LASER_TIMEOUT_SEC)
                                {
                                    yield return true;
                                }

                                lsr.SetCustomName(oldLaserNames[lsr.GetId()]);
                                oldLaserNames.Remove(lsr.GetId());

                                lsr.ApplyAction("OnOff_Off");
                                lsr.ApplyAction("OnOff_On");

                            }
                        }
                    }
                    yield return true;
                }

            }
            


        }

        public void Save()
        { }

        private bool hasPrefix(IMyTerminalBlock blk)
        {
            return blk.CustomName.StartsWith(BLOCK_PREFIX);
        }


        private string convertToGPS(string name, Vector3D vec)
        {
            //GPS:[DOCK] Laser Antenna:-163.48:-14.56:-40.83:
            return "GPS:" + name + ":" + vec.X.ToString("F2") + ":" + vec.Y.ToString("F2") + ":" + vec.Z.ToString("F2") + ":";
        }

        private string convertToVector(string gps, out Vector3D vec)
        {
            string[] splits = gps.Split(':');
            vec = new Vector3D (Double.Parse(splits[2]), Double.Parse(splits[3]), Double.Parse(splits[4]) );
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

        private List<Vector3D> getWaypoints()
        {
            // Final destionation: 2.5m in front of connector

            Vector3D finalPt = connector.GetPosition() + 2.5 * connector.WorldMatrix.Forward;
            Vector3D finalApproach = DOCK_LEFT ? finalPt + FINAL_APPROACH_DIST * connector.WorldMatrix.Left : finalPt + FINAL_APPROACH_DIST * connector.WorldMatrix.Right;
            Vector3D beginApproach = DOCK_LEFT ? finalPt + BEGIN_APPROACH_DIST * connector.WorldMatrix.Left : finalPt + BEGIN_APPROACH_DIST * connector.WorldMatrix.Right;

            return new List<Vector3D> { beginApproach, finalApproach, finalPt };
        }

        private string parseLaserName(string info)
        {
            string[] splt = info.Split(new String[] { "Rotating towards " }, StringSplitOptions.None);

            if (splt.Length != 2)
            {
                splt = info.Split(new String[] { "Connected to " }, StringSplitOptions.None);
            }

            if (splt.Length == 2)
            {
                return splt[1];
            }
            else
            {
                return info;
            }
        }

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================
    }
}