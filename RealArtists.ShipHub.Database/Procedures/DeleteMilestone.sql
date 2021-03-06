﻿CREATE PROCEDURE [dbo].[DeleteMilestone]
  @MilestoneId BIGINT
AS
BEGIN
  -- SET NOCOUNT ON added to prevent extra result sets from
  -- interfering with SELECT statements.
  SET NOCOUNT ON

  BEGIN TRY
    BEGIN TRANSACTION

    -- Clear foreign key references. As with labels,
    -- we don't need to track the issues modified because the client
    -- is smart enough to remove deleted milestones from issues.
    UPDATE Issues SET MilestoneId = NULL WHERE MilestoneId = @MilestoneId

    DELETE FROM Milestones WHERE Id = @MilestoneId

    UPDATE SyncLog SET
      [Delete] = 1,
      [RowVersion] = DEFAULT
    -- Crafty change output
    OUTPUT INSERTED.OwnerType as ItemType, INSERTED.OwnerId as ItemId
    WHERE ItemType = 'milestone' AND [Delete] = 0 AND ItemId = @MilestoneId

    COMMIT TRANSACTION
  END TRY
  BEGIN CATCH
    IF (XACT_STATE() != 0) ROLLBACK TRANSACTION;
    THROW;
  END CATCH
END
