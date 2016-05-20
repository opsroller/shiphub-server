﻿namespace RealArtists.ShipHub.Common.DataModel.Types {
  using System;

  public class IssueTableType {
    public int Id { get; set; }
    public int Number { get; set; }
    public string State { get; set; }
    public string Title { get; set; }
    public string Body { get; set; }
    public int? AssigneeId { get; set; }
    public int UserId { get; set; }
    public int? MilestoneId { get; set; }
    public bool Locked { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int? ClosedById { get; set; }
    public string Reactions { get; set; }
  }
}
