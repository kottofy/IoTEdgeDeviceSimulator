using System;

namespace IoTEdgeDeviceSimulator
{
    public class DeviceMessage
    {
        public string Name { get; set; }
        public DateTime TimeStamp { get; set; }
        public string OutputFormat { get; set; }
        public Message Message { get; set; }

        public DeviceMessage()
        {
            Message = new Message();
        }
    }

    public class Message
    {
        public Log Log { get; set; }

        public Message()
        {
            Log = new Log();
        }
    }

    public class Log
    {
        public DateTime logDateTime { get; set; }
        public float aFloatValue { get; set; }
        public string aStringValue { get; set; }
        public int anIntValue { get; set; }
    }
}