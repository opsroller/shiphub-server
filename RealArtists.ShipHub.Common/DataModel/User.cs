﻿namespace RealArtists.ShipHub.Common.DataModel {
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;

  public class User : Account {
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Organization> Organizations { get; set; } = new HashSet<Organization>();

    public long? RepositoryMetaDataId { get; set; }

    public long? OrganizationMetaDataId { get; set; }

    public virtual GitHubMetaData RepositoryMetaData { get; set; }

    public virtual GitHubMetaData OrganizationMetaData { get; set; }

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<AccessToken> AccessTokens { get; set; } = new HashSet<AccessToken>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<Repository> AssignableRepositories { get; set; } = new HashSet<Repository>();

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<AccountRepository> LinkedRepositories { get; set; } = new HashSet<AccountRepository>();
  }
}
