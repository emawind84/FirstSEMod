using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using Sandbox.Game;

namespace FirstSEMod
{
    class SecuritySystem
    {
        IMyGridTerminalSystem GridTerminalSystem;

        // #### SETTINGS #### //
        string soundAlarmGroupName = "sound blocks";
        string lightAlarmGroupName = "alert lights";
        string logGroupName = "warning logger";
        // Name of the block that trigger the protection system if turned on
        string alarmBlockSwitch = "Beacon";

        // #### PRIVATE #### //
        // do not edit these variables
        List<IMyTerminalBlock> hackedBlocks = new List<IMyTerminalBlock>();
        bool thrustersFixApplied = false;
        int blockCount = 0;
        bool blocksMissing = false;

        void Main()
        {
            Log("clear");
            Log("System online");

            DateTime now = DateTime.Now;
            Log("Time: " + now.ToString("HH:mm:ss"));

            var beacon = GridTerminalSystem.GetBlockWithName(alarmBlockSwitch);
            if (beacon != null && (beacon as IMyFunctionalBlock).Enabled)
            {
                Log("Alarm on");
                CloseDoors();
                RunSecuritySystem();
            }
            else
            {
                blockCount = 0;
            }
            RunProximitySensorCheck();
            RunPowerStatusCheck();


            // #### TEST ####       
            //DisableThrustersControl();       
            //SwitchAlarm(true);       
            //CloseDoors();  
            //DisableThrusters(); 
            //ResetOverrideThrusters();    
            //EnableAntenna();
            //EnableTurrets();
        }

        void RunSecuritySystem()
        {
            List<IMyTerminalBlock> allBlocks = GridTerminalSystem.Blocks;

            if (blockCount != 0 && blockCount != allBlocks.Count)
            {
                blocksMissing = true;
            }
            else
            {
                blockCount = allBlocks.Count;
            }

            var check = true;

            for (int i = 0; i < allBlocks.Count; i++)
            {
                // check if hacked
                var block = (IMyCubeBlock)allBlocks[i];
                if (block.IsBeingHacked)
                {
                    if (!hackedBlocks.Contains((IMyTerminalBlock)block))
                    {
                        hackedBlocks.Add((IMyTerminalBlock)block);
                    }
                    check = false;
                    Log("Hacking detected: " + (block as IMyTerminalBlock).CustomName);
                    break;
                }

                // check if functional       
                if (!block.IsFunctional)
                {
                    check = false;
                    Log("Damage detected: " + (block as IMyTerminalBlock).CustomName);
                    break;
                }
            }

            if (hackedBlocks.Count > 0 || blocksMissing)
            {
                check = false;
                TakeSecurityMeasure();
                Log("Hacking detected or block missing");
            }

            // sound the alarm     
            if (check)
            {
                // everything is ok     
                SwitchAlarm(false);
                Log("No breach or damage detected");
            }
            else
            {
                // something went wrong     
                SwitchAlarm(true);
                EnableAntenna();
            }
        }

        void RunProximitySensorCheck()
        {
            bool detected = false;
            List<IMyTerminalBlock> sensors = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors);

            for (int i = 0; i < sensors.Count; i++)
            {
                if (((IMySensorBlock)sensors[i]).LastDetectedEntity != null)
                {
                    detected = true;
                    Log(sensors[i].CustomName + ": Entity detected");
                    break;
                }
            }

            IMyTerminalBlock light = GridTerminalSystem.GetBlockWithName("Alert Light");
            if (!detected)
            {
                Log("No entity detected!");
                SwitchAlarm(false);
            }
            else
            {
                //Log("entity detected!");
                SwitchAlarm();
            }

        }

        void TakeSecurityMeasure()
        {
            CloseDoors();
            DisableThrustersControl();
            DisableThrusters();
            ResetOverrideThrusters();
            EnableTurrets();
        }

        void EnableTurrets()
        {
            List<IMyTerminalBlock> turrets = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(turrets);

            for (int i = 0; i < turrets.Count; i++)
            {
                ITerminalAction switchOn = turrets[i].GetActionWithName("OnOff_On");
                switchOn.Apply(turrets[i]);
            }
            Log("Turrets enabled");
        }

