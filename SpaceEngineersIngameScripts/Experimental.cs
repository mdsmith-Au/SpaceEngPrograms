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

        const string sensor_name = "Sensor";
        const string lcdname = "LCD Display";

        IMySensorBlock sensor;
        IMyTextPanel lcd;

        public Program()
        {
            sensor = GridTerminalSystem.GetBlockWithName(sensor_name) as IMySensorBlock;
            lcd = GridTerminalSystem.GetBlockWithName(lcdname) as IMyTextPanel;
            lcd.ShowPublicTextOnScreen();
            
        }



        public void Main(string args)
        {
            //printPropertiesAndActions(sensor
            printDetectedObjects(sensor, lcd);
        }

        private void printDetectedObjects(IMySensorBlock sens, IMyTextPanel lcd)
        {
            var det = new List<MyDetectedEntityInfo>();
            sens.DetectedEntities(det);

            lcd.WritePublicText("Detected " + det.Count + " entities.\n", false);

            foreach (var d in det)
            {

                lcd.WritePublicText("ID: " + d.EntityId + "\n", true);
                lcd.WritePublicText("Name: " + d.Name + "\n", true);
                lcd.WritePublicText("Type: " + d.Type + "\n", true);
                lcd.WritePublicText("Time(ms): " + d.TimeStamp + "\n", true);
                lcd.WritePublicText("Pos: " + d.Position + "\n", true);
                lcd.WritePublicText("Relation: " + d.Relationship + "\n", true);
            }
            

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