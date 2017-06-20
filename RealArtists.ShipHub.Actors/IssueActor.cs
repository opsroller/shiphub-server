﻿namespace RealArtists.ShipHub.Actors {
  using System;
  using System.Collections.Generic;
  using System.Data.Entity;
  using System.Linq;
  using System.Net;
  using System.Threading.Tasks;
  using ActorInterfaces;
  using ActorInterfaces.GitHub;
  using AutoMapper;
  using Common;
  using Common.DataModel;
  using Common.DataModel.Types;
  using Common.GitHub;
  using Newtonsoft.Json.Linq;
  using Orleans;
  using QueueClient;
  using gm = Common.GitHub.Models;

  public class IssueActor : Grain, IIssueActor {
    private IMapper _mapper;
    private IGrainFactory _grainFactory;
    private IFactory<ShipHubContext> _contextFactory;
    private IShipHubQueueClient _queueClient;

    // Issue Fields
    private long _issueId;
    private int _issueNumber;
    private long _repoId;
    private string _repoFullName;
    private bool _isPullRequest;

    private GitHubMetadata _metadata;
    private GitHubMetadata _commentMetadata;
    private GitHubMetadata _reactionMetadata;

    // Pull Request Fields
    private long? _prId;
    private string _prHeadSha;
    private string _prBaseBranch;
    private bool _prMergeableStateBlocked;

    private GitHubMetadata _prMetadata;
    private GitHubMetadata _prCommentMetadata;
    private GitHubMetadata _prStatusMetadata;
    private GitHubMetadata _prMergeStatusMetadata;

    // Event sync
    private static readonly HashSet<string> _IgnoreTimelineEvents = new HashSet<string>(new[] {
      "commented",
      "commit-commented",
      "line-commented",
      "mentioned",
      "review_dismissed",
      "reviewed",
      "subscribed",
      "unsubscribed"
    }, StringComparer.OrdinalIgnoreCase);

    public IssueActor(IMapper mapper, IGrainFactory grainFactory, IFactory<ShipHubContext> contextFactory, IShipHubQueueClient queueClient) {
      _mapper = mapper;
      _grainFactory = grainFactory;
      _contextFactory = contextFactory;
      _queueClient = queueClient;
    }

    public override async Task OnActivateAsync() {
      _issueNumber = (int)this.GetPrimaryKeyLong(out _repoFullName);

      using (var context = _contextFactory.CreateInstance()) {
        var issue = await context.Issues
          .AsNoTracking()
          .SingleOrDefaultAsync(x => x.Repository.FullName == _repoFullName && x.Number == _issueNumber);

        if (issue == null) {
          throw new InvalidOperationException($"Issue {_repoFullName}#{_issueNumber} does not exist and cannot be activated.");
        }

        _issueId = issue.Id;
        _repoId = issue.RepositoryId;
        _isPullRequest = issue.PullRequest;

        _metadata = issue.Metadata;
        _commentMetadata = issue.CommentMetadata;
        _reactionMetadata = issue.ReactionMetadata;

        if (_isPullRequest) {
          var pullRequest = await context.PullRequests
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.IssueId == _issueId);

          _prId = pullRequest?.Id;
          _prHeadSha = pullRequest?.Head?.Sha;
          _prBaseBranch = pullRequest?.Base?.Ref;
          _prMergeableStateBlocked = pullRequest?.MergeableState?.Equals("blocked", StringComparison.OrdinalIgnoreCase) == true;

          _prMetadata = pullRequest?.Metadata;
          _prCommentMetadata = pullRequest?.CommentMetadata;
          _prStatusMetadata = pullRequest?.StatusMetadata;
          _prMergeStatusMetadata = pullRequest?.MergeStatusMetadata;
        }
      }

      await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync() {
      using (var context = _contextFactory.CreateInstance()) {
        await Save(context);
      }
      await base.OnDeactivateAsync();
    }

    private async Task Save(ShipHubContext context) {
      // TODO: These actors churn a lot. Make a stored proc for save.
      await context.UpdateMetadata("Issues", _issueId, _metadata);
      await context.UpdateMetadata("Issues", "CommentMetadataJson", _issueId, _commentMetadata);
      await context.UpdateMetadata("Issues", "ReactionMetadataJson", _issueId, _reactionMetadata);

      if (_prId.HasValue) {
        await context.UpdateMetadata("PullRequests", "MetadataJson", _prId.Value, _prMetadata);
        await context.UpdateMetadata("PullRequests", "CommentMetadataJson", _prId.Value, _prCommentMetadata);
        await context.UpdateMetadata("PullRequests", "StatusMetadataJson", _prId.Value, _prStatusMetadata);
        await context.UpdateMetadata("PullRequests", "MergeStatusMetadataJson", _prId.Value, _prMergeStatusMetadata);
      }
    }

    public async Task SyncInteractive(long forUserId) {
      var ghc = _grainFactory.GetGrain<IGitHubActor>(forUserId);

      using (var context = _contextFactory.CreateInstance()) {
        var updater = new DataUpdater(context, _mapper);
        try {
          await SyncIssueTimeline(ghc, forUserId, updater, context);
        } catch (GitHubRateException) {
          // nothing to do
        }

        await updater.Changes.Submit(_queueClient);

        // Save metadata and other updates
        await Save(context);
      }
    }

    private async Task SyncIssueTimeline(IGitHubActor ghc, long forUserId, DataUpdater updater, ShipHubContext context) {
      // Always refresh the issue when viewed
      await UpdateIssueDetails(ghc, updater);

      // If it's a PR we need that data too.
      if (_isPullRequest) {
        await UpdatePullRequestDetails(ghc, forUserId, updater);

        // Reviews have to come before comments, since PR comments reference reviews
        await UpdatePullRequestReviews(ghc, forUserId, updater);

        await UpdatePullRequestComments(ghc, updater);
        // TODO: PullRequestComment reactions?
        await UpdatePullRequestCommitStatuses(ghc, updater);
      }

      // This acutally does a lot of things, including retrieving referenced
      // comments, commits, etc.
      await UpdateIssueTimeline(ghc, forUserId, updater);

      // So many reactions
      await UpdateIssueReactions(ghc, updater);
      await UpdateIssueCommentReactions(ghc, updater, context);
      await UpdateCommitCommentReactions(ghc, updater, context);
      if (_isPullRequest) { // Can't roll this up into other PR code because it must come after timeline
        await UpdatePullRequestCommentReactions(ghc, updater, context);
      }
    }

    private async Task UpdateIssueDetails(IGitHubActor ghc, DataUpdater updater) {
      var issueResponse = await ghc.Issue(_repoFullName, _issueNumber, _metadata, RequestPriority.Interactive);
      if (issueResponse.IsOk) {
        _isPullRequest = issueResponse.Result.PullRequest != null;  // Issues can become PRs
        await updater.UpdateIssues(_repoId, issueResponse.Date, new[] { issueResponse.Result });
      }
      _metadata = GitHubMetadata.FromResponse(issueResponse);
    }

    private async Task UpdateIssueComments(IGitHubActor ghc, DataUpdater updater) {
      if (_commentMetadata.IsExpired()) {
        var commentResponse = await ghc.IssueComments(_repoFullName, _issueNumber, null, _commentMetadata, RequestPriority.Interactive);
        if (commentResponse.IsOk) {
          await updater.UpdateIssueComments(_repoId, commentResponse.Date, commentResponse.Result);
        }
        _commentMetadata = GitHubMetadata.FromResponse(commentResponse);
      }
    }

    private async Task UpdatePullRequestDetails(IGitHubActor ghc, long forUserId, DataUpdater updater) {
      // Sadly, the PR info doesn't contain labels 😭
      var prResponse = await ghc.PullRequest(_repoFullName, _issueNumber, _prMetadata, RequestPriority.Interactive);
      if (prResponse.IsOk) {
        var pr = prResponse.Result;
        _prId = pr.Id; // Issues can become PRs
        _prHeadSha = pr.Head.Sha;
        _prBaseBranch = pr.Base.Ref;
        _prMergeableStateBlocked = pr.MergeableState == "blocked";

        await updater.UpdatePullRequests(_repoId, prResponse.Date, new[] { prResponse.Result });
      }
      _prMetadata = GitHubMetadata.FromResponse(prResponse);

      // Branch Protection
      if (_prMergeableStateBlocked && _prBaseBranch != null) {
        _grainFactory.GetGrain<IRepositoryActor>(_repoId).SyncProtectedBranch(_prBaseBranch, forUserId).LogFailure();
      }
    }

    private async Task UpdatePullRequestReviews(IGitHubActor ghc, long forUserId, DataUpdater updater) {
      gm.Review myReview = null;

      // Reviews and Review comments need to use the current user's token. Don't track metadata (yet - per user ideally)
      var prReviewsResponse = await ghc.PullRequestReviews(_repoFullName, _issueNumber, priority: RequestPriority.Interactive);
      if (prReviewsResponse.IsOk) {
        await updater.UpdateReviews(_repoId, _issueId, prReviewsResponse.Date, prReviewsResponse.Result, userId: forUserId, complete: true);
        myReview = prReviewsResponse.Result
         .Where(x => x.State.Equals("PENDING", StringComparison.OrdinalIgnoreCase))
         .FirstOrDefault(x => x.User.Id == forUserId);
      }

      // Only fetch if *this user* has a pending review
      // Since we make the request every time, it's ok not to look for pending reviews in the DB
      if (myReview != null) {
        var reviewCommentsResponse = await ghc.PullRequestReviewComments(_repoFullName, _issueNumber, myReview.Id, priority: RequestPriority.Interactive);
        if (reviewCommentsResponse.IsOk && reviewCommentsResponse.Result.Any()) {
          await updater.UpdatePullRequestComments(_repoId, _issueId, reviewCommentsResponse.Date, reviewCommentsResponse.Result, pendingReviewId: myReview.Id);
        }
      }
    }

    private async Task UpdatePullRequestComments(IGitHubActor ghc, DataUpdater updater) {
      var prCommentsResponse = await ghc.PullRequestComments(_repoFullName, _issueNumber, _prCommentMetadata, RequestPriority.Interactive);
      if (prCommentsResponse.IsOk) {
        await updater.UpdatePullRequestComments(_repoId, _issueId, prCommentsResponse.Date, prCommentsResponse.Result);
      }
      _prCommentMetadata = GitHubMetadata.FromResponse(prCommentsResponse);
    }

    private async Task UpdatePullRequestCommitStatuses(IGitHubActor ghc, DataUpdater updater) {
      var commitStatusesResponse = await ghc.CommitStatuses(_repoFullName, _prHeadSha, _prStatusMetadata, RequestPriority.Interactive);
      if (commitStatusesResponse.IsOk) {
        await updater.UpdateCommitStatuses(_repoId, _prHeadSha, commitStatusesResponse.Result);
      }
      _prStatusMetadata = GitHubMetadata.FromResponse(commitStatusesResponse);
    }

    private async Task UpdateIssueTimeline(IGitHubActor ghc, long forUserId, DataUpdater updater) {
      ///////////////////////////////////////////
      /* NOTE!
       * We can't sync the timeline incrementally, because the client wants commit and
       * reference data inlined. This means we always have to download all the
       * timeline events in case an old one now has updated data. Other options are to
       * just be wrong, or to simply reference the user by id and mark them referenced
       * by the repo.
       */
      //////////////////////////////////////////

      // TODO: cache per-user
      // TODO: If caching, are there things that should occur every time anyway?
      var timelineResponse = await ghc.Timeline(_repoFullName, _issueNumber, _issueId, priority: RequestPriority.Interactive);
      if (timelineResponse.IsOk) {
        var allEvents = timelineResponse.Result;
        var filteredEvents = allEvents.Where(x => !_IgnoreTimelineEvents.Contains(x.Event)).ToArray();

        // For adding to the DB later
        // TODO: Technically this can pick stale accounts if an old and new version are both in the collection.
        // My batching branch fixes this by tracking versions
        // Set must allow nulls
        var accounts = new HashSet<gm.Account>(KeyEqualityComparer.FromKeySelector((gm.Account x) => x?.Id));

        foreach (var timelineEvent in filteredEvents) {
          accounts.Add(timelineEvent.Actor);
          accounts.Add(timelineEvent.Assignee);
        }

        await LookupEventCommitDetails(ghc, accounts, filteredEvents);
        await LookupEventSourceDetails(ghc, accounts, filteredEvents);

        // Fixup and sanity checks
        foreach (var item in filteredEvents) {
          switch (item.Event) {
            case "crossreferenced":
              if (item.Actor != null) { // It's a comment reference
                accounts.Add(item.Source?.Actor);
                item.Actor = item.Source?.Actor;
              } else { // It's an issue reference
                accounts.Add(item.Source?.Issue?.User);
                item.Actor = item.Source?.Issue?.User;
              }
              break;
            case "committed":
              item.CreatedAt = item.ExtensionDataDictionary["committer"]["date"].ToObject<DateTimeOffset>();
              break;
            default:
              // Leave most things alone.
              break;
          }

          if (item.CreatedAt == DateTimeOffset.MinValue) {
            throw new Exception($"Unable to process event of type {item.Event} on {_repoFullName}/{_issueNumber} ({_issueId}). Invalid date.");
          }
        }

        await updater.UpdateTimelineEvents(_repoId, timelineResponse.Date, forUserId, accounts, filteredEvents);

        // Comments
        if (allEvents.Any(x => x.Event == "commented")) {
          await UpdateIssueComments(ghc, updater);
        }

        // Commit Comments
        // Comments in commit-commented events look complete.
        // Let's run with it.
        var commitCommentEvents = allEvents.Where(x => x.Event == "commit-commented").ToArray();
        if (commitCommentEvents.Any()) {
          var commitComments = commitCommentEvents
            .SelectMany(x => x.ExtensionDataDictionary["comments"].ToObject<IEnumerable<gm.CommitComment>>(GitHubSerialization.JsonSerializer))
            .ToArray();

          await updater.UpdateCommitComments(_repoId, timelineResponse.Date, commitComments);
        }

        // Merged event commit statuses
        if (_isPullRequest) {
          var mergedEvent = allEvents
            .Where(x => x.Event == "merged")
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefault();

          var mergeCommitId = mergedEvent?.CommitId;

          if (mergeCommitId != null) {
            var mergeCommitStatusesResponse = await ghc.CommitStatuses(_repoFullName, mergeCommitId, _prMergeStatusMetadata, RequestPriority.Interactive);
            if (mergeCommitStatusesResponse.IsOk) {
              await updater.UpdateCommitStatuses(_repoId, mergeCommitId, mergeCommitStatusesResponse.Result);
            }
            _prMergeStatusMetadata = GitHubMetadata.FromResponse(mergeCommitStatusesResponse);
          }
        }
      }
    }

    private async Task LookupEventSourceDetails(IGitHubActor ghc, HashSet<gm.Account> accounts, IEnumerable<gm.IssueEvent> events) {
      var withSources = events.Where(x => x.Source?.Url != null).ToArray();
      var sources = withSources.Select(x => x.Source.Url).Distinct().ToArray();

      if (sources.Any()) {
        var sourceLookups = sources
          .Select(x => {
            var parts = x.Split('/');
            var numParts = parts.Length;
            var repoName = parts[numParts - 4] + "/" + parts[numParts - 3];
            var issueNum = int.Parse(parts[numParts - 1]);
            return new {
              Id = x,
              Task = ghc.Issue(repoName, issueNum, priority: RequestPriority.Interactive),
            };
          })
          .ToDictionary(x => x.Id, x => x.Task);

        await Task.WhenAll(sourceLookups.Values);

        var prLookups = sourceLookups.Values
          .Where(x => x.Result.Result.PullRequest != null)
          .Select(x => {
            var url = x.Result.Result.PullRequest.Url;
            var parts = url.Split('/');
            var numParts = parts.Length;
            var repoName = parts[numParts - 4] + "/" + parts[numParts - 3];
            var prNum = int.Parse(parts[numParts - 1]);
            return new {
              Id = url,
              Task = ghc.PullRequest(repoName, prNum, priority: RequestPriority.Interactive),
            };
          })
          .ToDictionary(x => x.Id, x => x.Task);

        await Task.WhenAll(prLookups.Values);

        foreach (var item in withSources) {
          var refIssue = sourceLookups[item.Source.Url].Result.Result;
          accounts.Add(item.Source.Actor);
          if (refIssue.Assignees.Any()) {
            accounts.UnionWith(refIssue.Assignees);
          }
          accounts.Add(refIssue.ClosedBy);
          accounts.Add(refIssue.User);

          item.ExtensionDataDictionary["ship_issue_state"] = refIssue.State;
          item.ExtensionDataDictionary["ship_issue_title"] = refIssue.Title;

          if (refIssue.PullRequest != null) {
            item.ExtensionDataDictionary["ship_is_pull_request"] = true;

            var pr = prLookups[refIssue.PullRequest.Url].Result.Result;
            item.ExtensionDataDictionary["ship_pull_request_merged"] = pr.Merged;
          }
        }
      }
    }

    private async Task LookupEventCommitDetails(IGitHubActor ghc, HashSet<gm.Account> accounts, IEnumerable<gm.IssueEvent> events) {
      // Find all events with associated commits, and embed them.
      var withCommits = events.Where(x => !x.CommitUrl.IsNullOrWhiteSpace()).ToArray();
      var commits = withCommits.Select(x => x.CommitUrl).Distinct().ToArray();

      if (commits.Any()) {
        var commitLookups = commits
          .Select(x => {
            var parts = x.Split('/');
            var numParts = parts.Length;
            var repoName = parts[numParts - 4] + "/" + parts[numParts - 3];
            var sha = parts[numParts - 1];
            return new {
              Id = x,
              Task = ghc.Commit(repoName, sha, priority: RequestPriority.Interactive),
            };
          })
          .ToDictionary(x => x.Id, x => x.Task);

        // TODO: Lookup Repo Name->ID mapping

        await Task.WhenAll(commitLookups.Values);

        foreach (var item in withCommits) {
          var lookup = commitLookups[item.CommitUrl].Result;

          // best effort - requests will fail when the user doesn't have source access.
          // see Nick's account and references from the github-beta repo
          if (!lookup.IsOk) {
            continue;
          }

          var commit = lookup.Result;
          accounts.Add(commit.Author);
          accounts.Add(commit.Committer);
          item.ExtensionDataDictionary["ship_commit_message"] = commit.CommitDetails.Message;
          if (commit.Author != null) {
            item.ExtensionDataDictionary["ship_commit_author"] = JObject.FromObject(commit.Author);
          }
          if (commit.Committer != null) {
            item.ExtensionDataDictionary["ship_commit_committer"] = JObject.FromObject(commit.Committer);
          }
        }
      }
    }

    private async Task UpdateIssueReactions(IGitHubActor ghc, DataUpdater updater) {
      if (_reactionMetadata.IsExpired()) {
        var issueReactionsResponse = await ghc.IssueReactions(_repoFullName, _issueNumber, _reactionMetadata, RequestPriority.Interactive);
        if (issueReactionsResponse.IsOk) {
          await updater.UpdateIssueReactions(_repoId, issueReactionsResponse.Date, _issueId, issueReactionsResponse.Result);
        }
        _reactionMetadata = GitHubMetadata.FromResponse(issueReactionsResponse);
      }
    }

    private async Task UpdateIssueCommentReactions(IGitHubActor ghc, DataUpdater updater, ShipHubContext context) {
      var commentReactionMetadata = await context.IssueComments
        .AsNoTracking()
        .Where(x => x.IssueId == _issueId)
        .ToDictionaryAsync(x => x.Id, x => x.ReactionMetadata);

      if (commentReactionMetadata.Any()) {
        // Now, find the ones that need updating.
        var commentReactionRequests = new Dictionary<long, Task<GitHubResponse<IEnumerable<gm.Reaction>>>>();
        foreach (var reactionMetadata in commentReactionMetadata) {
          if (reactionMetadata.Value.IsExpired()) {
            commentReactionRequests.Add(reactionMetadata.Key, ghc.IssueCommentReactions(_repoFullName, reactionMetadata.Key, reactionMetadata.Value, RequestPriority.Interactive));
          }
        }

        if (commentReactionRequests.Any()) {
          await Task.WhenAll(commentReactionRequests.Values);

          foreach (var commentReactionsResponse in commentReactionRequests) {
            var resp = await commentReactionsResponse.Value;
            switch (resp.Status) {
              case HttpStatusCode.NotModified:
                break;
              case HttpStatusCode.NotFound:
                await updater.DeleteIssueComment(commentReactionsResponse.Key);
                break;
              default:
                await updater.UpdateIssueCommentReactions(_repoId, resp.Date, commentReactionsResponse.Key, resp.Result);
                break;
            }
            await context.UpdateMetadata("Comments", "ReactionMetadataJson", commentReactionsResponse.Key, resp);
          }
        }
      }
    }

    private async Task UpdateCommitCommentReactions(IGitHubActor ghc, DataUpdater updater, ShipHubContext context) {
      var committedEvents = await context.IssueEvents
        .AsNoTracking()
        .Where(x => x.IssueId == _issueId && x.Event == "committed")
        .ToArrayAsync();
      var commitShas = committedEvents
        .Select(x => x.ExtensionData.DeserializeObject<JToken>().Value<string>("sha"))
        .ToArray();

      if (commitShas.Any()) {
        var commitCommentCommentMetadata = await context.CommitComments
          .AsNoTracking()
          .Where(x => commitShas.Contains(x.CommitId))
          .ToDictionaryAsync(x => x.Id, x => x.ReactionMetadata);

        var commitCommentReactionRequests = new Dictionary<long, Task<GitHubResponse<IEnumerable<gm.Reaction>>>>();
        foreach (var reactionMetadata in commitCommentCommentMetadata) {
          if (reactionMetadata.Value.IsExpired()) {
            commitCommentReactionRequests.Add(reactionMetadata.Key, ghc.CommitCommentReactions(_repoFullName, reactionMetadata.Key, reactionMetadata.Value, RequestPriority.Interactive));
          }
        }

        if (commitCommentReactionRequests.Any()) {
          await Task.WhenAll(commitCommentReactionRequests.Values);

          foreach (var commitCommentReactionsResponse in commitCommentReactionRequests) {
            var resp = await commitCommentReactionsResponse.Value;
            switch (resp.Status) {
              case HttpStatusCode.NotModified:
                break;
              case HttpStatusCode.NotFound:
                await updater.DeleteCommitComment(commitCommentReactionsResponse.Key);
                break;
              default:
                await updater.UpdateCommitCommentReactions(_repoId, resp.Date, commitCommentReactionsResponse.Key, resp.Result);
                break;
            }
            await context.UpdateMetadata("CommitComments", "ReactionMetadataJson", commitCommentReactionsResponse.Key, resp);
          }
        }
      }
    }

    private async Task UpdatePullRequestCommentReactions(IGitHubActor ghc, DataUpdater updater, ShipHubContext context) {
      var prcReactionMetadata = await context.PullRequestComments
        .AsNoTracking()
        .Where(x => x.IssueId == _issueId)
        .ToDictionaryAsync(x => x.Id, x => x.ReactionMetadata);
      context.Database.Connection.Close();

      if (prcReactionMetadata.Any()) {
        var prcReactionRequests = new Dictionary<long, Task<GitHubResponse<IEnumerable<gm.Reaction>>>>();
        foreach (var reactionMetadata in prcReactionMetadata) {
          if (reactionMetadata.Value.IsExpired()) {
            prcReactionRequests.Add(reactionMetadata.Key, ghc.PullRequestCommentReactions(_repoFullName, reactionMetadata.Key, reactionMetadata.Value, RequestPriority.Interactive));
          }
        }

        if (prcReactionRequests.Any()) {
          await Task.WhenAll(prcReactionRequests.Values);

          foreach (var prcReactionsResponse in prcReactionRequests) {
            var resp = await prcReactionsResponse.Value;
            switch (resp.Status) {
              case HttpStatusCode.NotModified:
                break;
              case HttpStatusCode.NotFound:
                await updater.DeletePullRequestComment(prcReactionsResponse.Key);
                break;
              default:
                await updater.UpdatePullRequestCommentReactions(_repoId, resp.Date, prcReactionsResponse.Key, resp.Result);
                break;
            }
            await context.UpdateMetadata("PullRequestComments", "ReactionMetadataJson", prcReactionsResponse.Key, resp);
          }
        }
      }
    }
  }
}
