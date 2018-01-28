using System;
using System.Collections.Generic;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;

namespace FirstSEMod
{
    /// <summary>
    /// SE Security System v1.1
    /// </summary>
    class Program : MyGridProgram
    {
        // #### SETTINGS #### //
        public static string SOUND_ALERT_TAG = "[SS-ALARM]";
        public static string LIGHT_ALERT_TAG = "[SS-ALARM]";

        // these blocks will receive the log (if not LCD the name will be used for logging)
        public static string LOG_BLOCK_TAG = "[SS-LOG]";

        // this block turn on/off the security system
        public static string ALARM_ON_SWITCH_TAG = "[SS-SWITCH]";

        // this light turn on if the security system is on
        public static string ALARM_ON_STATUS_TAG = "[SS-LIGHT]";

        // the number of lcd to update per run
        public static int LOG_GRID_PER_STEP = 1;

        // #### PRIVATE #### //
        // do not edit these variables
        List<IMyTerminalBlock> hackedBlocks = new List<IMyTerminalBlock>();
        List<IMyTerminalBlock> loggers = new List<IMyTerminalBlock>();
        public bool thrustersFixApplied = false;
        public int blockCount = 0;
        public bool blocksMissing = false;
        public static int step = 0;

        public Program()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Update100;
        }

        public void Main()
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(LOG_BLOCK_TAG, blocks);
            //SearchGroupsOfName(LOG_BLOCK_GROUP, _grp);
            //GridTerminalSystem.SearchBlocksOfName(LOG_BLOCK_TAG, loggers);
            List<IMyTerminalBlock> _loggers = blocks;
            for (int i = 0; i < LOG_GRID_PER_STEP; i++)
            {
                IMyTerminalBlock _logger = null;
                if ( _loggers.Count > 0 )
                {
                    _logger = _loggers[(step * LOG_GRID_PER_STEP + i) % _loggers.Count];
                }
                Log(_logger, "clear");
                Log(_logger, "System online");

                DateTime now = DateTime.Now;
                Log(_logger, "Time: " + now.ToString("HH:mm:ss"));

                List<IMyTerminalBlock> _t = new List<IMyTerminalBlock>();
                GridTerminalSystem.SearchBlocksOfName(ALARM_ON_SWITCH_TAG, _t);
                if (_t.Count > 0 && (_t[0] as IMyFunctionalBlock).Enabled)
                {
                    Log(_logger, "Alarm on");
                    ToggleAlarmLight(_logger);
                    RunSecuritySystem(_logger);
                }
                else
                {
                    Log(_logger, "Alarm off");
                    ToggleAlarmLight(_logger, false);
                    blockCount = 0;
                }
                //RunProximitySensorCheck(_logger);
                //RunPowerStatusCheck(_logger);

                // #### TEST ####       
                //DisableThrustersControl();       
                //SwitchAlarm(true);       
                //CloseDoors();  
                //DisableThrusters(); 
                //ResetOverrideThrusters();    
                //EnableAntenna();
                //EnableTurrets();

                // increment step for logging on text panel
                if(_logger is IMyTextPanel)
                    MMLCDTextManager.UpdatePanel(_logger as IMyTextPanel);
            }
            
