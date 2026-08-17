-- Schema for the Courier Online behavioral event table on cmlpsiturk.
--
-- Each POST to the /save route (custom.py) inserts one row; `data` is a JSON array of
-- {"seq": <int>, "event": "<DataPoint JSON string>"} objects. fetch_courier.py reads the
-- table back and rebuilds per-session files, ordering by `id`.
--
-- Apply with:  mysql -u maint -p psiturk < schema/vconline.sql
-- Safe to re-run; both statements are idempotent.

CREATE TABLE IF NOT EXISTS vconline (
  id            INT          NOT NULL AUTO_INCREMENT,
  prolific_pid  VARCHAR(255) NULL,
  study_id      VARCHAR(255) NULL,
  session_id    VARCHAR(255) NULL,
  experiment    VARCHAR(255) NULL,
  -- LONGTEXT, not TEXT: a single flush can exceed TEXT's 64 KB limit (the largest batch
  -- observed so far is 265 KB), and MySQL would truncate it into invalid JSON.
  data          LONGTEXT     NULL,
  timestamp     DATETIME     NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

-- Migration: distinguishes batches from different page loads.
--
-- window.courierSeq restarts at 0 on every page load, and fetch_courier.py dedupes with
-- by_pid[pid][seq] = event (last write wins). Without a per-load token, one participant who
-- reloads mid-task silently overwrites their own earlier events, and two sessions sharing a
-- pid annihilate each other during reconstruction.
--
-- MariaDB/MySQL have no "ADD COLUMN IF NOT EXISTS" portable across versions, so this is
-- wrapped to stay re-runnable.
SET @ddl = (
  SELECT IF(
    COUNT(*) = 0,
    'ALTER TABLE vconline ADD COLUMN load_token VARCHAR(64) NULL, ADD INDEX idx_pid_token (prolific_pid, load_token)',
    'SELECT ''load_token already present'''
  )
  FROM information_schema.COLUMNS
  WHERE TABLE_SCHEMA = DATABASE()
    AND TABLE_NAME   = 'vconline'
    AND COLUMN_NAME  = 'load_token'
);
PREPARE stmt FROM @ddl;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
