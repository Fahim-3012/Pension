-- Run this ONLY if you already imported the older smart_pension.sql and want to keep your data.
-- (Otherwise just run reset_database.sql and import the new smart_pension.sql.)
USE `smart_pension`;
ALTER TABLE `Pensioners`
  MODIFY `Phone` varchar(20) NOT NULL,
  ADD COLUMN `Gender` longtext NOT NULL,
  ADD COLUMN `Address` longtext NOT NULL,
  ADD COLUMN `NomineeName` longtext NOT NULL,
  ADD COLUMN `NomineeRelation` longtext NOT NULL,
  ADD COLUMN `IsApproved` tinyint(1) NOT NULL DEFAULT 1,
  ADD COLUMN `ConsentAt` datetime(6) NULL,
  ADD UNIQUE KEY `IX_Pensioners_Phone` (`Phone`);
-- accounts that existed before this upgrade are treated as approved
UPDATE `Pensioners` SET `IsApproved` = 1;
