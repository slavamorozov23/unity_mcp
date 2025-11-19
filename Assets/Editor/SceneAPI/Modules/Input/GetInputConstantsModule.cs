using System.Collections.Generic;
using Newtonsoft.Json;

namespace SceneAPI.Modules
{
    public class GetInputConstantsModule
    {
        public string Execute(string requestBody)
        {
            var constants = new
            {
                AxisType = new Dictionary<string, int>
                {
                    { "KeyOrMouseButton", 0 },
                    { "MouseMovement", 1 },
                    { "JoystickAxis", 2 }
                },
                AxisOptions = new Dictionary<string, int>
                {
                    { "X_Axis", 0 }, { "Y_Axis", 1 }, { "3rd_Axis", 2 }, { "4th_Axis", 3 },
                    { "5th_Axis", 4 }, { "6th_Axis", 5 }, { "7th_Axis", 6 }, { "8th_Axis", 7 },
                    { "9th_Axis", 8 }, { "10th_Axis", 9 }, { "11th_Axis", 10 }, { "12th_Axis", 11 },
                    { "13th_Axis", 12 }, { "14th_Axis", 13 }, { "15th_Axis", 14 }, { "16th_Axis", 15 },
                    { "17th_Axis", 16 }, { "18th_Axis", 17 }, { "19th_Axis", 18 }, { "20th_Axis", 19 }
                },
                JoyNum = new Dictionary<string, int>
                {
                    { "All_Joysticks", 0 },
                    { "Joystick_1", 1 }, { "Joystick_2", 2 }, { "Joystick_3", 3 }, { "Joystick_4", 4 },
                    { "Joystick_5", 5 }, { "Joystick_6", 6 }, { "Joystick_7", 7 }, { "Joystick_8", 8 }
                }
            };

            return JsonConvert.SerializeObject(new { success = true, constants = constants }, Formatting.Indented);
        }
    }
}