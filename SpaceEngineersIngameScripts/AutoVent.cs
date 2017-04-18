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

namespace AutoVent
{
    public class Program : MyGridProgram
    {
        //=======================================================================
        //////////////////////////BEGIN//////////////////////////////////////////
        //=======================================================================

        private const string TAG = "AUTO";

        private const float oxygenLevelHigh = 0.99f;
        private const float oxygenLevelLow = 0.9f;

        private bool setupComplete;
        private List<IMyTerminalBlock> allBlocks;

        private List<IMyAirVent> vents;

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

            vents = new List<IMyAirVent>();
            grouped.GetBlocksOfType<IMyAirVent>(vents);
            if (vents.Count == 0)
            {
                Echo("No air vents found");
                return false;
            }

            allBlocksTemp = new List<IMyTerminalBlock>();

            setupComplete = true;
            return true;
        }


        public void Main(string args)
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

            Turn();
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