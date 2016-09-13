using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRageMath;

class Hangar2 : MyGridProgram
{

    // Copy starting here and ignore last } - programming block does not need above class def'n

    // Version 1.0
    // Author: cptmds 2016-02-08
    public struct requestTicket
    {
        public int groupNum;
        public String action;
        public Boolean timed;
        public int timeRemainingInSeconds;

        public requestTicket(int group, String act, Boolean delayed = false, int time = 0)
        {
            groupNum = group;
            action = act;
            timeRemainingInSeconds = time;
            timed = delayed;
        }
    }

    private HangarController hang = null;

    void Main(string argument)
    {
        if (hang == null) hang = new HangarController(GridTerminalSystem, Me, Echo, ElapsedTime);

        if (argument.Length > 0)
        {
            int groupNumber = hang.groups.IndexOf(argument);
            if (groupNumber != -1) hang.requests.Add(new requestTicket(groupNumber, "toggle"));
            else Echo("Invalid argument! Ignoring.");
        }
        hang.mainLoop();
    }



    public class HangarController
    {
        // User defined variables

        // List of group names; i.e. if you have more than one hangar, put the doors/lights/sound blocks/air vents from each into a separate group
        public List<string> groups = new List<string> { "Hangar", "Garage", "Workshop" };

        // How long to wait before closing or opening when a button is pushed
        private const int numberSecondsBeforeOpen = 10;
        private const int numberSecondsBeforeClose = 5;


        // Don't touch anything after this line unless you actually know C#
        private List<IMyDoor>[] hangarDoors;
        private List<IMyDoor>[] interiorDoors;
        private List<IMyDoor>[] exteriorDoors;
        private List<IMySoundBlock>[] soundBlocks;
        private List<IMyInteriorLight>[] warningLights;
        private List<IMyAirVent>[] airVents;

        // Which hangars are open
        private Boolean[] hangarOpen;

        protected IMyGridTerminalSystem GridTerminalSystem;
        protected Action<string> Echo;
        protected TimeSpan ElapsedTime;
        protected IMyTerminalBlock Me;

        // List of all pending tasks
        public List<requestTicket> requests = new List<requestTicket>();

        public HangarController(IMyGridTerminalSystem grid, IMyProgrammableBlock me, Action<string> echo, TimeSpan elapsedTime)
        {
            GridTerminalSystem = grid;
            Echo = echo;
            ElapsedTime = elapsedTime;
            Me = me;

            hangarDoors = new List<IMyDoor>[groups.Count];
            interiorDoors = new List<IMyDoor>[groups.Count];
            exteriorDoors = new List<IMyDoor>[groups.Count];
            soundBlocks = new List<IMySoundBlock>[groups.Count];
            warningLights = new List<IMyInteriorLight>[groups.Count];
            airVents = new List<IMyAirVent>[groups.Count];

            hangarOpen = new Boolean[groups.Count];

            // Get list of groups on this station/ship
            List<IMyBlockGroup> BlockGroups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(BlockGroups);

            // Search all groups that exist for the groups with name as specified in groups list
            for (int i = 0; i < BlockGroups.Count; i++)
            {
                int pos = groups.IndexOf(BlockGroups[i].Name);
                // If name is one of our candidates...
                if (pos != -1)
                {
                    List<IMyTerminalBlock> blocks = BlockGroups[i].Blocks;

                    // Define list of blocks for each group
                    List<IMyDoor> hangarDoorList = new List<IMyDoor>();
                    List<IMyDoor> interiorDoorList = new List<IMyDoor>();
                    List<IMyDoor> exteriorDoorList = new List<IMyDoor>();
                    List<IMySoundBlock> soundBlockList = new List<IMySoundBlock>();
                    List<IMyInteriorLight> warningLightList = new List<IMyInteriorLight>();
                    List<IMyAirVent> airVentList = new List<IMyAirVent>();

                    // Go through all blocks and add to appropriate list
                    // Also initialize to a sane known state e.g. closed, on...
                    for (int j = 0; j < blocks.Count; j++)
                    {
                        IMyTerminalBlock block = blocks[j];
                        String blockType = block.DefinitionDisplayNameText;
                        String blockName = block.CustomName;
                        block.ApplyAction("OnOff_On");

                        if (blockType.Equals("Airtight Hangar Door"))
                        {
                            IMyDoor item = block as IMyDoor;
                            item.ApplyAction("Open_Off");
                            hangarDoorList.Add(item);
                        }
                        else if ((blockType.Equals("Sliding Door") || blockType.Equals("Door")) && blockName.Contains("Interior"))
                        {
                            IMyDoor item = block as IMyDoor;
                            item.ApplyAction("Open_Off");
                            interiorDoorList.Add(item);
                        }
                        else if ((blockType.Equals("Sliding Door") || blockType.Equals("Door")) && blockName.Contains("Exterior"))
                        {
                            IMyDoor item = block as IMyDoor;
                            item.ApplyAction("Open_Off");
                            exteriorDoorList.Add(item);
                        }
                        else if (blockType.Equals("Sound Block"))
                        {
                            IMySoundBlock item = block as IMySoundBlock;
                            item.ApplyAction("StopSound");
                            item.SetValueFloat("LoopableSlider", 10);
                            soundBlockList.Add(item);
                        }
                        else if (blockType.Equals("Interior Light"))
                        {
                            IMyInteriorLight item = block as IMyInteriorLight;
                            item.ApplyAction("OnOff_Off");
                            item.SetValueFloat("Blink Interval", 1);
                            item.SetValueFloat("Blink Lenght", 50);
                            item.SetValueFloat("Blink Offset", 0);
                            item.SetValue<Color>("Color", Color.Red);
                            warningLightList.Add(item);
                        }
                        else if (blockType.Contains("Air Vent"))
                        {
                            IMyAirVent item = block as IMyAirVent;
                            item.ApplyAction("Depressurize_Off");
                            airVentList.Add(item);
                        }
                    }

                    // Some cleanup
                    hangarDoorList.TrimExcess();
                    interiorDoorList.TrimExcess();
                    exteriorDoorList.TrimExcess();
                    soundBlockList.TrimExcess();
                    warningLightList.TrimExcess();
                    airVentList.TrimExcess();

                    if (hangarDoorList.Count == 0)
                    {
                        Echo("Warning: no hangar doors detected for " + BlockGroups[i].Name);
                    }
                    else if (interiorDoorList.Count == 0)
                    {
                        Echo("Warning: no interior doors detected for " + BlockGroups[i].Name);
                    }
                    else if (soundBlockList.Count == 0)
                    {
                        Echo("Warning: no sound blocks detected for " + BlockGroups[i].Name);
                    }
                    else if (warningLightList.Count == 0)
                    {
                        Echo("Warning: no warning lights detected for " + BlockGroups[i].Name);
                    }
                    else if (airVentList.Count == 0)
                    {
                        Echo("Warning: no air vents detected for " + BlockGroups[i].Name);
                    }

                    // Now that we have populated lists add them to the correct position in the group list

                    hangarDoors[pos] = hangarDoorList;
                    interiorDoors[pos] = interiorDoorList;
                    exteriorDoors[pos] = exteriorDoorList;
                    soundBlocks[pos] = soundBlockList;
                    warningLights[pos] = warningLightList;
                    airVents[pos] = airVentList;
                    hangarOpen[pos] = false;

                    // Exterior doors have been requested to close so we set a check to lock them when they are in fact closed
                    requests.Add(new requestTicket(pos, "lockExteriorDoors"));
                }

            }
        }

