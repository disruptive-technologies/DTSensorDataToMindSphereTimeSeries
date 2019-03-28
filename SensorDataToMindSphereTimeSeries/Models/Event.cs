namespace SensorDataToMindSphereTimeSeries.Models
{
    public class Event
    {
        public string EventId { get; set; }
        public string TargetName { get; set; }
        public string EventType { get; set; }
        public SensorData Data { get; set; }
        public string Timestamp { get; set; }
    }
}
