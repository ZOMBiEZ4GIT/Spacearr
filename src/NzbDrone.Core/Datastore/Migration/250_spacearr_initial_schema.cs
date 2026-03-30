using FluentMigrator;
using NzbDrone.Core.Datastore.Migration.Framework;

namespace NzbDrone.Core.Datastore.Migration
{
    [Migration(250)]
    public class spacearr_initial_schema : NzbDroneMigrationBase
    {
        protected override void MainDbUpgrade()
        {
            // Add missing columns to tables created by migration 001
            Alter.Table("ScheduledTasks").AddColumn("LastStartTime").AsDateTime().Nullable();

            // Create tables that were in later Radarr migrations but got deleted
            Create.TableForModel("NotificationStatus")
                  .WithColumn("ProviderId").AsInt32().NotNullable()
                  .WithColumn("InitialFailure").AsDateTime().Nullable()
                  .WithColumn("MostRecentFailure").AsDateTime().Nullable()
                  .WithColumn("EscalationLevel").AsInt32().NotNullable()
                  .WithColumn("DisabledTill").AsDateTime().Nullable();

            Create.TableForModel("CustomFilters")
                  .WithColumn("Type").AsString().NotNullable()
                  .WithColumn("Label").AsString().NotNullable()
                  .WithColumn("Filters").AsString().NotNullable();

            Create.TableForModel("UpdateHistory")
                  .WithColumn("Date").AsDateTime().NotNullable()
                  .WithColumn("Version").AsString().NotNullable()
                  .WithColumn("EventType").AsInt32().NotNullable();

            // Spacearr tables
            Create.TableForModel("MediaFiles")
                  .WithColumn("Path").AsString().NotNullable().Unique()
                  .WithColumn("SizeBytes").AsInt64().NotNullable()
                  .WithColumn("BitrateBps").AsInt64().NotNullable()
                  .WithColumn("Codec").AsString().Nullable()
                  .WithColumn("Resolution").AsString().Nullable()
                  .WithColumn("ResolutionWidth").AsInt32().NotNullable()
                  .WithColumn("ResolutionHeight").AsInt32().NotNullable()
                  .WithColumn("DurationSeconds").AsInt32().NotNullable()
                  .WithColumn("ContainerFormat").AsString().Nullable()
                  .WithColumn("LibraryPath").AsString().Nullable()
                  .WithColumn("LastScanned").AsDateTime().NotNullable();

            Create.TableForModel("MediaItems")
                  .WithColumn("MediaFileId").AsInt32().NotNullable()
                  .WithColumn("Source").AsInt32().NotNullable()
                  .WithColumn("ExternalId").AsInt32().NotNullable()
                  .WithColumn("Title").AsString().NotNullable()
                  .WithColumn("Year").AsInt32().NotNullable()
                  .WithColumn("SeriesTitle").AsString().Nullable()
                  .WithColumn("SeasonNumber").AsInt32().Nullable()
                  .WithColumn("EpisodeNumber").AsInt32().Nullable()
                  .WithColumn("QualityProfile").AsString().Nullable()
                  .WithColumn("Monitored").AsBoolean().NotNullable()
                  .WithColumn("Tags").AsString().Nullable()
                  .WithColumn("PosterUrl").AsString().Nullable()
                  .WithColumn("LastEnriched").AsDateTime().NotNullable();

            Create.TableForModel("ScanJobs")
                  .WithColumn("StartedAt").AsDateTime().NotNullable()
                  .WithColumn("CompletedAt").AsDateTime().Nullable()
                  .WithColumn("Status").AsInt32().NotNullable()
                  .WithColumn("FilesScanned").AsInt32().NotNullable()
                  .WithColumn("FilesAdded").AsInt32().NotNullable()
                  .WithColumn("FilesRemoved").AsInt32().NotNullable()
                  .WithColumn("ErrorMessage").AsString().Nullable();

            Create.TableForModel("ActionHistory")
                  .WithColumn("ActionType").AsInt32().NotNullable()
                  .WithColumn("MediaItemId").AsInt32().Nullable()
                  .WithColumn("Title").AsString().Nullable()
                  .WithColumn("Details").AsString().Nullable()
                  .WithColumn("OldQuality").AsString().Nullable()
                  .WithColumn("NewQuality").AsString().Nullable()
                  .WithColumn("SpaceFreedBytes").AsInt64().NotNullable()
                  .WithColumn("Timestamp").AsDateTime().NotNullable();

            Create.TableForModel("Recommendations")
                  .WithColumn("MediaItemId").AsInt32().NotNullable()
                  .WithColumn("RecommendationType").AsInt32().NotNullable()
                  .WithColumn("CurrentSizeBytes").AsInt64().NotNullable()
                  .WithColumn("EstimatedNewSizeBytes").AsInt64().NotNullable()
                  .WithColumn("EstimatedSavingsBytes").AsInt64().NotNullable()
                  .WithColumn("SuggestedQuality").AsString().Nullable()
                  .WithColumn("Dismissed").AsBoolean().NotNullable()
                  .WithColumn("CreatedAt").AsDateTime().NotNullable();

            Create.TableForModel("Rules")
                  .WithColumn("Name").AsString().NotNullable()
                  .WithColumn("FilterCriteria").AsString().Nullable()
                  .WithColumn("ActionType").AsInt32().NotNullable()
                  .WithColumn("TargetQuality").AsString().Nullable()
                  .WithColumn("Active").AsBoolean().NotNullable()
                  .WithColumn("LastEvaluated").AsDateTime().Nullable();

            // Indexes (Path already has unique index from .Unique() above)
            Create.Index("IX_MediaFiles_LibraryPath").OnTable("MediaFiles").OnColumn("LibraryPath");
            Create.Index("IX_MediaItems_MediaFileId").OnTable("MediaItems").OnColumn("MediaFileId");
            Create.Index("IX_MediaItems_ExternalId").OnTable("MediaItems").OnColumn("ExternalId");
            Create.Index("IX_ActionHistory_Timestamp").OnTable("ActionHistory").OnColumn("Timestamp");
            Create.Index("IX_Recommendations_MediaItemId").OnTable("Recommendations").OnColumn("MediaItemId");
            Create.Index("IX_Recommendations_Dismissed").OnTable("Recommendations").OnColumn("Dismissed");
        }
    }
}