        void DisableThrustersControl()
        {
            /* 
            for (int i = 0; i < hackedBlocks.Count; i++ )
            { 
                if( hackedBlocks[i] is IMyShipController ){ 
                    DisableThrustersControl( hackedBlocks[i] ); 
                } 
            } 
            */

            List<IMyTerminalBlock> controls = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(controls);

            for (int i = 0; i < controls.Count; i++)
            {
                DisableThrustersControl(controls[i]);
            }
            Log("Thrusters control disabled");
        }

        void DisableThrusters()
        {
            List<IMyTerminalBlock> thrusters = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);

            for (int i = 0; i < thrusters.Count; i++)
            {
                ITerminalAction switchOff = thrusters[i].GetActionWithName("OnOff_Off");
                switchOff.Apply(thrusters[i]);
            }
            Log("Thrusters disabled");
        }

        void DisableThrustersControl(IMyTerminalBlock controller)
        {
            ITerminalAction controlThrustersAction = controller.GetActionWithName("ControlThrusters");
            ITerminalAction dampenersOverrideAction = controller.GetActionWithName("DampenersOverride");

            if (!thrustersFixApplied)
            {
                controlThrustersAction.Apply(controller);
                thrustersFixApplied = true;
            }

            if (((IMyShipController)controller).ControlThrusters == true)
            {
                // disable thrusters        
                controlThrustersAction.Apply(controller);
            }

            if (((IMyShipController)controller).DampenersOverride == false)
            {
                // enable dampeners override        
                dampenersOverrideAction.Apply(controller);
            }

        }

        void EnableAntenna()
        {
            EnableAntenna(null);
        }

        void EnableAntenna(IMyRadioAntenna antenna)
        {
            if (antenna != null)
            {
                ITerminalAction action = antenna.GetActionWithName("OnOff_On");
                action.Apply(antenna);
                //antenna.SetValueFloat("Radius", 50000); 
            }
            else
            {
                List<IMyTerminalBlock> antennas = new List<IMyTerminalBlock>();
                GridTerminalSystem.GetBlocksOfType<IMyRadioAntenna>(antennas);

                for (int i = 0; i < antennas.Count; i++)
                {
                    ITerminalAction action = antennas[i].GetActionWithName("OnOff_On");
                    action.Apply(antennas[i]);
                    //antennas[i].SetValueFloat("Radius", 50000); 
                }
            }
        }

        void SwitchAlarm(bool swh = true)
        {
            SwitchSoundAlarm(swh);
            SwitchLightAlarm(swh);
        }

        void SwitchSoundAlarm(bool swh)
        {
            List<IMyBlockGroup> alarmsGrps = new List<IMyBlockGroup>();
            SearchGroupsOfName(soundAlarmGroupName, alarmsGrps);

            for (int i = 0; i < alarmsGrps.Count; i++)
            {
                var blocks = alarmsGrps[i].Blocks;
                for (int y = 0; y < blocks.Count; y++)
                {
                    //get the action to play the sound bolck          
                    ITerminalAction playalarms = blocks[y].GetActionWithName("PlaySound");
                    ITerminalAction stopalarms = blocks[y].GetActionWithName("StopSound");

                    if (swh)
                    {
                        //Sound the alarm         
                        stopalarms.Apply(blocks[y]);
                        playalarms.Apply(blocks[y]);
                    }
                    else
                    {
                        stopalarms.Apply(blocks[y]);
                    }
                }
            }
        }

        void SwitchLightAlarm(bool swh)
        {
            List<IMyBlockGroup> alarmsGrps = new List<IMyBlockGroup>();
            SearchGroupsOfName(lightAlarmGroupName, alarmsGrps);

            for (int i = 0; i < alarmsGrps.Count; i++)
            {
                var blocks = alarmsGrps[i].Blocks;
                for (int y = 0; y < blocks.Count; y++)
                {
                    if (swh)
                    {
                        ITerminalAction switchOn = blocks[y].GetActionWithName("OnOff_On");
                        switchOn.Apply(blocks[y]);
                    }
                    else
                    {
                        ITerminalAction switchOff = blocks[y].GetActionWithName("OnOff_Off");
                        switchOff.Apply(blocks[y]);
                    }
                }
            }
        }

        void Log(string content)
        {
            List<IMyBlockGroup> loggers = new List<IMyBlockGroup>();
            SearchGroupsOfName(logGroupName, loggers);
            var cnt = 1;
            for (int i = 0; i < loggers.Count; i++)
            {
                var blocks = loggers[i].Blocks;
                for (int y = 0; y < blocks.Count; y++)
                {
                    if (blocks[y] is IMyTerminalBlock)
                    {
                        if (content.Equals("clear"))
                        {
                            Log(blocks[y], "");
                        }
                        else
                        {
                            Log(blocks[y], content + "\n", true);
                        }
                    }
                    cnt++;
                    //break;
                }
            }
        }

        void Log(IMyTerminalBlock block, string content, bool append = false)
        {
            if (block is IMyTextPanel)
            {
                ((IMyTextPanel)block).WritePublicText(content, append);
                ((IMyTextPanel)block).ShowTextureOnScreen();
                ((IMyTextPanel)block).ShowPublicTextOnScreen();
            }
            else
            {
                if (append)
                {
                    block.SetCustomName(block.CustomName + content);
                }
                else
                {
                    block.SetCustomName(content);
                }
            }
        }

        void SearchGroupsOfName(string name, List<IMyBlockGroup> groups)
        {
            if (groups == null) return;
            // using String.Empty crash the server    
            //if( name == null || name == String.Empty ) return;      
            if (name == null || name == "") return;

            List<IMyBlockGroup> allGroups = GridTerminalSystem.BlockGroups;

            for (int i = 0; i < allGroups.Count; i++)
            {
                if (allGroups[i].Name.Contains(name))
                {
                    groups.Add(allGroups[i]);
                }
            }
        }

        void ResetOverrideThrusters()
        {
            List<IMyTerminalBlock> thrusters = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);

            for (int i = 0; i < thrusters.Count; i++)
            {
                for (int y = 0; y < 10; y++)
                {
                    ITerminalAction action = thrusters[i].GetActionWithName("DecreaseOverride");
                    action.Apply(thrusters[i]);
                }
            }
            Log("Override thrusters reset");
        }

        void CloseDoors()
        {
            List<IMyTerminalBlock> doorBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyDoor>(doorBlocks);

            int doorCount = doorBlocks.Count;

            for (int i = 0; i < doorCount; i++)
            {
                ITerminalAction closeDoors = doorBlocks[i].GetActionWithName("Open_Off");
                closeDoors.Apply(doorBlocks[i]);
            }
            Log("All doors closed");
        }

        string GetDetailedInfoValue(IMyTerminalBlock block, string name)
        {
            string value = "";
            string[] lines = block.DetailedInfo.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string[] line = lines[i].Split(':');
                if (line[0].Equals(name))
                {
                    value = line[1].Substring(1);
                    break;
                }
            }
            return value;
        }

        int GetPowerAsInt(string text)
        {
            if (String.IsNullOrWhiteSpace(text))
            {
                return 0;
            }
            string[] values = text.Split(' ');
            if (values[1].Equals("kW"))
            {
                return (int)(float.Parse(values[0]) * 1000f);
            }
            else if (values[1].Equals("kWh"))
            {
                return (int)(float.Parse(values[0]) * 1000f);
            }
            else if (values[1].Equals("MW"))
            {
                return (int)(float.Parse(values[0]) * 1000000f);
            }
            else if (values[1].Equals("MWh"))
            {
                return (int)(float.Parse(values[0]) * 1000000f);
            }
            else
            {
                return (int)float.Parse(values[0]);
            }
            return 0;
        }

        void RunPowerStatusCheck()
        {
            List<IMyTerminalBlock> allPower = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> tmp = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(tmp);
            allPower.AddRange(tmp);
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(tmp);
            allPower.AddRange(tmp);
            GridTerminalSystem.GetBlocksOfType<IMyReactor>(tmp);
            allPower.AddRange(tmp);

            var freePower = 0;
            var usedOutput = 0;
            var maxOutput = 0;
            for (var i = 0; i < allPower.Count; i++)
            {
                //Log( GetDetailedInfoValue( solarBlocks[i], "Current Output" ) );  
                usedOutput += GetPowerAsInt(GetDetailedInfoValue(allPower[i], "Current Output"));
                maxOutput += GetPowerAsInt(GetDetailedInfoValue(allPower[i], "Max Output"));
            }

            //return maxOutput - usedOutput;  
            Log("Power output: " + usedOutput);
            Log("Power available: " + (maxOutput - usedOutput));
        }  
    }
}
