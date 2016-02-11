using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;

class Elevator2 : MyGridProgram
{

    // Copy starting here and ignore last } - programming block does not need above class def'n

    // Version 1.0
    // Author: cptmds 2016-02-10


    private Elevator elev = null;

    void Main(string argument)
    {
        if (elev == null) elev = new Elevator(GridTerminalSystem, Me, Echo, ElapsedTime);

        if (argument.Length > 0)
        {

        }
        elev.mainLoop();
    }



    public class Elevator
    {
        // User defined variables

        private const string ELEVATOR_SUFFIX = "[ELEV]"; // What all elevator related blocks should have in their name
        private const float epsilon = 0.2f; // How close the piston must be (in meters) to a floor to be considered at that floor
        private const float elevatorSpeed = 2f; // Speed of the elevator in m/s

        private float[] floorLocations;


        protected IMyGridTerminalSystem GridTerminalSystem;
        protected Action<string> Echo;
        protected TimeSpan ElapsedTime;
        protected IMyTerminalBlock Me;

        private IMySoundBlock sound;
        private List<IMyPistonBase> pistons;
        private IMyDoor cabinDoor;
        private List

       

        public Elevator(IMyGridTerminalSystem grid, IMyProgrammableBlock me, Action<string> echo, TimeSpan elapsedTime)
        {
            GridTerminalSystem = grid;
            Echo = echo;
            ElapsedTime = elapsedTime;
            Me = me;

        }

        // Process all requests - main thread (think while(true) in a microprocessor)
        // Note this assume a 1Hz clock cycle from a timer block
        public void mainLoop()
        {
        }
    }

    // Stop copying here
}