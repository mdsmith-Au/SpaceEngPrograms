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

        const float orbitAltitude = 5000;

        IMyRemoteControl control;
        IMyTextPanel display;
        //IMyCockpit cockpit;

        public Program()
        {
            control = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyRemoteControl;
            display = GridTerminalSystem.GetBlockWithName("LCD Test") as IMyTextPanel;
            //cockpit = GridTerminalSystem.GetBlockWithName("Flight Seat") as IMyCockpit;
        }

        public void Main(string args)
        {
            printPropertiesAndActions(display);

            //MyShipVelocities vel = control.GetShipVelocities();

            //Echo("Velocity: " + vel.LinearVelocity.ToString());
            //Echo("");
            Vector3D grav = control.GetNaturalGravity();
            //Echo("Gravity: " + grav.ToString());

            Vector3D curPos = control.GetPosition();

            Vector3D newPos = curPos + orbitAltitude * -grav.Normalize();

            control.ClearWaypoints();

            control.AddWaypoint(newPos, "up up and Away");

            //control.ApplyAction("Up");
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

        //=======================================================================
        //////////////////////////END////////////////////////////////////////////
        //=======================================================================
    }
}