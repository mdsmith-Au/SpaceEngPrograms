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

namespace Experimental
{
    public class Program : MyGridProgram
    {
        //=======================================================================
        //////////////////////////BEGIN//////////////////////////////////////////
        //=======================================================================

        const string DOCK_NAME = "[DOCK]";

        const bool APPROACH_RIGHT = true;

        IMyShipConnector conn;

        public Program()
        {
            List<IMyShipConnector> blks = new List<IMyShipConnector>();
            GridTerminalSystem.GetBlocksOfType(blks, isDock);

            if (blks.Count > 0)
            {
                conn = blks[0];
            }

        }

        public void Main(string args)
        {
            MatrixD wm = conn.WorldMatrix;

            // destination for ship connector = our connector + 2.5m forward
            Vector3D destFinal = conn.GetPosition() + 2.5 * wm.Forward;

            // to line up, specify waypoint before
            Vector3D destPreFinal;
            if (APPROACH_RIGHT)
            {
                destPreFinal = destFinal + wm.Right * 100;
            }
            else
            {
                destPreFinal = destFinal + wm.Left * 100;
            }


            // When received by ship, ship needs to assume these are for the connector and to shift by appropriate amount to remote control center

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