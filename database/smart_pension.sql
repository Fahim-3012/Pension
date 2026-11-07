-- =====================================================================
-- Smart Pension Management System - MySQL / MariaDB schema
-- Works with XAMPP (phpMyAdmin), MySQL 8 and MariaDB 10.4+
-- Matches the ASP.NET Core 8 + Identity + EF Core (Pomelo) models exactly.
--
-- HOW TO USE: phpMyAdmin -> Import -> choose this file -> Go.
-- Then run the API WITHOUT "dotnet ef" (do not run migrations: tables
-- already exist). The API creates the Admin role/user on first start.
-- =====================================================================

CREATE DATABASE IF NOT EXISTS `smart_pension`
  CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE `smart_pension`;

SET FOREIGN_KEY_CHECKS = 0;

-- ---------------------- ASP.NET Core Identity -------------------------
CREATE TABLE IF NOT EXISTS `AspNetRoles` (
  `Id` varchar(255) NOT NULL,
  `Name` varchar(256) NULL,
  `NormalizedName` varchar(256) NULL,
  `ConcurrencyStamp` longtext NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `RoleNameIndex` (`NormalizedName`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUsers` (
  `Id` varchar(255) NOT NULL,
  `FullName` longtext NOT NULL,
  `UserName` varchar(256) NULL,
  `NormalizedUserName` varchar(256) NULL,
  `Email` varchar(256) NULL,
  `NormalizedEmail` varchar(256) NULL,
  `EmailConfirmed` tinyint(1) NOT NULL,
  `PasswordHash` longtext NULL,
  `SecurityStamp` longtext NULL,
  `ConcurrencyStamp` longtext NULL,
  `PhoneNumber` longtext NULL,
  `PhoneNumberConfirmed` tinyint(1) NOT NULL,
  `TwoFactorEnabled` tinyint(1) NOT NULL,
  `LockoutEnd` datetime(6) NULL,
  `LockoutEnabled` tinyint(1) NOT NULL,
  `AccessFailedCount` int NOT NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `UserNameIndex` (`NormalizedUserName`),
  KEY `EmailIndex` (`NormalizedEmail`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetRoleClaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `RoleId` varchar(255) NOT NULL,
  `ClaimType` longtext NULL,
  `ClaimValue` longtext NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AspNetRoleClaims_RoleId` (`RoleId`),
  CONSTRAINT `FK_AspNetRoleClaims_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`)
    REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserClaims` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` varchar(255) NOT NULL,
  `ClaimType` longtext NULL,
  `ClaimValue` longtext NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_AspNetUserClaims_UserId` (`UserId`),
  CONSTRAINT `FK_AspNetUserClaims_AspNetUsers_UserId` FOREIGN KEY (`UserId`)
    REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserLogins` (
  `LoginProvider` varchar(128) NOT NULL,
  `ProviderKey` varchar(128) NOT NULL,
  `ProviderDisplayName` longtext NULL,
  `UserId` varchar(255) NOT NULL,
  PRIMARY KEY (`LoginProvider`, `ProviderKey`),
  KEY `IX_AspNetUserLogins_UserId` (`UserId`),
  CONSTRAINT `FK_AspNetUserLogins_AspNetUsers_UserId` FOREIGN KEY (`UserId`)
    REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserRoles` (
  `UserId` varchar(255) NOT NULL,
  `RoleId` varchar(255) NOT NULL,
  PRIMARY KEY (`UserId`, `RoleId`),
  KEY `IX_AspNetUserRoles_RoleId` (`RoleId`),
  CONSTRAINT `FK_AspNetUserRoles_AspNetRoles_RoleId` FOREIGN KEY (`RoleId`)
    REFERENCES `AspNetRoles` (`Id`) ON DELETE CASCADE,
  CONSTRAINT `FK_AspNetUserRoles_AspNetUsers_UserId` FOREIGN KEY (`UserId`)
    REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AspNetUserTokens` (
  `UserId` varchar(255) NOT NULL,
  `LoginProvider` varchar(128) NOT NULL,
  `Name` varchar(128) NOT NULL,
  `Value` longtext NULL,
  PRIMARY KEY (`UserId`, `LoginProvider`, `Name`),
  CONSTRAINT `FK_AspNetUserTokens_AspNetUsers_UserId` FOREIGN KEY (`UserId`)
    REFERENCES `AspNetUsers` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ---------------------- Pension application tables -------------------
CREATE TABLE IF NOT EXISTS `Pensioners` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` longtext NOT NULL,
  `PensionId` varchar(255) NOT NULL,
  `FullName` longtext NOT NULL,
  `Phone` varchar(20) NOT NULL,
  `Gender` longtext NOT NULL,
  `Address` longtext NOT NULL,
  `NomineeName` longtext NOT NULL,
  `NomineeRelation` longtext NOT NULL,
  `Nid` varchar(255) NOT NULL,
  `DateOfBirth` datetime(6) NOT NULL,
  `RetirementDate` datetime(6) NOT NULL,
  `PensionCode` varchar(255) NOT NULL,           -- unique 11-digit code
  `PensionType` longtext NOT NULL,
  `MonthlyPension` decimal(18,2) NOT NULL DEFAULT 15000.00,
  `BankType` longtext NOT NULL,
  `BankName` longtext NOT NULL,
  `Branch` longtext NOT NULL,
  `AccountNumber` longtext NOT NULL,
  `AccountHolder` longtext NOT NULL,
  `BankVerified` tinyint(1) NOT NULL DEFAULT 0,
  `FaceReference` longtext NULL,
  `LastLifeVerification` datetime(6) NULL,
  `RegisteredAt` datetime(6) NOT NULL,
  `IsActive` tinyint(1) NOT NULL DEFAULT 1,
  `IsApproved` tinyint(1) NOT NULL DEFAULT 1,      -- new accounts active at once (admin can untick)
  `ConsentAt` datetime(6) NULL,
  PRIMARY KEY (`Id`),
  UNIQUE KEY `IX_Pensioners_PensionId` (`PensionId`),
  UNIQUE KEY `IX_Pensioners_PensionCode` (`PensionCode`),
  UNIQUE KEY `IX_Pensioners_Nid` (`Nid`),
  UNIQUE KEY `IX_Pensioners_Phone` (`Phone`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- Status: 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Completed
CREATE TABLE IF NOT EXISTS `Withdrawals` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `PensionerId` int NOT NULL,
  `Amount` decimal(18,2) NOT NULL,
  `RequestedAt` datetime(6) NOT NULL,
  `Status` int NOT NULL DEFAULT 0,
  `ProcessedAt` datetime(6) NULL,
  `AdminNote` longtext NULL,
  PRIMARY KEY (`Id`),
  KEY `IX_Withdrawals_PensionerId` (`PensionerId`),
  CONSTRAINT `FK_Withdrawals_Pensioners_PensionerId` FOREIGN KEY (`PensionerId`)
    REFERENCES `Pensioners` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `LifeVerifications` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `PensionerId` int NOT NULL,
  `VerifiedAt` datetime(6) NOT NULL,
  `Method` longtext NOT NULL,
  `Score` double NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `Notifications` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserId` longtext NOT NULL,
  `Message` longtext NOT NULL,
  `CreatedAt` datetime(6) NOT NULL,
  `IsRead` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS `AuditLogs` (
  `Id` int NOT NULL AUTO_INCREMENT,
  `UserName` longtext NOT NULL,
  `Action` longtext NOT NULL,
  `At` datetime(6) NOT NULL,
  PRIMARY KEY (`Id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

SET FOREIGN_KEY_CHECKS = 1;

-- Optional: pre-create the two roles (the API also does this automatically).
INSERT INTO `AspNetRoles` (`Id`, `Name`, `NormalizedName`, `ConcurrencyStamp`)
SELECT UUID(), 'Admin', 'ADMIN', UUID() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `AspNetRoles` WHERE `NormalizedName` = 'ADMIN');
INSERT INTO `AspNetRoles` (`Id`, `Name`, `NormalizedName`, `ConcurrencyStamp`)
SELECT UUID(), 'Pensioner', 'PENSIONER', UUID() FROM DUAL
WHERE NOT EXISTS (SELECT 1 FROM `AspNetRoles` WHERE `NormalizedName` = 'PENSIONER');
