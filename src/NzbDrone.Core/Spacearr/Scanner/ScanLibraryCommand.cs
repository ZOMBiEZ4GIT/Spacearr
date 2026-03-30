using NzbDrone.Core.Messaging.Commands;

namespace NzbDrone.Core.Spacearr.Scanner
{
    public class ScanLibraryCommand : Command
    {
        public override bool SendUpdatesToClient => true;
        public override bool RequiresDiskAccess => true;
        public override bool IsLongRunning => true;
    }
}
