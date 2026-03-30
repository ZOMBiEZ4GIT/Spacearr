using System;

namespace NzbDrone.Common.Exceptions
{
    public class SpacearrStartupException : NzbDroneException
    {
        public SpacearrStartupException(string message, params object[] args)
            : base("Spacearr failed to start: " + string.Format(message, args))
        {
        }

        public SpacearrStartupException(string message)
            : base("Spacearr failed to start: " + message)
        {
        }

        public SpacearrStartupException()
            : base("Spacearr failed to start")
        {
        }

        public SpacearrStartupException(Exception innerException, string message, params object[] args)
            : base("Spacearr failed to start: " + string.Format(message, args), innerException)
        {
        }

        public SpacearrStartupException(Exception innerException, string message)
            : base("Spacearr failed to start: " + message, innerException)
        {
        }

        public SpacearrStartupException(Exception innerException)
            : base("Spacearr failed to start: " + innerException.Message)
        {
        }
    }
}
