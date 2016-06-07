﻿CREATE TYPE [dbo].[CommentTableType] AS TABLE (
  [Id]           BIGINT         NOT NULL,
  [IssueNumber]  INT            NOT NULL,
  [UserId]       BIGINT         NOT NULL,
  [Body]         NVARCHAR(MAX)  NOT NULL,
  [CreatedAt]    DATETIMEOFFSET NOT NULL,
  [UpdatedAt]    DATETIMEOFFSET NOT NULL,
  [Reactions]    NVARCHAR(300)  NULL
)