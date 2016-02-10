using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;
class AutoDoor : MyGridProgram
{

    // Copy starting here and ignore last } - programming block does not need above class def'n

    // Version 1.0
    // Author: cptmds 2016-02-08

    private AutoDoorProgram autodoor = null;

    void Main(string argument)
    {
        autodoor = new AutoDoorProgram(GridTerminalSystem, Me, Echo, ElapsedTime);

    }



    public class AutoDoorProgram
    {
        // User defined variables

        protected IMyGridTerminalSystem GridTerminalSystem;
        protected Action<string> Echo;
        protected TimeSpan ElapsedTime;
        protected IMyTerminalBlock Me;


        public AutoDoorProgram(IMyGridTerminalSystem grid, IMyProgrammableBlock me, Action<string> echo, TimeSpan elapsedTime)
        {
            GridTerminalSystem = grid;
            Echo = echo;
            ElapsedTime = elapsedTime;
            Me = me;

            IMyDoor door = GridTerminalSystem.GetBlockWithName("TestDoor") as IMyDoor;

            IMySensorBlock sensor = GridTerminalSystem.GetBlockWithName("TestSensor") as IMySensorBlock;

            //Echo("Door position: " + door.GetPosition().ToString());

            VRage.ModAPI.IMyEntity player = sensor.LastDetectedEntity;

            //Echo("Sensor thinks: " + player.GetPosition().ToString());

            double dist = (player.GetPosition() - door.GetPosition()).Length() ;

            Echo("Distance: " + dist.ToString());

            if (dist < 3)
            {
                door.ApplyAction("Open_On");
            }
            else
            {
                door.ApplyAction("Open_Off");
            }

        }
    }

    // Stop copying here
}