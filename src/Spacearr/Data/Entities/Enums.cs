namespace Spacearr.Data.Entities;

public enum ArrType { Radarr, Sonarr }
public enum MediaKind { Movie, Episode }
public enum JobType { Scan, Enrich, Action }
public enum JobStatus { Queued, Running, Succeeded, Failed, Cancelled }
public enum JobTrigger { Manual, Scheduled }
public enum ActionType { Delete, Replace }
public enum ActionOutcome { Succeeded, Failed }