            step++;
        }

        void RunSecuritySystem(IMyTerminalBlock log)
        {
            List<IMyTerminalBlock> allBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocks(allBlocks);

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
                var block = allBlocks[i];
                if (block.IsBeingHacked || ( block.OwnerId != 0 && Me.OwnerId != block.OwnerId ) )
                {
                    check = false;
                    if (!hackedBlocks.Contains((IMyTerminalBlock)block))
                    {
                        hackedBlocks.Add((IMyTerminalBlock)block);
                    }
                    Log(log, "Hacking detected: " + (block as IMyTerminalBlock).CustomName);
                    break;
                }

                // check if functional       
                if (!block.IsFunctional)
                {
                    check = false;
                    Log(log, "Damage detected: " + (block as IMyTerminalBlock).CustomName);
                    break;
                }
            }

            check &= RunProximitySensorCheck(log);

            //if (hackedBlocks.Count > 0 || blocksMissing)
            //{
            //    check = false;
            //    TakeSecurityMeasure(log);
            //    Log(log, "Hacking detected or block missing");
            //}

            // sound the alarm     
            if (check)
            {
                // everything is ok     
                SwitchAlarm(false, log);
                Log(log, "No breach or damage detected");
            }
            else
            {
                // something went wrong     
                TakeSecurityMeasure(log);
                SwitchAlarm(true, log);
                EnableAntenna();
            }
        }

        bool RunProximitySensorCheck(IMyTerminalBlock log)
        {
            bool detected = false;
            List<IMyTerminalBlock> sensors = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMySensorBlock>(sensors);

            for (int i = 0; i < sensors.Count; i++)
            {
                if ( !((IMySensorBlock)sensors[i]).LastDetectedEntity.IsEmpty() )
                {
                    detected = true;
                    Log(log, sensors[i].CustomName + ": Entity detected");
                    break;
                }
            }

            IMyTerminalBlock light = GridTerminalSystem.GetBlockWithName("Alert Light");
            if (detected)
            {
                return false;
            }
            Log(log, "No entity detected!");
            return true;
        }

        void TakeSecurityMeasure(IMyTerminalBlock log)
        {
            CloseDoors(log);
            DisableThrustersControl(log);
            DisableThrusters(log);
            ResetOverrideThrusters(log);
            EnableTurrets(log);
        }

        void EnableTurrets(IMyTerminalBlock log=null)
        {
            List<IMyTerminalBlock> turrets = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyLargeTurretBase>(turrets);

            for (int i = 0; i < turrets.Count; i++)
            {
                ITerminalAction switchOn = turrets[i].GetActionWithName("OnOff_On");
                switchOn.Apply(turrets[i]);
            }
            Log(log, "Turrets enabled");
        }

        void DisableThrustersControl(IMyTerminalBlock log=null)
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
                var controller = controls[i];
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

                //if (((IMyShipController)controller).DampenersOverride == false)
                //{
                //    // enable dampeners override        
                //    dampenersOverrideAction.Apply(controller);
                //}
            }
            Log(log, "Thrusters control disabled");
        }

        void DisableThrusters(IMyTerminalBlock log=null)
        {
            List<IMyTerminalBlock> thrusters = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(thrusters);

            for (int i = 0; i < thrusters.Count; i++)
            {
                ITerminalAction switchOff = thrusters[i].GetActionWithName("OnOff_Off");
                switchOff.Apply(thrusters[i]);
            }
            Log(log, "Thrusters disabled");
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

        void SwitchAlarm(bool swh=true, IMyTerminalBlock log=null)
        {
            SwitchSoundAlarm(swh, log);
            SwitchLightAlarm(swh, log);
        }

        void SwitchSoundAlarm(bool swh, IMyTerminalBlock log = null)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(SOUND_ALERT_TAG, blocks);

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is IMySoundBlock)
                {
                    //get the action to play the sound bolck          
                    ITerminalAction playalarms = blocks[i].GetActionWithName("PlaySound");
                    ITerminalAction stopalarms = blocks[i].GetActionWithName("StopSound");

                    if (swh)
                    {
                        //Log(log, "sound alarm on");
                        //Sound the alarm         
                        stopalarms.Apply(blocks[i]);
                        playalarms.Apply(blocks[i]);
                    }
                    else
                    {
                        //Log(log, "sound alarm off");
                        stopalarms.Apply(blocks[i]);
                    }
                }
            }
        }

        void SwitchLightAlarm(bool swh, IMyTerminalBlock log = null)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(LIGHT_ALERT_TAG, blocks);

            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] is IMyLightingBlock)
                {
                    if (swh)
                    {
                        //Log(log, "light alarm on");
                        ITerminalAction switchOn = blocks[i].GetActionWithName("OnOff_On");
                        switchOn.Apply(blocks[i]);
                    }
                    else
                    {
                        //Log(log, "light alarm off");
                        ITerminalAction switchOff = blocks[i].GetActionWithName("OnOff_Off");
                        switchOff.Apply(blocks[i]);
                    }
                }
            }
        }

        void ToggleAlarmLight(IMyTerminalBlock log, bool swh = true)
        {
            var blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.SearchBlocksOfName(ALARM_ON_STATUS_TAG, blocks);

            for (int i = 0; i < blocks.Count; i++) {
                if (blocks[i] is IMyLightingBlock) {
                    var bl = blocks[0];
                    bl.SetValueFloat("Blink Interval", 1F);
                    bl.SetValueFloat("Blink Lenght", 10F);
                    //Log(log, "Blink Interval: " + bl.GetValueFloat("Blink Interval"));
                    //Log(log, "Blink Lenght: " + bl.GetValueFloat("Blink Lenght"));
                    if (swh)
                    {
                        //Log(log, "alarm lights on");
                        ITerminalAction switchOn = bl.GetActionWithName("OnOff_On");
                        switchOn.Apply(bl);
                    }
                    else
                    {
                        //Log(log, "alarm lights off");
                        ITerminalAction switchOff = bl.GetActionWithName("OnOff_Off");
                        switchOff.Apply(bl);
                    }
                }
            }
        }

        void Log(IMyTerminalBlock log, string content, bool append = true)
        {
            if (log is IMyTextPanel)
            {
                if (content.Equals("clear"))
                    MMLCDTextManager.ClearText(log as IMyTextPanel);
                else
                {
                    if (!append)
                        MMLCDTextManager.ClearText(log as IMyTextPanel);
                    MMLCDTextManager.AddLine(log as IMyTextPanel, content);
                }
                //((IMyTextPanel)block).WritePublicText(content, append);
                //((IMyTextPanel)block).ShowTextureOnScreen();
                //((IMyTextPanel)block).ShowPublicTextOnScreen();
            }
            else if (log != null)
            {
                if (content.Equals("clear"))
                    log.CustomName = "";
                else if (append)
                    log.CustomName = log.CustomName + content + "\n";
                else
                    log.CustomName = content + "\n";
            }
        }

        void SearchGroupsOfName(string name, List<IMyBlockGroup> groups)
        {
            if (groups == null)
                return;
            // using String.Empty crash the server    
            //if( name == null || name == String.Empty ) return;      
            if (name == null || name == "") return;

            List<IMyBlockGroup> allGroups = new List<IMyBlockGroup>();
            GridTerminalSystem.GetBlockGroups(allGroups);

            for (int i = 0; i < allGroups.Count; i++)
            {
                if (allGroups[i].Name.Contains(name))
                {
                    groups.Add(allGroups[i]);
                }
            }
        }

        void ResetOverrideThrusters(IMyTerminalBlock log)
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
            Log(log, "Override thrusters reset");
        }

        void CloseDoors(IMyTerminalBlock log)
        {
            List<IMyTerminalBlock> doorBlocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMyDoor>(doorBlocks);

            int doorCount = doorBlocks.Count;

            for (int i = 0; i < doorCount; i++)
            {
                ITerminalAction closeDoors = doorBlocks[i].GetActionWithName("Open_Off");
                closeDoors.Apply(doorBlocks[i]);
            }
            Log(log, "All doors closed");
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
            //return 0;
        }

        void RunPowerStatusCheck(IMyTerminalBlock log)
        {
            List<IMyTerminalBlock> allPower = new List<IMyTerminalBlock>();
            List<IMyTerminalBlock> tmp = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(tmp);
            allPower.AddRange(tmp);
            GridTerminalSystem.GetBlocksOfType<IMyBatteryBlock>(tmp);
            allPower.AddRange(tmp);
            GridTerminalSystem.GetBlocksOfType<IMyReactor>(tmp);
            allPower.AddRange(tmp);

            //var freePower = 0;
            var usedOutput = 0;
            var maxOutput = 0;
            for (var i = 0; i < allPower.Count; i++)
            {
                if (allPower[i] is IMyFunctionalBlock && !(allPower[i] as IMyFunctionalBlock).Enabled)
                {
                    continue;
                }
                //Log( GetDetailedInfoValue( solarBlocks[i], "Current Output" ) );  
                usedOutput += GetPowerAsInt(GetDetailedInfoValue(allPower[i], "Current Output"));
                maxOutput += GetPowerAsInt(GetDetailedInfoValue(allPower[i], "Max Output"));
            }

            //return maxOutput - usedOutput;  
            Log(log, "Power output: " + usedOutput);
            Log(log, "Power available: " + (maxOutput - usedOutput));
        }

        void RunEmergencyPowerCheck(IMyTerminalBlock log)
        {
            List<IMyTerminalBlock> solars = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType<IMySolarPanel>(solars);

            var usedOutput = 0;
            var maxOutput = 0;
            for (var i = 0; i < solars.Count; i++)
            {
                //Log( GetDetailedInfoValue( solarBlocks[i], "Current Output" ) );  
                usedOutput += GetPowerAsInt(GetDetailedInfoValue(solars[i], "Current Output"));
                maxOutput += GetPowerAsInt(GetDetailedInfoValue(solars[i], "Max Output"));
            }

            List<IMyTerminalBlock> reactors = new List<IMyTerminalBlock>();

            Log(log, "Solar power available: " + maxOutput);
            if (maxOutput - usedOutput < 10000)
            {
                for (var i = 0; i < reactors.Count; i++)
                {
                    ITerminalAction switchOn = reactors[i].GetActionWithName("OnOff_On");
                    switchOn.Apply(reactors[i]);
                }
                Log(log, "Reactor power on");
            }
            else
            {
                for (var i = 0; i < reactors.Count; i++)
                {
                    ITerminalAction switchOn = reactors[i].GetActionWithName("OnOff_Off");
                    switchOn.Apply(reactors[i]);
                }
                Log(log, "Reactor power off");
            }
        }
        
    }

    /// <summary>
    /// API by MMaster
    /// </summary>
    public static class MMLCDTextManager
    {
        private static Dictionary<IMyTextPanel, MMLCDText> panelTexts = new Dictionary<IMyTextPanel, MMLCDText>();

        public static MMLCDText GetLCDText(IMyTextPanel panel)
        {
            MMLCDText lcdText = null;

            if (!panelTexts.TryGetValue(panel, out lcdText))
            {
                lcdText = new MMLCDText();
                panelTexts.Add(panel, lcdText);
            }

            return lcdText;
        }

        public static void AddLine(IMyTextPanel panel, string line)
        {
            MMLCDText lcd = GetLCDText(panel);
            lcd.AddLine(line);
        }

        public static void Add(IMyTextPanel panel, string text)
        {
            MMLCDText lcd = GetLCDText(panel);

            lcd.AddFast(text);
            lcd.current_width += MMStringFunc.GetStringSize(text);
        }

        public static void AddRightAlign(IMyTextPanel panel, string text, float end_screen_x)
        {
            MMLCDText lcd = GetLCDText(panel);

            float text_width = MMStringFunc.GetStringSize(text);
            end_screen_x -= lcd.current_width;


            if (end_screen_x < text_width)
            {
                lcd.AddFast(text);
                lcd.current_width += text_width;
                return;
            }

            end_screen_x -= text_width;
            int fillchars = (int)Math.Round(end_screen_x / MMStringFunc.WHITESPACE_WIDTH, MidpointRounding.AwayFromZero);
            float fill_width = fillchars * MMStringFunc.WHITESPACE_WIDTH;

            string filler = new String(' ', fillchars);
            lcd.AddFast(filler + text);
            lcd.current_width += fill_width + text_width;

        }

        public static void AddCenter(IMyTextPanel panel, string text, float screen_x)
        {
            MMLCDText lcd = GetLCDText(panel);

            float text_width = MMStringFunc.GetStringSize(text);
            screen_x -= lcd.current_width;

            if (screen_x < text_width / 2)
            {
                lcd.AddFast(text);
                lcd.current_width += text_width;
                return;
            }

            screen_x -= text_width / 2;
            int fillchars = (int)Math.Round(screen_x / MMStringFunc.WHITESPACE_WIDTH, MidpointRounding.AwayFromZero);
            float fill_width = fillchars * MMStringFunc.WHITESPACE_WIDTH;

            string filler = new String(' ', fillchars);
            lcd.AddFast(filler + text);
            lcd.current_width += fill_width + text_width;
        }

        public static void AddProgressBar(IMyTextPanel panel, double percent, int width = 22)
        {
            MMLCDText lcd = GetLCDText(panel);
            int totalBars = width - 2;
            int fill = (int)(percent * totalBars) / 100;
            if (fill > totalBars)
                fill = totalBars;
            string progress = "[" + new String('|', fill) + new String('\'', totalBars - fill) + "]";

            lcd.AddFast(progress);
            lcd.current_width += MMStringFunc.PROGRESSCHAR_WIDTH * width;
        }

        public static void ClearText(IMyTextPanel panel)
        {
            GetLCDText(panel).ClearText();
        }

        public static void UpdatePanel(IMyTextPanel panel)
        {
            MMLCDText lcd = GetLCDText(panel);
            panel.WritePublicText(lcd.GetDisplayString());
            panel.ShowTextureOnScreen();
            panel.ShowPublicTextOnScreen();
            //lcd.ScrollNextLine();
        }

        public class MMLCDText
        {
            public int scrollPosition = 0;
            public int scrollDirection = 1;
            public const int DisplayLines = 22; // 22 for font size 0.8 

            public List<string> lines = new List<string>();
            public int current_line = 0;
            public float current_width = 0;

            public void CheckCurLine()
            {
                if (current_line >= lines.Count)
                    lines.Add("");
            }

            public void AddFast(string text)
            {
                CheckCurLine();
                lines[current_line] += text;
            }

            public void AddLine(string line)
            {
                AddFast(line);
                current_line++;
                current_width = 0;
            }

            public void ClearText()
            {
                lines.Clear();
                current_width = 0;
                current_line = 0;
            }

            public string GetFullString()
            {
                return String.Join("\n", lines);
            }

            // Display only 22 lines from scrollPos 
            public string GetDisplayString()
            {
                if (lines.Count < DisplayLines)
                {
                    scrollPosition = 0;
                    scrollDirection = 1;
                    return GetFullString();
                }

                List<string> display =
                    lines.GetRange(scrollPosition,
                        Math.Min(lines.Count - scrollPosition, DisplayLines));

                return String.Join("\n", display);
            }

            //public void ScrollNextLine()
            //{
            //    int lines_cnt = lines.Count;
            //    if (lines_cnt < DisplayLines)
            //    {
            //        scrollPosition = 0;
            //        scrollDirection = 1;
            //        return;
            //    }

            //    if (scrollDirection > 0)
            //    {
            //        if (scrollPosition + LCDsProgram.SCROLL_LINES + DisplayLines > lines_cnt)
            //        {
            //            scrollDirection = -1;
            //            scrollPosition = lines_cnt - DisplayLines;
            //            return;
            //        }

            //        scrollPosition += LCDsProgram.SCROLL_LINES;
            //    }
            //    else
            //    {
            //        if (scrollPosition - LCDsProgram.SCROLL_LINES < 0)
            //        {
            //            scrollPosition = 0;
            //            scrollDirection = 1;
            //            return;
            //        }

            //        scrollPosition -= LCDsProgram.SCROLL_LINES;
            //    }
            //}
        }
    }

    public static class MMStringFunc
    {
        private static Dictionary<char, float> charSize = new Dictionary<char, float>();

        public const float WHITESPACE_WIDTH = 8f;
        public const float PROGRESSCHAR_WIDTH = 6f;

        public static void InitCharSizes()
        {
            if (charSize.Count > 0)
                return;

            AddCharsSize("3FKTabdeghknopqsuy", 17f);
            AddCharsSize("#0245689CXZ", 19f);
            AddCharsSize("$&GHPUVY", 20f);
            AddCharsSize("ABDNOQRS", 21f);
            AddCharsSize("(),.1:;[]ft{}", 9f);
            AddCharsSize("+<=>E^~", 18f);
            AddCharsSize(" !I`ijl", 8f);
            AddCharsSize("7?Jcz", 16f);
            AddCharsSize("L_vx", 15f);
            AddCharsSize("\"-r", 10f);
            AddCharsSize("mw", 27f);
            AddCharsSize("M", 26f);
            AddCharsSize("W", 31f);
            AddCharsSize("'|", 6f);
            AddCharsSize("*", 11f);
            AddCharsSize("\\", 12f);
            AddCharsSize("/", 14f);
            AddCharsSize("%", 24f);
            AddCharsSize("@", 25f);
            AddCharsSize("\n", 0f);
        }

        private static void AddCharsSize(string chars, float size)
        {
            for (int i = 0; i < chars.Length; i++)
                charSize.Add(chars[i], size);
        }

        public static float GetCharSize(char c)
        {
            float width = 17f;
            charSize.TryGetValue(c, out width);

            return width;
        }

        public static float GetStringSize(string str)
        {
            float sum = 0;
            for (int i = 0; i < str.Length; i++)
                sum += GetCharSize(str[i]);

            return sum;
        }

        public static string GetStringTrimmed(string text, float pixel_width)
        {
            int trimlen = Math.Min((int)pixel_width / 14, text.Length - 2);
            float stringSize = GetStringSize(text);
            if (stringSize <= pixel_width)
                return text;

            while (stringSize > pixel_width - 20)
            {
                text = text.Substring(0, trimlen);
                stringSize = GetStringSize(text);
                trimlen -= 2;
            }
            return text + "..";
        }
    }

}