        // Process all requests - main thread (think while(true) in a microprocessor)
        // Note this assume a 1Hz clock cycle from a timer block
        public void mainLoop()
        {
            Echo("Hangar status:");
            for (int i = 0; i < hangarOpen.Length; i++)
            {
                Echo("[" + groups[i] + "]: " + (hangarOpen[i] ? "Open" : "Closed"));
            }
            // Go through backwards so we can remove stuff without screwing our loop up
            // Note we must also use RemoveAt() for the same reason
            // Luckily this isn't Java!
            for (int i = requests.Count - 1; i >= 0; i--)
            {
                requestTicket req = requests[i];
                int groupNo = req.groupNum;

                // For stuff that must be processed immediately
                if (!req.timed)
                {
                    switch (req.action)
                    {
                        case "toggle":
                            {
                                // Case where hangar is depressurised: repressurize
                                if (hangarOpen[groupNo]) goto case "close";
                                else goto case "open";
                            }
                            // Start open procedure
                        case "open":
                            {
                                // Flash lights, play warning sound
                                foreach (IMyInteriorLight light in warningLights[groupNo]) light.ApplyAction("OnOff_On");
                                foreach (IMySoundBlock sound in soundBlocks[groupNo])
                                {
                                    sound.SetValueFloat("LoopableSlider", numberSecondsBeforeOpen + 2);
                                    sound.ApplyAction("PlaySound");
                                }

                                requests.RemoveAt(i);

                                //Make sure interior doors are on so we can close them
                                foreach (IMyDoor door in interiorDoors[groupNo]) door.ApplyAction("OnOff_On");

                                // Lock the interior doors after some time
                                requests.Add(new requestTicket(groupNo, "lockInterior_delay", true, numberSecondsBeforeOpen - 1));
                                break;
                            }
                            // Start close procedure
                        case "close":
                            {
                                // Make sure lights are on (they should still be but just in case)
                                foreach (IMyInteriorLight light in warningLights[groupNo]) light.ApplyAction("OnOff_On");
                                // Play sound
                                foreach (IMySoundBlock sound in soundBlocks[groupNo])
                                {
                                    sound.SetValueFloat("LoopableSlider", numberSecondsBeforeClose);
                                    sound.ApplyAction("PlaySound");
                                }

                                requests.RemoveAt(i);

                                //Make sure all outside doors have power so we can close them
                                foreach (IMyDoor door in hangarDoors[groupNo]) door.ApplyAction("OnOff_On");
                                foreach (IMyDoor door in exteriorDoors[groupNo]) door.ApplyAction("OnOff_On");

                                // Schedule close request
                                requests.Add(new requestTicket(groupNo, "close_delay", true, numberSecondsBeforeClose));
                                break;
                            }
                            // Check pressurization and unlock interior doors/turn off emer. lights if OK
                        case "checkPressurize":
                            {
                                // Get first airvent; they should all be in the room and hence have the same pressure
                                IMyAirVent airvent = airVents[groupNo][0];
                                if (airvent.GetOxygenLevel() > 0.99)
                                {
                                    // Ok we're pressurized, turn lights off and enable interior doors
                                    foreach (IMyInteriorLight light in warningLights[groupNo]) light.ApplyAction("OnOff_Off");
                                    foreach (IMyDoor door in interiorDoors[groupNo]) door.ApplyAction("OnOff_On");
                                    hangarOpen[groupNo] = false;

                                    requests.RemoveAt(i);
                                }
                                break;
                            }
                        // Lock all exterior doors
                        case "lockExteriorDoors":
                            {
                                // Check door status
                                Boolean doorClosed = true;
                                foreach (IMyDoor door in exteriorDoors[groupNo])
                                {
                                    // If door is still closing or open...
                                    if (door.OpenRatio != 0)
                                    {
                                        door.ApplyAction("Open_Off");
                                        doorClosed = false;
                                    }
                                    else
                                    {
                                        door.ApplyAction("OnOff_Off");
                                    }
                                }
                                if (doorClosed)
                                {
                                    requests.RemoveAt(i);
                                }
                                break;
                            }
                            // Lock interior doors
                        case "lockInterior":
                            {
                                // Check door status
                                Boolean doorClosed = true;
                                foreach (IMyDoor door in interiorDoors[groupNo])
                                {
                                    // If door is still closing or open...
                                    if (door.OpenRatio != 0)
                                    {
                                        door.ApplyAction("Open_Off");
                                        doorClosed = false;
                                    }
                                    else
                                    {
                                        door.ApplyAction("OnOff_Off");
                                    }
                                }
                                if (doorClosed)
                                {
                                    requests.RemoveAt(i);
                                    // Doors now locked, move onto opening main doors
                                    requests.Add(new requestTicket(groupNo, "open_delay"));
                                }
                                break;
                            }
                        case "open_delay":
                            {
                                // Make sure all outside doors turn on
                                foreach (IMyDoor door in exteriorDoors[groupNo])
                                {
                                    door.ApplyAction("OnOff_On");
                                }
                                foreach (IMyDoor door in hangarDoors[groupNo])
                                {
                                    door.ApplyAction("OnOff_On");
                                }
                                
                                requests.RemoveAt(i);
                                // Queue new request to open doors soon
                                // This is because we can't turn on and open a door at the same time - game ignore the open request
                                requests.Add(new requestTicket(groupNo, "open_delay2"));
                                break;
                            }
                        case "open_delay2":
                            {
                                // Actually open all outside doors now
                                foreach (IMyDoor door in exteriorDoors[groupNo])
                                {
                                    door.ApplyAction("Open_On");
                                }
                                foreach (IMyDoor door in hangarDoors[groupNo])
                                {
                                    door.ApplyAction("Open_On");
                                }
                                hangarOpen[groupNo] = true;
                                requests.RemoveAt(i);
                                break;
                            }

                    }
                }
                // Deal with delayed stuff
                else
                {
                    int timeLeft = req.timeRemainingInSeconds;
                    // If it's not time yet just decrement the counter
                    if (timeLeft > 0)
                    {
                        // Replace request with new one with one less second
                        requests[i] = new requestTicket(groupNo, req.action, true, timeLeft - 1);
                    }
                    else
                    {
                        switch (req.action)
                        {
                            // Close outside doors
                            case "close_delay":
                                {
                                    foreach (IMyDoor door in exteriorDoors[groupNo])
                                    {
                                        door.ApplyAction("Open_Off");
                                    }
                                    foreach (IMyDoor door in hangarDoors[groupNo])
                                    {
                                        door.ApplyAction("Open_Off");
                                    }

                                    requests.RemoveAt(i);
                                    // Set events to unlock inside door when pressurized and make sure exterior doors are closed so we can actually pressurize
                                    requests.Add(new requestTicket(groupNo, "checkPressurize"));
                                    requests.Add(new requestTicket(groupNo, "lockExteriorDoors"));
                                    break;
                                }
                                // Lock interior doors - or at least schedule the procedure
                            case "lockInterior_delay":

                                {
                                    requests.RemoveAt(i);
                                    requests.Add(new requestTicket(groupNo, "lockInterior"));
                                    break;
                                }
                        }
                    }

                }
            }
        }
    }

    // Stop copying here
}