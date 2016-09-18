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
using Sandbox.Game.Entities;

namespace Experimental
{
    public class Program : MyGridProgram
    {
        //=======================================================================
        //////////////////////////BEGIN//////////////////////////////////////////
        //=======================================================================

        const string DOCK_NAME = "[DOCK]";

        IMyShipConnector conn;
        IMyTextPanel text;

        public Program()
        {
            conn = GridTerminalSystem.GetBlockWithName("[DOCK] Connector") as IMyShipConnector;
            text = GridTerminalSystem.GetBlockWithName("[DOCK] Debug LCD") as IMyTextPanel;
        }



        public void Main(string args)
        {
            MatrixD orientation = conn.WorldMatrix.GetOrientation();
            Vector3D location = conn.GetPosition();
            Vector3D ori = orientation.Left;

            text.WritePublicText(convertToGPS("Left", 2.5*ori + location));
            //Echo("Left: " + ori.ToString());

            ori = orientation.Right;

            text.WritePublicText(convertToGPS("Right", 2.5*ori + location), true);
            //Echo("Right: " + ori.ToString());


            ori = orientation.Down;
            text.WritePublicText(convertToGPS("Down", 2.5*ori + location), true);
            //Echo("Down: " + ori.ToString());

            ori = orientation.Up;
            text.WritePublicText(convertToGPS("Up", 2.5*ori + location), true);
            //Echo("Up: " + ori.ToString());


        }


        private string convertToGPS(string name, Vector3D vec)
        {
            //GPS:[DOCK] Laser Antenna:-163.48:-14.56:-40.83:
            return "GPS:" + name + ":" + vec.X.ToString("F2") + ":" + vec.Y.ToString("F2") + ":" + vec.Z.ToString("F2") + ":";
        }

        private string convertToVector(string gps, out Vector3D vec)
        {
            string[] splits = gps.Split(':');
            vec = new Vector3D(Double.Parse(splits[2]), Double.Parse(splits[3]), Double.Parse(splits[4]));
            return splits[1];
        }


        public void Save()
        { }


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

        private bool isDock(IMyTerminalBlock blk)
        {
            return blk.CustomName.Contains(DOCK_NAME);
        }

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================
    }
}