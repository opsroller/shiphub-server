﻿namespace RealArtists.ShipHub.Common.GitHub.Models {
  using System;
  using System.Collections.Generic;
  using System.Runtime.Serialization;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;

  public class TimelineEvent {
    private static readonly HashSet<string> _issueEventTypeNames = new HashSet<string>(Enum.GetNames(typeof(IssueEvent)));


    public long Id { get; set; }
    public string Url { get; set; }
    public Account Actor { get; set; }
    public string CommitId { get; set; }
    public TimelineEventType Event { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    //public Label Label { get; set; }
    public Account Assignee { get; set; }
    //public Milestone Milestone { get; set; } // What GitHub sends is incomplete and nearly useless. No need to parse.
    //public ReferenceSource Source { get; set; } // See below
    //public TimelineRename Rename { get; set; }

    // We want this to be saved in _extensionData, so don't actually deserialize it.
    [JsonIgnore]
    public ReferenceSource Source { get { return _extensionData.Val("source")?.ToObject<ReferenceSource>(); } }

    // Because now they're mingled
    public bool IsIssueEvent { get { return _issueEventTypeNames.Contains(Event.ToString()); } }

    /// <summary>
    /// Just in case (for future compatibility)
    /// </summary>
    [JsonExtensionData]
    public IDictionary<string, JToken> _extensionData = new Dictionary<string, JToken>();

    [JsonIgnore]
    public string ExtensionData {
      get {
        return _extensionData.SerializeObject(Formatting.None);
      }
      set {
        if (value != null) {
          _extensionData = JsonConvert.DeserializeObject<Dictionary<string, JToken>>(value);
        } else {
          _extensionData.Clear();
        }
      }
    }
  }

  public class TimelineRename {
    public string From { get; set; }
    public string To { get; set; }
  }

  public class ReferenceSource {
    public long Id { get; set; }
    public Account Actor { get; set; }
    public string Url { get; set; }
  }

  public enum TimelineEventType {
    /// <summary>
    /// Not a valid value. Uninitialized or an error.
    /// </summary>
    Unspecified = 0,

    /// <summary>
    /// The issue was assigned to the actor.
    /// </summary>
    [EnumMember(Value = "assigned")]
    Assigned,

    /// <summary>
    /// The issue was closed by the actor. When the commit_id is present, it identifies the commit that closed the issue using "closes / fixes #NN" syntax.
    /// </summary>
    [EnumMember(Value = "closed")]
    Closed,

    /// <summary>
    /// A comment was added to the issue.
    /// </summary>
    [EnumMember(Value = "commented")]
    Commented,

    /// <summary>
    /// A commit was added to the pull request's `HEAD` branch. Only provided for pull requests.
    /// </summary>
    [EnumMember(Value = "committed")]
    Committed,

    /// <summary>
    /// The issue was referenced from another issue. The `source` attribute contains the `id`, `actor`, and `url` of the reference's source.
    /// </summary>
    [EnumMember(Value = "cross-referenced")]
    CrossReferenced,

    /// <summary>
    /// The issue was removed from a milestone.
    /// </summary>
    [EnumMember(Value = "demilestoned")]
    Demilestoned,

    /// <summary>
    /// The pull request's branch was deleted.
    /// </summary>
    [EnumMember(Value = "head_ref_deleted")]
    HeadRefDeleted,

    /// <summary>
    /// The pull request's branch was restored
    /// </summary>
    [EnumMember(Value = "head_ref_restored")]
    HeadRefRestored,

    /// <summary>
    /// A label was added to the issue.
    /// </summary>
    [EnumMember(Value = "labeled")]
    Labeled,

    /// <summary>
    /// The issue was locked by the actor.
    /// </summary>
    [EnumMember(Value = "locked")]
    Locked,

    /// <summary>
    /// The actor was @mentioned in an issue body.
    /// </summary>
    [EnumMember(Value = "mentioned")]
    Mentioned,

    /// <summary>
    /// The issue was merged by the actor. The `commit_id` attribute is the SHA1 of the HEAD commit that was merged.
    /// </summary>
    [EnumMember(Value = "merged")]
    Merged,

    /// <summary>
    /// The issue was added to a milestone.
    /// </summary>
    [EnumMember(Value = "milestoned")]
    Milestoned,

    /// <summary>
    /// The issue was referenced from a commit message. The `commit_id` attribute is the commit SHA1 of where that happened.
    /// </summary>
    [EnumMember(Value = "referenced")]
    Referenced,

    /// <summary>
    /// The issue title was changed.
    /// </summary>
    [EnumMember(Value = "renamed")]
    Renamed,

    /// <summary>
    /// The issue was reopened by the actor.
    /// </summary>
    [EnumMember(Value = "reopened")]
    Reopened,

    /// <summary>
    /// The actor subscribed to receive notifications for an issue.
    /// </summary>
    [EnumMember(Value = "subscribed")]
    Subscribed,

    /// <summary>
    /// The actor was unassigned from the issue.
    /// </summary>
    [EnumMember(Value = "unassigned")]
    Unassigned,

    /// <summary>
    /// A label was removed from the issue.
    /// </summary>
    [EnumMember(Value = "unlabeled")]
    Unlabeled,

    /// <summary>
    /// The issue was unlocked by the actor.
    /// </summary>
    [EnumMember(Value = "unlocked")]
    Unlocked,

    /// <summary>
    /// The actor unsubscribed to stop receiving notifications for an issue.
    /// </summary>
    [EnumMember(Value = "unsubscribed")]
    Unsubscribed,
  }
}
