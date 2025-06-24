/*
 Navicat Premium Dump SQL

 Source Server         : MySQL
 Source Server Type    : MySQL
 Source Server Version : 80037 (8.0.37)
 Source Host           : localhost:3306
 Source Schema         : golf-club-db

 Target Server Type    : MySQL
 Target Server Version : 80037 (8.0.37)
 File Encoding         : 65001

 Date: 24/06/2025 08:07:21
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for employeehistory
-- ----------------------------
DROP TABLE IF EXISTS `employeehistory`;
CREATE TABLE `employeehistory`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `WorkerId` int NOT NULL,
  `ArrivalTime` datetime NOT NULL,
  `LeaveTime` datetime NULL DEFAULT NULL,
  `Status` int NOT NULL,
  `WorkHours` int GENERATED ALWAYS AS (greatest((timestampdiff(HOUR,`ArrivalTime`,`LeaveTime`) - 1),0)) STORED NULL,
  `MarkTime` datetime NULL DEFAULT NULL,
  `MarkZoneId` int NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `WorkerId`(`WorkerId` ASC) USING BTREE,
  INDEX `employeehistory_MarkZoneId`(`MarkZoneId` ASC) USING BTREE,
  CONSTRAINT `employeehistory_ibfk_1` FOREIGN KEY (`WorkerId`) REFERENCES `workers` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `employeehistory_MarkZoneId` FOREIGN KEY (`MarkZoneId`) REFERENCES `zones` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 28 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for holiday
-- ----------------------------
DROP TABLE IF EXISTS `holiday`;
CREATE TABLE `holiday`  (
  `Id` bigint UNSIGNED NOT NULL AUTO_INCREMENT,
  `ScheduleId` int UNSIGNED NULL DEFAULT NULL,
  `HolidayDate` datetime NOT NULL,
  `Description` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `Id`(`Id` ASC) USING BTREE,
  INDEX `FK_Holiday_Schedule_Id`(`ScheduleId` ASC) USING BTREE,
  CONSTRAINT `FK_Holiday_Schedule_Id` FOREIGN KEY (`ScheduleId`) REFERENCES `schedule` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 63 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for notify_history
-- ----------------------------
DROP TABLE IF EXISTS `notify_history`;
CREATE TABLE `notify_history`  (
  `id` int NOT NULL AUTO_INCREMENT,
  `worker_id` int NOT NULL,
  `arrival_time` datetime NOT NULL,
  `status` int NOT NULL,
  `mark_time` datetime NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `notify_history_ibfk_1`(`worker_id` ASC) USING BTREE,
  CONSTRAINT `notify_history_ibfk_1` FOREIGN KEY (`worker_id`) REFERENCES `workers` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 21 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for notify_jobs
-- ----------------------------
DROP TABLE IF EXISTS `notify_jobs`;
CREATE TABLE `notify_jobs`  (
  `id` int NOT NULL AUTO_INCREMENT,
  `organization_id` int NULL DEFAULT NULL,
  `zone_id` int NULL DEFAULT NULL,
  `worker_ids` json NULL,
  `percentage` decimal(5, 2) NULL DEFAULT NULL,
  `message` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `time` time NULL DEFAULT NULL,
  `shift_id` int UNSIGNED NULL DEFAULT NULL,
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `notify_jobs_org_id_ibfk_1`(`organization_id` ASC) USING BTREE,
  INDEX `notify_jobs_ZoneId`(`zone_id` ASC) USING BTREE,
  INDEX `notify_jobs_ShiftId`(`shift_id` ASC) USING BTREE,
  CONSTRAINT `notify_jobs_org_id_ibfk_1` FOREIGN KEY (`organization_id`) REFERENCES `organization` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `notify_jobs_ShiftId` FOREIGN KEY (`shift_id`) REFERENCES `schedule` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `notify_jobs_ZoneId` FOREIGN KEY (`zone_id`) REFERENCES `zones` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `check_worker_or_percent` CHECK ((`worker_ids` is not null) or (`percentage` is not null))
) ENGINE = InnoDB AUTO_INCREMENT = 15 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for organization
-- ----------------------------
DROP TABLE IF EXISTS `organization`;
CREATE TABLE `organization`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ParentOrganizationId` int NULL DEFAULT NULL,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_Organization_ParentOrganizationId`(`ParentOrganizationId` ASC) USING BTREE,
  CONSTRAINT `FK_Organization_Organization_ParentOrganizationId` FOREIGN KEY (`ParentOrganizationId`) REFERENCES `organization` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 36 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for schedule
-- ----------------------------
DROP TABLE IF EXISTS `schedule`;
CREATE TABLE `schedule`  (
  `Id` int UNSIGNED NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `PermissibleLateTimeStart` time NULL DEFAULT NULL,
  `PermissibleEarlyLeaveStart` time NULL DEFAULT NULL,
  `DeletedAt` datetime NULL DEFAULT NULL,
  `BreakStart` time NULL DEFAULT NULL,
  `BreakEnd` time NULL DEFAULT NULL,
  `PermissibleLateTimeEnd` time NULL DEFAULT NULL,
  `PermissibleEarlyLeaveEnd` time NULL DEFAULT NULL,
  `PermissionToLateTime` time NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `Id`(`Id` ASC) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 10 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for scheduleday
-- ----------------------------
DROP TABLE IF EXISTS `scheduleday`;
CREATE TABLE `scheduleday`  (
  `Id` int UNSIGNED NOT NULL AUTO_INCREMENT,
  `ScheduleId` int UNSIGNED NULL DEFAULT NULL,
  `DayOfWeek` varchar(50) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `WorkStart` time NULL DEFAULT NULL,
  `WorkEnd` time NULL DEFAULT NULL,
  `IsSelected` tinyint(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (`Id`) USING BTREE,
  UNIQUE INDEX `Id`(`Id` ASC) USING BTREE,
  INDEX `FK_ScheduleDay_Schedule_Id`(`ScheduleId` ASC) USING BTREE,
  CONSTRAINT `FK_ScheduleDay_Schedule_Id` FOREIGN KEY (`ScheduleId`) REFERENCES `schedule` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 58 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for users
-- ----------------------------
DROP TABLE IF EXISTS `users`;
CREATE TABLE `users`  (
  `Id` int NOT NULL,
  `Username` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Password` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Role` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for workers
-- ----------------------------
DROP TABLE IF EXISTS `workers`;
CREATE TABLE `workers`  (
  `FullName` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `JobTitle` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ChatId` bigint NULL DEFAULT NULL,
  `OrganizationId` int NULL DEFAULT NULL,
  `TelegramUsername` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL,
  `Mobile` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `AdditionalMobile` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `CardNumber` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `PhotoPath` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NULL DEFAULT NULL,
  `ZoneId` int NULL DEFAULT NULL,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  `StartWork` timestamp NOT NULL,
  `EndWork` timestamp NULL DEFAULT NULL,
  `Id` int NOT NULL AUTO_INCREMENT,
  `ScheduleId` int UNSIGNED NULL DEFAULT NULL,
  PRIMARY KEY (`Id`) USING BTREE,
  INDEX `IX_Workers_OrganizationId`(`OrganizationId` ASC) USING BTREE,
  INDEX `FK_Workers_ScheduleId`(`ScheduleId` ASC) USING BTREE,
  INDEX `FK_Workers_Organization_ZoneId`(`ZoneId` ASC) USING BTREE,
  CONSTRAINT `FK_Workers_Organization_OrganizationId` FOREIGN KEY (`OrganizationId`) REFERENCES `organization` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_Workers_Organization_ZoneId` FOREIGN KEY (`ZoneId`) REFERENCES `zones` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT,
  CONSTRAINT `FK_Workers_Schedule_Id` FOREIGN KEY (`ScheduleId`) REFERENCES `schedule` (`Id`) ON DELETE RESTRICT ON UPDATE RESTRICT
) ENGINE = InnoDB AUTO_INCREMENT = 33 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Table structure for zones
-- ----------------------------
DROP TABLE IF EXISTS `zones`;
CREATE TABLE `zones`  (
  `Id` int NOT NULL AUTO_INCREMENT,
  `Name` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `EnterIp` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `ExitIp` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Login` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `Password` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  `DeletedAt` timestamp NULL DEFAULT NULL,
  `NotifyIp` varchar(255) CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci NOT NULL,
  PRIMARY KEY (`Id`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 12 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_0900_ai_ci ROW_FORMAT = Dynamic;

-- ----------------------------
-- Procedure structure for POMELO_AFTER_ADD_PRIMARY_KEY
-- ----------------------------
DROP PROCEDURE IF EXISTS `POMELO_AFTER_ADD_PRIMARY_KEY`;
delimiter ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `POMELO_AFTER_ADD_PRIMARY_KEY`(IN `SCHEMA_NAME_ARGUMENT` VARCHAR(255), IN `TABLE_NAME_ARGUMENT` VARCHAR(255), IN `COLUMN_NAME_ARGUMENT` VARCHAR(255))
BEGIN
	DECLARE HAS_AUTO_INCREMENT_ID INT(11);
	DECLARE PRIMARY_KEY_COLUMN_NAME VARCHAR(255);
	DECLARE PRIMARY_KEY_TYPE VARCHAR(255);
	DECLARE SQL_EXP VARCHAR(1000);
	SELECT COUNT(*)
		INTO HAS_AUTO_INCREMENT_ID
		FROM `information_schema`.`COLUMNS`
		WHERE `TABLE_SCHEMA` = (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA()))
			AND `TABLE_NAME` = TABLE_NAME_ARGUMENT
			AND `COLUMN_NAME` = COLUMN_NAME_ARGUMENT
			AND `COLUMN_TYPE` LIKE '%int%'
			AND `COLUMN_KEY` = 'PRI';
	IF HAS_AUTO_INCREMENT_ID THEN
		SELECT `COLUMN_TYPE`
			INTO PRIMARY_KEY_TYPE
			FROM `information_schema`.`COLUMNS`
			WHERE `TABLE_SCHEMA` = (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA()))
				AND `TABLE_NAME` = TABLE_NAME_ARGUMENT
				AND `COLUMN_NAME` = COLUMN_NAME_ARGUMENT
				AND `COLUMN_TYPE` LIKE '%int%'
				AND `COLUMN_KEY` = 'PRI';
		SELECT `COLUMN_NAME`
			INTO PRIMARY_KEY_COLUMN_NAME
			FROM `information_schema`.`COLUMNS`
			WHERE `TABLE_SCHEMA` = (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA()))
				AND `TABLE_NAME` = TABLE_NAME_ARGUMENT
				AND `COLUMN_NAME` = COLUMN_NAME_ARGUMENT
				AND `COLUMN_TYPE` LIKE '%int%'
				AND `COLUMN_KEY` = 'PRI';
		SET SQL_EXP = CONCAT('ALTER TABLE `', (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA())), '`.`', TABLE_NAME_ARGUMENT, '` MODIFY COLUMN `', PRIMARY_KEY_COLUMN_NAME, '` ', PRIMARY_KEY_TYPE, ' NOT NULL AUTO_INCREMENT;');
		SET @SQL_EXP = SQL_EXP;
		PREPARE SQL_EXP_EXECUTE FROM @SQL_EXP;
		EXECUTE SQL_EXP_EXECUTE;
		DEALLOCATE PREPARE SQL_EXP_EXECUTE;
	END IF;
END
;;
delimiter ;

-- ----------------------------
-- Procedure structure for POMELO_BEFORE_DROP_PRIMARY_KEY
-- ----------------------------
DROP PROCEDURE IF EXISTS `POMELO_BEFORE_DROP_PRIMARY_KEY`;
delimiter ;;
CREATE DEFINER=`root`@`localhost` PROCEDURE `POMELO_BEFORE_DROP_PRIMARY_KEY`(IN `SCHEMA_NAME_ARGUMENT` VARCHAR(255), IN `TABLE_NAME_ARGUMENT` VARCHAR(255))
BEGIN
	DECLARE HAS_AUTO_INCREMENT_ID TINYINT(1);
	DECLARE PRIMARY_KEY_COLUMN_NAME VARCHAR(255);
	DECLARE PRIMARY_KEY_TYPE VARCHAR(255);
	DECLARE SQL_EXP VARCHAR(1000);
	SELECT COUNT(*)
		INTO HAS_AUTO_INCREMENT_ID
		FROM `information_schema`.`COLUMNS`
		WHERE `TABLE_SCHEMA` = (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA()))
			AND `TABLE_NAME` = TABLE_NAME_ARGUMENT
			AND `Extra` = 'auto_increment'
			AND `COLUMN_KEY` = 'PRI'
			LIMIT 1;
	IF HAS_AUTO_INCREMENT_ID THEN
		SELECT `COLUMN_TYPE`
			INTO PRIMARY_KEY_TYPE
			FROM `information_schema`.`COLUMNS`
			WHERE `TABLE_SCHEMA` = (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA()))
				AND `TABLE_NAME` = TABLE_NAME_ARGUMENT
				AND `COLUMN_KEY` = 'PRI'
			LIMIT 1;
		SELECT `COLUMN_NAME`
			INTO PRIMARY_KEY_COLUMN_NAME
			FROM `information_schema`.`COLUMNS`
			WHERE `TABLE_SCHEMA` = (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA()))
				AND `TABLE_NAME` = TABLE_NAME_ARGUMENT
				AND `COLUMN_KEY` = 'PRI'
			LIMIT 1;
		SET SQL_EXP = CONCAT('ALTER TABLE `', (SELECT IFNULL(SCHEMA_NAME_ARGUMENT, SCHEMA())), '`.`', TABLE_NAME_ARGUMENT, '` MODIFY COLUMN `', PRIMARY_KEY_COLUMN_NAME, '` ', PRIMARY_KEY_TYPE, ' NOT NULL;');
		SET @SQL_EXP = SQL_EXP;
		PREPARE SQL_EXP_EXECUTE FROM @SQL_EXP;
		EXECUTE SQL_EXP_EXECUTE;
		DEALLOCATE PREPARE SQL_EXP_EXECUTE;
	END IF;
END
;;
delimiter ;

INSERT INTO `users` VALUES (1, 'admin', 'admin123', 'Admin');
INSERT INTO `users` VALUES (2, 'hr', 'hr123', 'HR');

SET FOREIGN_KEY_CHECKS = 1;
