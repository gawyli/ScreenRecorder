using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenRecorder.Capture.Utilities;
public static class EnumsUtils
{
    public static T ParseEnumValue<T>(string input)
    {
        return (T)Enum.Parse(typeof(T), input, false);
    }
}