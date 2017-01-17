﻿namespace RealArtists.ShipHub.Common.DataModel {
  using System;
  using System.Collections.Generic;
  using System.Data;
  using System.Data.Common;
  using System.Data.Entity;
  using System.Data.SqlClient;
  using System.Diagnostics;
  using System.Diagnostics.CodeAnalysis;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using GitHub;
  using Legacy;
  using Types;

  [DbConfigurationType(typeof(ShipHubContextConfiguration))]
  public class ShipHubContext : DbContext {
    static ShipHubContext() {
      // Tell EF to leave our DB alone.
      // Maybe do migrations with dacpacs when possible later.
      Database.SetInitializer<ShipHubContext>(null);
    }

    public ShipHubContext()
      : this("name=ShipHubContext") {
    }

    public ShipHubContext(string nameOrConnectionString)
      : base(nameOrConnectionString) {
      ConnectionFactory = new SqlConnectionFactory(Database.Connection.ConnectionString);
    }

    public ShipHubContext(DbConnection existingConnection, bool contextOwnsConnection)
      : base(existingConnection, contextOwnsConnection) {
      ConnectionFactory = new SqlConnectionFactory(Database.Connection.ConnectionString);
    }

    public virtual DbSet<AccountRepository> AccountRepositories { get; set; }
    public virtual DbSet<Account> Accounts { get; set; }
    public virtual DbSet<CacheMetadata> CacheMetadata { get; set; }
    public virtual DbSet<Comment> Comments { get; set; }
    public virtual DbSet<Hook> Hooks { get; set; }
    public virtual DbSet<IssueEvent> IssueEvents { get; set; }
    public virtual DbSet<Issue> Issues { get; set; }
    public virtual DbSet<Label> Labels { get; set; }
    public virtual DbSet<Milestone> Milestones { get; set; }
    public virtual DbSet<Project> Projects { get; set; }
    public virtual DbSet<OrganizationAccount> OrganizationAccounts { get; set; }
    public virtual DbSet<Repository> Repositories { get; set; }
    public virtual DbSet<Subscription> Subscriptions { get; set; }
    public virtual DbSet<SyncLog> SyncLogs { get; set; }
    public virtual DbSet<Usage> Usage { get; set; }

    public virtual IQueryable<User> Users { get { return Accounts.OfType<User>(); } }
    public virtual IQueryable<Organization> Organizations { get { return Accounts.OfType<Organization>(); } }

    public SqlConnectionFactory ConnectionFactory { get; }

    public override int SaveChanges() {
      throw new NotImplementedException("Please use asynchronous methods instead.");
    }

    public override Task<int> SaveChangesAsync() {
      if (Environment.StackTrace.Contains("RealArtists.ShipHub.Api.Tests")) {
        // The current implementation of EF calls SaveChangesAsync(CancellationToken cancellationToken) here,
        // so we could just have the override below. However, in case that changes, keep the test in both
        // places. It should only impact tests.
        return base.SaveChangesAsync();
      } else {
        throw new InvalidOperationException("EF sucks at concurrency. Use a stored procedure instead.");
      }
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken) {
      if (Environment.StackTrace.Contains("RealArtists.ShipHub.Api.Tests")) {
        return base.SaveChangesAsync(cancellationToken);
      } else {
        throw new InvalidOperationException("EF sucks at concurrency. Use a stored procedure instead.");
      }
    }

    [SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "instance", Justification = "See comment.")]
    protected override void OnModelCreating(DbModelBuilder modelBuilder) {
      // This gross hack ensure the right DLL gets copied as a dependency of our project.
      // If you must know: http://stackoverflow.com/a/23329890
      var instance = System.Data.Entity.SqlServer.SqlProviderServices.Instance;
      // End Hack

      modelBuilder.Entity<Account>()
        .Map<User>(m => m.Requires("Type").HasValue(Account.UserType))
        .Map<Organization>(m => m.Requires("Type").HasValue(Account.OrganizationType));

      modelBuilder.Entity<Account>()
        .HasMany(e => e.Comments)
        .WithRequired(e => e.User)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Account>()
        .HasMany(e => e.Issues)
        .WithRequired(e => e.User)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Account>()
        .HasMany(e => e.OwnedRepositories)
        .WithRequired(e => e.Account)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Account>()
        .HasOptional(e => e.Subscription)
        .WithRequired(e => e.Account)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Issue>()
        .HasMany(e => e.Assignees)
        .WithMany(e => e.AssignedIssues)
        .Map(m => m.ToTable("IssueAssignees").MapLeftKey("IssueId").MapRightKey("UserId"));

      modelBuilder.Entity<Issue>()
        .HasMany(e => e.Comments)
        .WithRequired(e => e.Issue)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Issue>()
        .HasMany(e => e.Labels)
        .WithMany(e => e.Issues)
        .Map(m => m.ToTable("IssueLabels").MapLeftKey("IssueId").MapRightKey("LabelId"));

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.LinkedAccounts)
        .WithRequired(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.Comments)
        .WithRequired(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.Events)
        .WithRequired(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.Issues)
        .WithRequired(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.Milestones)
        .WithRequired(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.Projects)
        .WithOptional(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Repository>()
        .HasMany(e => e.Labels)
        .WithRequired(e => e.Repository)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<User>()
        .HasMany(e => e.AssignableRepositories)
        .WithMany(e => e.AssignableAccounts)
        .Map(m => m.ToTable("RepositoryAccounts").MapLeftKey("AccountId").MapRightKey("RepositoryId"));

      modelBuilder.Entity<User>()
        .HasMany(e => e.LinkedRepositories)
        .WithRequired(e => e.Account)
        .WillCascadeOnDelete(false);

      modelBuilder.Entity<Organization>()
        .HasMany(e => e.Projects)
        .WithOptional(e => e.Organization)
        .WillCascadeOnDelete(false);
    }

    public Task BumpRepositoryVersion(long repositoryId) {
      return ExecuteCommandTextAsync(
        "UPDATE SyncLog SET [RowVersion] = DEFAULT WHERE OwnerType = 'repo' AND OwnerId = @RepoId AND ItemType = 'repository' and ItemId = @RepoId",
        new SqlParameter("RepoId", SqlDbType.BigInt) { Value = repositoryId });
    }

    public Task BumpOrganizationVersion(long organizationId) {
      return ExecuteCommandTextAsync(
        "UPDATE SyncLog SET [RowVersion] = DEFAULT WHERE OwnerType = 'org' AND OwnerId = @OrgId AND ItemType = 'account' AND ItemId = @OrgId",
        new SqlParameter("OrgId", SqlDbType.BigInt) { Value = organizationId });
    }

    public async Task RevokeAccessToken(string accessToken) {
      using (dynamic dsp = new DynamicStoredProcedure("[dbo].[RevokeAccessToken]", ConnectionFactory)) {
        dsp.Token = accessToken;
        await dsp.ExecuteNonQueryAsync();
      }
    }

    public Task UpdateMetadata(string table, long id, GitHubResponse response) {
      return UpdateMetadata(table, "MetadataJson", id, response);
    }

    public Task UpdateMetadata(string table, string column, long id, GitHubResponse response) {
      return UpdateMetadata(table, column, id, GitHubMetadata.FromResponse(response));
    }

    public Task UpdateMetadata(string table, long id, GitHubMetadata metadata) {
      return UpdateMetadata(table, "MetadataJson", id, metadata);
    }

    public Task UpdateMetadata(string table, string column, long id, GitHubMetadata metadata) {
      // This can happen sometimes and doesn't make sense to handle until here.
      // Obviously, don't update.
      if (metadata == null) {
        return Task.CompletedTask;
      }

      return ExecuteCommandTextAsync(
        $@"UPDATE [{table}] SET
             [{column}] = @Metadata
           WHERE Id = @Id
             AND ([{column}] IS NULL OR CAST(JSON_VALUE([{column}], '$.lastRefresh') as DATETIMEOFFSET) < CAST(JSON_VALUE(@Metadata, '$.lastRefresh') as DATETIMEOFFSET))",
        new SqlParameter("Id", SqlDbType.BigInt) { Value = id },
        new SqlParameter("Metadata", SqlDbType.NVarChar) { Value = metadata.SerializeObject() });
    }

    public async Task UpdateRateLimit(GitHubRateLimit limit) {
      using (dynamic dsp = new DynamicStoredProcedure("[dbo].[UpdateRateLimit]", ConnectionFactory)) {
        dsp.Token = limit.AccessToken;
        dsp.RateLimit = limit.RateLimit;
        dsp.RateLimitRemaining = limit.RateLimitRemaining;
        dsp.RateLimitReset = limit.RateLimitReset;
        await dsp.ExecuteNonQueryAsync();
      }
    }

    public async Task SetUserAccessToken(long userId, string scopes, GitHubRateLimit limit) {
      using (dynamic dsp = new DynamicStoredProcedure("[dbo].[SetUserAccessToken]", ConnectionFactory)) {
        dsp.UserId = userId;
        dsp.Scopes = scopes;
        dsp.Token = limit.AccessToken;
        dsp.RateLimit = limit.RateLimit;
        dsp.RateLimitRemaining = limit.RateLimitRemaining;
        dsp.RateLimitReset = limit.RateLimitReset;
        await dsp.ExecuteNonQueryAsync();
      }
    }

    public Task UpdateRepositoryIssueSince(long repoId, DateTimeOffset? issueSince) {
      return ExecuteCommandTextAsync(
        $"UPDATE Repositories SET IssueSince = @IssueSince WHERE Id = @RepoId",
        new SqlParameter("IssueSince", SqlDbType.DateTimeOffset) { Value = issueSince },
        new SqlParameter("RepoId", SqlDbType.BigInt) { Value = repoId });
    }

    public async Task UpdateCache(string cacheKey, GitHubMetadata metadata) {
      // This can happen sometimes and doesn't make sense to handle until here.
      // Obviously, don't update.
      if (metadata == null) {
        return;
      }

      using (dynamic dsp = new DynamicStoredProcedure("[dbo].[UpdateCacheMetadata]", ConnectionFactory)) {
        dsp.Key = cacheKey;
        dsp.MetadataJson = metadata.SerializeObject();
        await dsp.ExecuteNonQueryAsync();
      }
    }

    private async Task<int> ExecuteCommandTextAsync(string commandText, params SqlParameter[] parameters) {
      using (var conn = ConnectionFactory.Get())
      using (var cmd = new SqlCommand(commandText, conn)) {
        try {
          cmd.CommandType = CommandType.Text;
          cmd.Parameters.AddRange(parameters);
          if (conn.State != ConnectionState.Open) {
            await conn.OpenAsync();
          }
          return await cmd.ExecuteNonQueryAsync();
        } finally {
          if (conn.State != ConnectionState.Closed) {
            conn.Close();
          }
        }
      }
    }

    private async Task<ChangeSummary> ExecuteAndReadChanges(string procedureName, Action<dynamic> applyParams) {
      var result = new ChangeSummary();

      using (var dsp = new DynamicStoredProcedure(procedureName, ConnectionFactory)) {
        applyParams(dsp);

        for (int attempt = 1; ; ++attempt) {
          try {
            using (var sdr = await dsp.ExecuteReaderAsync()) {
              dynamic ddr = sdr;
              do {
                while (sdr.Read()) {
                  long itemId = ddr.ItemId;
                  switch ((string)ddr.ItemType) {
                    case "org":
                      result.Organizations.Add(itemId);
                      break;
                    case "repo":
                      result.Repositories.Add(itemId);
                      break;
                    case "user":
                      result.Users.Add(itemId);
                      break;
                    default:
                      throw new Exception($"Unknown change ItemType {ddr.ItemType}");
                  }
                }
              } while (sdr.NextResult());

              return result;
            }
          } catch (SqlException ex) {
            if (ex.Number == 1205 && attempt < 2) {
              // Retry deadlock once
              continue;
            }
            throw;
          }
        }
      }
    }

    public Task<ChangeSummary> UpdateAccount(DateTimeOffset date, AccountTableType account) {
      return BulkUpdateAccounts(date, new[] { account });
    }

    public Task<ChangeSummary> BulkUpdateAccounts(DateTimeOffset date, IEnumerable<AccountTableType> accounts) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateAccounts]", x => {
        x.Date = date;
        x.Accounts = CreateTableParameter(
          "Accounts",
          "[dbo].[AccountTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("Type", typeof(string)),
            Tuple.Create("Login", typeof(string)),
          },
          y => new object[] {
            y.Id,
            y.Type,
            y.Login,
          },
          accounts);
      });
    }

    public Task<ChangeSummary> BulkUpdateComments(long repositoryId, IEnumerable<CommentTableType> comments) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateComments]", x => {
        x.RepositoryId = repositoryId;
        x.Comments = CreateCommentTable("Comments", comments);
      });
    }

    public Task<ChangeSummary> BulkUpdateIssueEvents(
      long userId,
      long repositoryId,
      IEnumerable<IssueEventTableType> issueEvents,
      IEnumerable<long> referencedAccounts) {
      return BulkUpdateEvents(userId, repositoryId, false, issueEvents, referencedAccounts);
    }

    public Task<ChangeSummary> BulkUpdateTimelineEvents(
      long userId,
      long repositoryId,
      IEnumerable<IssueEventTableType> issueEvents,
      IEnumerable<long> referencedAccounts) {
      return BulkUpdateEvents(userId, repositoryId, true, issueEvents, referencedAccounts);
    }

    private Task<ChangeSummary> BulkUpdateEvents(
      long userId,
      long repositoryId,
      bool fromTimeline,
      IEnumerable<IssueEventTableType> issueEvents,
      IEnumerable<long> referencedAccounts) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateIssueEvents]", x => {
        x.UserId = userId;
        x.RepositoryId = repositoryId;
        x.Timeline = fromTimeline;
        x.ReferencedAccounts = CreateItemListTable("ReferencedAccounts", referencedAccounts);
        x.IssueEvents = CreateTableParameter(
          "IssueEvents",
          "[dbo].[IssueEventTableType]",
          new[] {
            Tuple.Create("UniqueKey", typeof(string)),
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("IssueId", typeof(long)),
            Tuple.Create("ActorId", typeof(long)),
            Tuple.Create("Event", typeof(string)),
            Tuple.Create("CreatedAt", typeof(DateTimeOffset)),
            Tuple.Create("Hash", typeof(Guid)),
            Tuple.Create("Restricted", typeof(bool)),
            Tuple.Create("ExtensionData", typeof(string)),
          },
          y => new object[] {
            y.UniqueKey,
            y.Id,
            y.IssueId,
            y.ActorId,
            y.Event,
            y.CreatedAt,
            y.Hash,
            y.Restricted,
            y.ExtensionData,
          },
          issueEvents);
      });
    }

    public Task<ChangeSummary> BulkUpdateIssues(
      long repositoryId,
      IEnumerable<IssueTableType> issues,
      IEnumerable<MappingTableType> labels,
      IEnumerable<MappingTableType> assignees) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateIssues]", x => {
        x.RepositoryId = repositoryId;
        x.Issues = CreateTableParameter(
          "Issues",
          "[dbo].[IssueTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("UserId", typeof(long)),
            Tuple.Create("Number", typeof(int)),
            Tuple.Create("State", typeof(string)),
            Tuple.Create("Title", typeof(string)),
            Tuple.Create("Body", typeof(string)),
            Tuple.Create("MilestoneId", typeof(long)),
            Tuple.Create("Locked", typeof(bool)),
            Tuple.Create("CreatedAt", typeof(DateTimeOffset)),
            Tuple.Create("UpdatedAt", typeof(DateTimeOffset)),
            Tuple.Create("ClosedAt", typeof(DateTimeOffset)),
            Tuple.Create("ClosedById", typeof(long)),
            Tuple.Create("PullRequest", typeof(bool)),
            Tuple.Create("Reactions", typeof(string)),
          },
          y => new object[] {
            y.Id,
            y.UserId,
            y.Number,
            y.State,
            y.Title,
            y.Body,
            y.MilestoneId,
            y.Locked,
            y.CreatedAt,
            y.UpdatedAt,
            y.ClosedAt,
            y.ClosedById,
            y.PullRequest,
            y.Reactions,
          },
          issues);

        if (labels != null) {
          x.Labels = CreateMappingTable("Labels", labels);
        }

        if (assignees != null) {
          x.Assignees = CreateMappingTable("Assignees", assignees);
        }
      });
    }

    public Task<ChangeSummary> BulkUpdateMilestones(long repositoryId, IEnumerable<MilestoneTableType> milestones, bool complete = false) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateMilestones]", x => {
        x.RepositoryId = repositoryId;
        x.Complete = complete;
        x.Milestones = CreateTableParameter(
          "Milestones",
          "[dbo].[MilestoneTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("Number", typeof(int)),
            Tuple.Create("State", typeof(string)),
            Tuple.Create("Title", typeof(string)),
            Tuple.Create("Description", typeof(string)),
            Tuple.Create("CreatedAt", typeof(DateTimeOffset)),
            Tuple.Create("UpdatedAt", typeof(DateTimeOffset)),
            Tuple.Create("ClosedAt", typeof(DateTimeOffset)), // Nullable types handled by DataTable
            Tuple.Create("DueOn", typeof(DateTimeOffset)), // Nullable types handled by DataTable
          },
          y => new object[] {
            y.Id,
            y.Number,
            y.State,
            y.Title,
            y.Description,
            y.CreatedAt,
            y.UpdatedAt,
            y.ClosedAt,
            y.DueOn,
          },
          milestones);
      });
    }

    private Task<ChangeSummary> BulkUpdateProjects(IEnumerable<ProjectTableType> projects, long? repositoryId = null, long? organizationId = null) {
      Debug.Assert(repositoryId != null || organizationId != null, "Must specify either repositoryId or organizationId");
      Debug.Assert(!(repositoryId != null && organizationId != null), "Must specify exactly one of repositoryId or organizationId");

      return ExecuteAndReadChanges("[dbo].[BulkUpdateProjects]", x => {
        x.RepositoryId = new SqlParameter("RepositoryId", SqlDbType.BigInt);
        if (repositoryId != null) {
          x.RepositoryId.Value = repositoryId.Value;
        } else {
          x.RepositoryId.Value = DBNull.Value;
        }
        x.OrganizationId = new SqlParameter("OrganizationId", SqlDbType.BigInt);
        if (organizationId != null) {
          x.OrganizationId.Value = organizationId.Value;
        } else {
          x.OrganizationId.Value = DBNull.Value;
        }
        x.Projects = CreateTableParameter(
          "Projects",
          "[dbo].[ProjectTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("Name", typeof(string)),
            Tuple.Create("Number", typeof(long)),
            Tuple.Create("Body", typeof(string)),
            Tuple.Create("CreatedAt", typeof(DateTimeOffset)),
            Tuple.Create("UpdatedAt", typeof(DateTimeOffset)),
            Tuple.Create("CreatorId", typeof(long)),
          },
          y => new object[] {
            y.Id,
            y.Name,
            y.Number,
            y.Body,
            y.CreatedAt,
            y.UpdatedAt,
            y.CreatorId,
          },
          projects);
      });
    }

    public Task<ChangeSummary> BulkUpdateRepositoryProjects(long repositoryId, IEnumerable<ProjectTableType> projects) {
      return BulkUpdateProjects(projects, repositoryId: repositoryId);
    }

    public Task<ChangeSummary> BulkUpdateOrganizationProjects(long organizationId, IEnumerable<ProjectTableType> projects) {
      return BulkUpdateProjects(projects, organizationId: organizationId);
    }

    public Task<ChangeSummary> BulkUpdateLabels(long repositoryId, IEnumerable<LabelTableType> labels, bool complete = false) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateLabels]", x => {
        x.RepositoryId = repositoryId;
        x.Complete = complete;
        x.Labels = CreateTableParameter(
          "Labels",
          "[dbo].[LabelTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("Color", typeof(string)),
            Tuple.Create("Name", typeof(string)),
          },
          y => new object[] {
            y.Id,
            y.Color,
            y.Name,
          },
          labels);
      });
    }

    public Task<ChangeSummary> BulkUpdateIssueReactions(long repositoryId, long issueId, IEnumerable<ReactionTableType> reactions) {
      return BulkUpdateReactions(repositoryId, issueId, null, reactions);
    }

    public Task<ChangeSummary> BulkUpdateCommentReactions(long repositoryId, long commentId, IEnumerable<ReactionTableType> reactions) {
      return BulkUpdateReactions(repositoryId, null, commentId, reactions);
    }

    private Task<ChangeSummary> BulkUpdateReactions(long repositoryId, long? issueId, long? commentId, IEnumerable<ReactionTableType> reactions) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateReactions]", x => {
        x.RepositoryId = repositoryId;
        x.IssueId = issueId;
        x.CommentId = commentId;
        x.Reactions = CreateTableParameter(
          "Reactions",
          "[dbo].[ReactionTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("UserId", typeof(long)),
            Tuple.Create("Content", typeof(string)),
            Tuple.Create("CreatedAt", typeof(DateTimeOffset)),
          },
          y => new object[] {
            y.Id,
            y.UserId,
            y.Content,
            y.CreatedAt,
          },
          reactions);
      });
    }

    public Task<ChangeSummary> BulkUpdateRepositories(DateTimeOffset date, IEnumerable<RepositoryTableType> repositories) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateRepositories]", x => {
        x.Date = date;
        x.Repositories = CreateTableParameter(
          "Repositories",
          "[dbo].[RepositoryTableType]",
          new[] {
            Tuple.Create("Id", typeof(long)),
            Tuple.Create("AccountId", typeof(long)),
            Tuple.Create("Private", typeof(bool)),
            Tuple.Create("Name", typeof(string)),
            Tuple.Create("FullName", typeof(string)),
            Tuple.Create("Size", typeof(long)),
            Tuple.Create("Disabled", typeof(bool)),
          },
          y => new object[] {
            y.Id,
            y.AccountId,
            y.Private,
            y.Name,
            y.FullName,
            y.Size,
            y.Disabled,
          },
          repositories);
      });
    }

    public Task<ChangeSummary> DeleteComments(IEnumerable<long> commentIds) {
      return ExecuteAndReadChanges("[dbo].[DeleteComments]", x => {
        x.Comments = CreateItemListTable("Comments", commentIds);
      });
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "We're returning it for use elsewhere.")]
    public DynamicStoredProcedure PrepareSync(string accessToken, long pageSize, IEnumerable<VersionTableType> repoVersions, IEnumerable<VersionTableType> orgVersions) {
      dynamic dsp = new DynamicStoredProcedure("[dbo].[WhatsNew]", ConnectionFactory);
      dsp.Token = accessToken;
      dsp.PageSize = pageSize;
      dsp.RepositoryVersions = CreateVersionTableType("RepositoryVersions", repoVersions);
      dsp.OrganizationVersions = CreateVersionTableType("OrganizationVersions", orgVersions);

      return dsp;
    }

    public Task<ChangeSummary> SetAccountLinkedRepositories(long accountId, IEnumerable<Tuple<long, bool>> repoIdAndAdminPairs) {
      return ExecuteAndReadChanges("[dbo].[SetAccountLinkedRepositories]", x => {
        x.AccountId = accountId;
        x.RepositoryIds = CreateMappingTable(
          "RepositoryIds",
          repoIdAndAdminPairs.Select(y => new MappingTableType() { Item1 = y.Item1, Item2 = y.Item2 ? 1 : 0 }));
      });
    }

    public Task<ChangeSummary> SetUserOrganizations(long userId, IEnumerable<long> organizationIds) {
      return ExecuteAndReadChanges("[dbo].[SetUserOrganizations]", x => {
        x.UserId = userId;
        x.OrganizationIds = CreateItemListTable("OrganizationIds", organizationIds);
      });
    }

    public Task<ChangeSummary> SetOrganizationUsers(long organizationId, IEnumerable<Tuple<long, bool>> userIdAndAdminPairs) {
      return ExecuteAndReadChanges("[dbo].[SetOrganizationUsers]", x => {
        x.OrganizationId = organizationId;
        x.UserIds = CreateMappingTable("UserIds", userIdAndAdminPairs.Select(y => new MappingTableType() { Item1 = y.Item1, Item2 = y.Item2 ? 1 : 0 }));
      });
    }

    public Task<ChangeSummary> SetRepositoryAssignableAccounts(long repositoryId, IEnumerable<long> assignableAccountIds) {
      return ExecuteAndReadChanges("[dbo].[SetRepositoryAssignableAccounts]", x => {
        x.RepositoryId = repositoryId;
        x.AssignableAccountIds = CreateItemListTable("AssignableAccountIds", assignableAccountIds);
      });
    }

    public Task<ChangeSummary> SetRepositoryIssueTemplate(long repositoryId, string issueTemplate) {
      return ExecuteAndReadChanges("[dbo].[SetRepositoryIssueTemplate]", x => {
        x.RepositoryId = repositoryId;
        x.IssueTemplate = issueTemplate;
      });
    }

    public async Task RecordUsage(long accountId, DateTimeOffset date) {
      if (date.Offset != TimeSpan.Zero) {
        throw new ArgumentException("date must be in UTC");
      }

      using (dynamic dsp = new DynamicStoredProcedure("[dbo].[RecordUsage]", ConnectionFactory)) {
        dsp.AccountId = accountId;
        dsp.Date = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, TimeSpan.Zero);
        await dsp.ExecuteNonQueryAsync();
      }
    }

    public Task<ChangeSummary> DeleteMilestone(long milestoneId) {
      return ExecuteAndReadChanges("[dbo].[DeleteMilestone]", x => {
        x.MilestoneId = milestoneId;
      });
    }

    public Task<ChangeSummary> DeleteLabel(long labelId) {
      return ExecuteAndReadChanges("[dbo].[DeleteLabel]", x => {
        x.LabelId = labelId;
      });
    }

    public Task<ChangeSummary> DisableRepository(long repositoryId, bool disabled) {
      return ExecuteAndReadChanges("[dbo].[DisableRepository]", x => {
        x.RepositoryId = repositoryId;
        x.Disabled = disabled;
      });
    }

    public async Task SaveRepositoryMetadata(
      long repositoryId,
      long repoSize,
      GitHubMetadata metadata,
      GitHubMetadata assignableMetadata,
      GitHubMetadata issueMetadata,
      DateTimeOffset issueSince,
      GitHubMetadata labelMetadata,
      GitHubMetadata milestoneMetadata,
      GitHubMetadata projectMetadata,
      GitHubMetadata contentsRootMetadata,
      GitHubMetadata contentsDotGitHubMetadata,
      GitHubMetadata contentsIssueTemplateMetadata) {
      using (dynamic dsp = new DynamicStoredProcedure("[dbo].[SaveRepositoryMetadata]", ConnectionFactory)) {
        dsp.RepositoryId = repositoryId;
        dsp.Size = repoSize;
        dsp.MetadataJson = metadata.SerializeObject();
        dsp.AssignableMetadataJson = assignableMetadata.SerializeObject();
        dsp.IssueMetadataJson = issueMetadata.SerializeObject();
        dsp.IssueSince = issueSince;
        dsp.LabelMetadataJson = labelMetadata.SerializeObject();
        dsp.MilestoneMetadataJson = milestoneMetadata.SerializeObject();
        dsp.ProjectMetadataJson = projectMetadata.SerializeObject();
        dsp.ContentsRootMetadataJson = contentsRootMetadata.SerializeObject();
        dsp.ContentsDotGitHubMetadataJson = contentsDotGitHubMetadata.SerializeObject();
        dsp.ContentsIssueTemplateMetadataJson = contentsIssueTemplateMetadata.SerializeObject();

        await dsp.ExecuteNonQueryAsync();
      }
    }

    public Task<ChangeSummary> BulkUpdateHooks(
      IEnumerable<HookTableType> hooks = null,
      IEnumerable<long> seen = null,
      IEnumerable<long> pinged = null,
      IEnumerable<long> deleted = null) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateHooks]", x => {
        if (hooks?.Any() == true) {
          x.Hooks = CreateTableParameter(
            "Hooks",
            "[dbo].[HookTableType]",
            new[] {
              Tuple.Create("Id", typeof(long)),
              Tuple.Create("GitHubId", typeof(long)),
              Tuple.Create("Secret", typeof(Guid)),
              Tuple.Create("Events", typeof(string)),
            },
            y => new object[] {
              y.Id,
              y.GitHubId,
              y.Secret,
              y.Events,
            },
            hooks);
        }

        if (seen?.Any() == true) {
          x.Seen = CreateItemListTable("Seen", seen);
        }

        if (pinged?.Any() == true) {
          x.Pinged = CreateItemListTable("Pinged", pinged);
        }

        if (deleted?.Any() == true) {
          x.Deleted = CreateItemListTable("Deleted", deleted);
        }
      });
    }

    public async Task<HookTableType> CreateHook(Guid secret, string events, long? organizationId = null, long? repositoryId = null) {
      if ((organizationId == null) == (repositoryId == null)) {
        throw new ArgumentException($"Exactly one of {nameof(organizationId)} and {nameof(repositoryId)} must be non-null.");
      }

      using (var sp = new DynamicStoredProcedure("[dbo].[CreateHook]", ConnectionFactory)) {
        dynamic dsp = sp;
        dsp.Secret = secret;
        dsp.Events = events;
        dsp.OrganizationId = organizationId;
        dsp.RepositoryId = repositoryId;

        using (var sdr = await sp.ExecuteReaderAsync(CommandBehavior.SingleRow)) {
          sdr.Read();
          dynamic ddr = sdr;
          return new HookTableType() {
            Id = ddr.Id,
            GitHubId = ddr.GitHubId,
            Secret = ddr.Secret,
            Events = ddr.Events,
          };
        }
      }
    }

    public Task<ChangeSummary> BulkUpdateSubscriptions(IEnumerable<SubscriptionTableType> subscriptions) {
      return ExecuteAndReadChanges("[dbo].[BulkUpdateSubscriptions]", x => {
        x.Subscriptions = CreateTableParameter(
          "Subscriptions",
          "[dbo].[SubscriptionTableType]",
          new[] {
            Tuple.Create("AccountId", typeof(long)),
            Tuple.Create("State", typeof(string)),
            Tuple.Create("TrialEndDate", typeof(DateTimeOffset)),
            Tuple.Create("Version", typeof(long)),
          },
          y => new object[] {
            y.AccountId,
            y.State,
            y.TrialEndDate,
            y.Version,
          },
          subscriptions);
      });
    }

    private static SqlParameter CreateItemListTable<T>(string parameterName, IEnumerable<T> values) {
      return CreateTableParameter(
        parameterName,
        "[dbo].[ItemListTableType]",
        new[] { Tuple.Create("Item", typeof(T)) },
        x => new object[] { x },
        values);
    }

    private static SqlParameter CreateCommentTable(string parameterName, IEnumerable<CommentTableType> comments) {
      return CreateTableParameter(
        parameterName,
        "[dbo].[CommentTableType]",
        new[] {
          Tuple.Create("Id", typeof(long)),
          Tuple.Create("IssueId", typeof(long)),
          Tuple.Create("IssueNumber", typeof(int)),
          Tuple.Create("UserId", typeof(long)),
          Tuple.Create("Body", typeof(string)),
          Tuple.Create("CreatedAt", typeof(DateTimeOffset)),
          Tuple.Create("UpdatedAt", typeof(DateTimeOffset)),
        },
        x => new object[] {
          x.Id,
          x.IssueId,
          x.IssueNumber,
          x.UserId,
          x.Body,
          x.CreatedAt,
          x.UpdatedAt,
        },
        comments);
    }

    private static SqlParameter CreateLabelTable(string parameterName, IEnumerable<LabelTableType> labels) {
      return CreateTableParameter(
        parameterName,
        "[dbo].[LabelTableType]",
        new[] {
          Tuple.Create("Id", typeof(long)),
          Tuple.Create("Color", typeof(string)),
          Tuple.Create("Name", typeof(string)),
        },
        x => new object[] {
          x.Id,
          x.Color,
          x.Name,
        },
        labels);
    }

    private static SqlParameter CreateMappingTable(string parameterName, IEnumerable<MappingTableType> mappings) {
      return CreateTableParameter(
        parameterName,
        "[dbo].[MappingTableType]",
        new[] {
          Tuple.Create("Item1", typeof(long)),
          Tuple.Create("Item2", typeof(long)),
        },
        x => new object[] {
          x.Item1,
          x.Item2,
        },
        mappings);
    }

    private static SqlParameter CreateVersionTableType(string parameterName, IEnumerable<VersionTableType> versions) {
      return CreateTableParameter(
        parameterName,
        "[dbo].[VersionTableType]",
        new[] {
          Tuple.Create("ItemId", typeof(long)),
          Tuple.Create("RowVersion", typeof(long)),
        },
        x => new object[] {
          x.ItemId,
          x.RowVersion,
        },
        versions);
    }

    private static SqlParameter CreateTableParameter<T>(string parameterName, string typeName, IEnumerable<Tuple<string, Type>> columns, Func<T, object[]> rowValues, IEnumerable<T> rows) {
      if (!typeName.Contains("[")) {
        typeName = $"[dbo].[{typeName}]";
      }

      DataTable table = null;

      try {
        if (rows != null) {
          table = new DataTable();

          table.Columns.AddRange(columns.Select(x => new DataColumn(x.Item1, x.Item2)).ToArray());

          foreach (var row in rows) {
            table.Rows.Add(rowValues(row));
          }
        }

        return new SqlParameter(parameterName, SqlDbType.Structured) {
          TypeName = typeName,
          Value = table
        };
      } catch {
        if (table != null) {
          table.Dispose();
          table = null;
        }
        throw;
      }
    }
  }
}
