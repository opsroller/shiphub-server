﻿CREATE TABLE [GitHub].[Accounts] (
  [Id]          INT            NOT NULL,
  [AvatarUrl]   NVARCHAR(500)  NULL,
  [Company]     NVARCHAR(255)  NOT NULL,
  [Login]       NVARCHAR(255)  NOT NULL,
  [Name]        NVARCHAR(255)  NOT NULL,
  [CreatedAt]   DATETIMEOFFSET NOT NULL,
  [UpdatedAt]   DATETIMEOFFSET NOT NULL,
  [ETag]        NVARCHAR(64)   NOT NULL,
  [LastRefresh] DATETIMEOFFSET NOT NULL,
  CONSTRAINT [PK_GitHub_Accounts] PRIMARY KEY CLUSTERED ([Id] ASC),
);
GO