﻿namespace RealArtists.ShipHub.Common.DataModel {
  using System;
  using System.Collections.Generic;
  using System.ComponentModel.DataAnnotations;
  using System.ComponentModel.DataAnnotations.Schema;
  using System.Diagnostics.CodeAnalysis;

  public class Repository {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; }

    public long AccountId { get; set; }

    public bool Private { get; set; }

    [Required]
    [StringLength(255)]
    public string Name { get; set; }

    [Required]
    [StringLength(510)]
    public string FullName { get; set; }

    public DateTimeOffset Date { get; set; }

    //public long? AssignableMetadataId { get; set; }

    //public long? LabelMetadataId { get; set; }

    public virtual Account Account { get; set; }

    //public virtual GitHubMetadata AssignableMetadata { get; set; }

    //public virtual GitHubMetadata LabelMetadata { get; set; }

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<IssueEvent> Events { get; set; } = new HashSet<IssueEvent>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Issue> Issues { get; set; } = new HashSet<Issue>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Milestone> Milestones { get; set; } = new HashSet<Milestone>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<User> AssignableAccounts { get; set; } = new HashSet<User>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<AccountRepository> LinkedAccounts { get; set; } = new HashSet<AccountRepository>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Label> Labels { get; set; } = new HashSet<Label>();
  }
}
