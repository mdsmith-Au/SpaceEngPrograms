﻿using System;
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
        IMyGyro gyro;
        IMyRemoteControl remote;
        IMyTextPanel debugPanel;

        MatrixD stationMatrix;

        public Program()
        {
            conn = GridTerminalSystem.GetBlockWithName("Connector") as IMyShipConnector;
            gyro = GridTerminalSystem.GetBlockWithName("Gyroscope") as IMyGyro;
            remote = GridTerminalSystem.GetBlockWithName("Remote Control") as IMyRemoteControl;
            debugPanel = GridTerminalSystem.GetBlockWithName("LCD Panel") as IMyTextPanel;

            stationMatrix = conn.WorldMatrix;

            debugPanel.WritePublicText("Default forward:\n");
            debugPanel.WritePublicText(stationMatrix.Forward.ToString(), true);
            debugPanel.WritePublicText("Modified forward = left:\n", true);
            stationMatrix.Forward = stationMatrix.Left;
            debugPanel.WritePublicText(stationMatrix.Forward.ToString(), true);

            //stationMatrix.Forward = stationMatrix.Left;
        }



        public void Main(string args)
        {
            Echo("Running...");
            MatrixD shipMatrix = remote.WorldMatrix;
            debugPanel.WritePrivateText("Desired forward:\n");
            debugPanel.WritePrivateText(stationMatrix.Forward.ToString(), true);
            debugPanel.WritePrivateText("Actual ship forward:\n", true);
            debugPanel.WritePrivateText(shipMatrix.Forward.ToString(), true);

            QuaternionD target = QuaternionD.CreateFromRotationMatrix(stationMatrix.GetOrientation());
            //QuaternionD target = QuaternionD.CreateFromTwoVectors(stationMatrix.Forward, stationMatrix.Up);

            string qstring = target.ToString();

            string[] splts = qstring.Split(new String[] { " " }, StringSplitOptions.RemoveEmptyEntries);
            Echo("Split num: ");
            Echo(splts.Length.ToString());

            foreach (var str in splts)
            {
                Echo("--" + str + "\n");
            }

            QuaternionD current = QuaternionD.CreateFromRotationMatrix(shipMatrix.GetOrientation());

            QuaternionD rotation = target / current;
            Vector3D axis;
            double angle;
            rotation.GetAxisAngle(out axis, out angle);

            MatrixD worldToGyro = MatrixD.Invert(gyro.WorldMatrix.GetOrientation());
            Vector3D localAxis = Vector3D.Transform(axis, worldToGyro);

            double value = Math.Log(angle + 1, 2);
            localAxis *= value < 0.001 ? 0 : value;
            gyro.SetValueBool("Override", true);
            gyro.SetValueFloat("Power", 1f);
            gyro.SetValue("Pitch", (float)localAxis.X);
            gyro.SetValue("Yaw", (float)-localAxis.Y);
            gyro.SetValue("Roll", (float)-localAxis.Z);
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