namespace Caraota.NET.Common.Exceptions
{
    public class MapleSessionException : Exception
    {
        public MapleSessionException(string message) : base(message) { }
        public MapleSessionException(string message, Exception innerException)
            : base(message, innerException) { }
    }
}
