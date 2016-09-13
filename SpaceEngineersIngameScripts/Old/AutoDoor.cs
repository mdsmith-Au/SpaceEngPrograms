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
    // Author: cptmds 2016-02-10


    // User defined variables
    private const String PREFIX = "[AUTODOOR]";
    private const double triggerDist = 4;

    // Sensor range values
    // Set within in-game limits which are as of 2016-02-10 min 1 and max 50
    private const float SensorLeft = 30f;
    private const float SensorRight = 30f;
    private const float SensorTop = 1f;
    private const float SensorBottom = 50f;
    private const float SensorFront = 50f;
    private const float SensorBack = 1f;

    // Speed at which the rotor should run at
    // Faster speeds = faster door response
    private const float rotorSpeed = 30f;


    private AutoDoorProgram autodoor = null;

    void Main(string argument)
    {
        if (autodoor == null) autodoor = new AutoDoorProgram(GridTerminalSystem, Me, Echo, ElapsedTime);

        autodoor.playerDetected();
    }



    public class AutoDoorProgram
    {

        protected IMyGridTerminalSystem GridTerminalSystem;
        protected Action<string> Echo;
        protected TimeSpan ElapsedTime;
        protected IMyTerminalBlock Me;

        private List<IMySensorBlock> sensors;
        private IMyMotorStator rotor;
        List<IMyDoor> doors;

        // Assume this is triggered when sensor detects player
        public void playerDetected()
        {

            //Echo("Player detected!");

            for (int i = 0; i < doors.Count; i++)
            {
                IMyDoor door = doors[i];
                Boolean anyoneNearDoor = false;
                for (int j = 0; j < sensors.Count; j++)
                {
                    VRage.ModAPI.IMyEntity player = sensors[j].LastDetectedEntity;
                    if (player == null) continue;
                    //Echo("Checking player at " + player.GetPosition().ToString() + " against door " + door.CustomName);

                    if ((player.GetPosition() - door.GetPosition()).Length() <= triggerDist)
                    {
                        anyoneNearDoor = true;
                        break;
                    }
                }
                if (anyoneNearDoor)
                {
                    door.ApplyAction("Open_On");
                }
                else
                {
                    door.ApplyAction("Open_Off");
                }
            }
        }



        public AutoDoorProgram(IMyGridTerminalSystem grid, IMyProgrammableBlock me, Action<string> echo, TimeSpan elapsedTime)
        {
            GridTerminalSystem = grid;
            Echo = echo;
            ElapsedTime = elapsedTime;
            Me = me;

            doors = new List<IMyDoor>();
            sensors = new List<IMySensorBlock>();

            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();

            grid.SearchBlocksOfName(PREFIX, blocks);

            // Add some error handling for blocks not found

            for (int i = 0; i < blocks.Count; i++)
            {
                IMyTerminalBlock block = blocks[i];
                String blockType = block.DefinitionDisplayNameText;
                String blockName = block.CustomName;

                //Echo("Processing block " + blockName);

                if (blockType.Equals("Sensor"))
                {
                    IMySensorBlock sensor = block as IMySensorBlock;
                    sensor.ApplyAction("OnOff_On");

                    List<ITerminalProperty> properties = new List<ITerminalProperty>();
                    sensor.GetProperties(properties);

                    sensor.SetValueFloat("Back", SensorBack);
                    sensor.SetValueFloat("Bottom", SensorBottom);
                    sensor.SetValueFloat("Top", SensorTop);
                    sensor.SetValueFloat("Left", SensorLeft);
                    sensor.SetValueFloat("Right", SensorRight);
                    sensor.SetValueFloat("Front", SensorFront);
                    sensor.SetValueBool("Detect Asteroids", false);
                    sensor.SetValueBool("Detect Enemy", false);
                    sensor.SetValueBool("Detect Floating Objects", false);
                    sensor.SetValueBool("Detect Friendly", true);
                    sensor.SetValueBool("Detect Large Ships", false);
                    sensor.SetValueBool("Detect Neutral", false);
                    sensor.SetValueBool("Detect Owner", true);
                    sensor.SetValueBool("Detect Players", true);
                    sensor.SetValueBool("Detect Small Ships", false);
                    sensor.SetValueBool("Detect Stations", false);
                    sensor.SetValueBool("Audible Proximity Alert", false);
                    sensors.Add(sensor);

                }
                else if (blockType.Equals("Sliding Door") || blockType.Equals("Door"))
                {
                    IMyDoor door = block as IMyDoor;
                    door.ApplyAction("Open_Off");
                    doors.Add(door);
                }
                else if (blockType.Equals("Rotor") || blockType.Equals("Advanced Rotor"))
                {
                    rotor = block as IMyMotorStator;
                    rotor.ApplyAction("OnOff_On");
                    rotor.SetValueFloat("Torque", 3.36E+07f);
                    rotor.SetValueFloat("BrakingTorque", 3.36E+07f);
                    rotor.SetValueFloat("Velocity", rotorSpeed);
                    rotor.SetValueFloat("UpperLimit", float.PositiveInfinity);
                    rotor.SetValueFloat("LowerLimit", float.NegativeInfinity);

                    // Add config here
                }
            }


        }
    }

    // Stop copying here
}