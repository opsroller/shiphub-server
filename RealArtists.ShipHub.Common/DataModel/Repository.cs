﻿namespace RealArtists.ShipHub.Common.DataModel {
  using System;
  using System.Collections.Generic;
  using System.ComponentModel.DataAnnotations;
  using System.ComponentModel.DataAnnotations.Schema;
  using System.Diagnostics.CodeAnalysis;

  public partial class Repository {
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    public int AccountId { get; set; }

    public bool Private { get; set; }

    [Required]
    [StringLength(100)]
    public string Name { get; set; }

    [Required]
    [StringLength(255)]
    public string FullName { get; set; }

    //public DateTimeOffset Date { get; set; }

    public long? AssignableMetaDataId { get; set; }

    public long? LabelMetaDataId { get; set; }

    [ConcurrencyCheck]
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public long? RowVersion { get; set; }

    public virtual Account Account { get; set; }

    public virtual GitHubMetaData AssignableMetaData { get; set; }

    public virtual GitHubMetaData LabelMetaData { get; set; }

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Comment> Comments { get; set; } = new HashSet<Comment>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Event> Events { get; set; } = new HashSet<Event>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Issue> Issues { get; set; } = new HashSet<Issue>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Milestone> Milestones { get; set; } = new HashSet<Milestone>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Account> AssignableAccounts { get; set; } = new HashSet<Account>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<AccountRepository> LinkedAccounts { get; set; } = new HashSet<AccountRepository>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Label> Labels { get; set; } = new HashSet<Label>();
  }
}